using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ClockScreensaver;

public class WeatherService : IDisposable
{
    private static readonly string CacheDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClockScreensaver");
    private static readonly string CacheFile = Path.Combine(CacheDir, "weather.json");

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };
    private volatile WeatherResult? _cached;
    private System.Threading.Timer? _timer;
    private readonly Settings _settings;

    public WeatherResult? Current => _cached;
    public event Action? Updated;

    public WeatherService(Settings settings)
    {
        _settings = settings;
        _cached = LoadCache();
    }

    public void Start()
    {
        // Fetch immediately in background, then every 30 minutes
        _ = Task.Run(FetchAsync);
        _timer = new System.Threading.Timer(_ => _ = Task.Run(FetchAsync),
            null, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30));
    }

    private async Task FetchAsync()
    {
        try
        {
            float lat, lon;
            if (_settings.LocationAuto || string.IsNullOrWhiteSpace(_settings.CityName))
                (lat, lon) = await GetIpLocationAsync();
            else
                (lat, lon) = await GeocodeCityAsync(_settings.CityName);

            var url = $"https://api.open-meteo.com/v1/forecast" +
                      $"?latitude={lat:F4}&longitude={lon:F4}" +
                      $"&current=temperature_2m,weathercode" +
                      $"&daily=sunrise,sunset&timezone=auto&forecast_days=1";

            var json = await _http.GetStringAsync(url);
            var doc = JsonNode.Parse(json)!;

            var tempC = doc["current"]!["temperature_2m"]!.GetValue<float>();
            var wmo   = doc["current"]!["weathercode"]!.GetValue<int>();

            DateTime sunrise = default, sunset = default;
            var dailyArr = doc["daily"]?["sunrise"]?.AsArray();
            var sunsetArr = doc["daily"]?["sunset"]?.AsArray();
            if (dailyArr?.Count > 0)
                DateTime.TryParse(dailyArr[0]!.GetValue<string>(), out sunrise);
            if (sunsetArr?.Count > 0)
                DateTime.TryParse(sunsetArr[0]!.GetValue<string>(), out sunset);

            _cached = new WeatherResult
            {
                Condition  = WmoMapper.Map(wmo),
                TempC      = tempC,
                Sunrise    = sunrise,
                Sunset     = sunset,
                FetchedAt  = DateTime.Now,
                Latitude   = lat,
                Longitude  = lon,
            };
            SaveCache(_cached);
            Updated?.Invoke();
        }
        catch { /* use cached or nothing */ }
    }

    private async Task<(float lat, float lon)> GetIpLocationAsync()
    {
        var json = await _http.GetStringAsync("https://ipapi.co/json/");
        var doc = JsonNode.Parse(json)!;
        var lat = doc["latitude"]!.GetValue<float>();
        var lon = doc["longitude"]!.GetValue<float>();
        return (lat, lon);
    }

    private async Task<(float lat, float lon)> GeocodeCityAsync(string city)
    {
        var encoded = Uri.EscapeDataString(city);
        var json = await _http.GetStringAsync(
            $"https://geocoding-api.open-meteo.com/v1/search?name={encoded}&count=1&language=en&format=json");
        var doc = JsonNode.Parse(json)!;
        var results = doc["results"]?.AsArray();
        if (results == null || results.Count == 0)
            throw new Exception("City not found");
        var lat = results[0]!["latitude"]!.GetValue<float>();
        var lon = results[0]!["longitude"]!.GetValue<float>();
        return (lat, lon);
    }

    private static WeatherResult? LoadCache()
    {
        try
        {
            if (!File.Exists(CacheFile)) return null;
            var json = File.ReadAllText(CacheFile);
            return JsonSerializer.Deserialize<WeatherResult>(json);
        }
        catch { return null; }
    }

    private static void SaveCache(WeatherResult r)
    {
        try
        {
            Directory.CreateDirectory(CacheDir);
            File.WriteAllText(CacheFile, JsonSerializer.Serialize(r));
        }
        catch { }
    }

    public void Dispose()
    {
        _timer?.Dispose();
        _http.Dispose();
    }
}
