using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ClockScreensaver;

public class WeatherService : IDisposable
{
    private static readonly string CacheDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ClockScreensaver");
    private static readonly string CacheFile = Path.Combine(CacheDir, "weather.json");

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private volatile WeatherResult? _cached;
    private System.Threading.Timer? _timer;
    private readonly Settings _settings;

    public WeatherResult? Current => _cached;
    public event Action? Updated;

    public WeatherService(Settings settings)
    {
        _settings = settings;
        _cached = LoadCache();
        if (_cached != null)
            Logger.Log($"Loaded cache: {_cached.Condition} {_cached.TempC:F1}°C at {_cached.FetchedAt:HH:mm:ss} (lat={_cached.Latitude:F3},lon={_cached.Longitude:F3})");
    }

    public void Start()
    {
        Logger.Log("WeatherService.Start()");
        _ = Task.Run(FetchAsync);
        _timer = new System.Threading.Timer(_ => _ = Task.Run(FetchAsync),
            null, TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(30));
    }

    private async Task FetchAsync()
    {
        try
        {
            float lat, lon;
            string source;

            bool useCity = !_settings.LocationAuto && !string.IsNullOrWhiteSpace(_settings.CityName);
            if (useCity)
            {
                Logger.Log($"Geocoding city: \"{_settings.CityName}\"");
                (lat, lon) = await GeocodeCityAsync(_settings.CityName);
                source = $"city:{_settings.CityName}";
            }
            else
            {
                Logger.Log($"Using IP location (LocationAuto={_settings.LocationAuto}, city=\"{_settings.CityName}\")");
                (lat, lon) = await GetIpLocationAsync();
                source = "ip";
            }

            Logger.Log($"Location resolved [{source}]: lat={lat:F4} lon={lon:F4}");

            var url = $"https://api.open-meteo.com/v1/forecast" +
                      $"?latitude={lat:F4}&longitude={lon:F4}" +
                      $"&current=temperature_2m,weathercode" +
                      $"&daily=sunrise,sunset&timezone=auto&forecast_days=1";

            Logger.Log($"Fetching: {url}");
            var json = await _http.GetStringAsync(url);
            var doc = JsonNode.Parse(json)!;

            var tempC = doc["current"]!["temperature_2m"]!.GetValue<float>();
            var wmo   = doc["current"]!["weathercode"]!.GetValue<int>();
            var cond  = WmoMapper.Map(wmo);

            DateTime sunrise = default, sunset = default;
            var dailyArr  = doc["daily"]?["sunrise"]?.AsArray();
            var sunsetArr = doc["daily"]?["sunset"]?.AsArray();
            if (dailyArr?.Count > 0)  DateTime.TryParse(dailyArr[0]!.GetValue<string>(),  out sunrise);
            if (sunsetArr?.Count > 0) DateTime.TryParse(sunsetArr[0]!.GetValue<string>(), out sunset);

            _cached = new WeatherResult
            {
                Condition  = cond,
                TempC      = tempC,
                Sunrise    = sunrise,
                Sunset     = sunset,
                FetchedAt  = DateTime.Now,
                Latitude   = lat,
                Longitude  = lon,
            };

            Logger.Log($"Weather OK: wmo={wmo} → {cond}, temp={tempC:F1}°C, sunrise={sunrise:HH:mm}, sunset={sunset:HH:mm}");
            SaveCache(_cached);
            Updated?.Invoke();
        }
        catch (Exception ex)
        {
            Logger.Log($"FetchAsync ERROR: {ex.GetType().Name}: {ex.Message}");
            // Keep using cached result if available
        }
    }

    private async Task<(float lat, float lon)> GetIpLocationAsync()
    {
        Logger.Log("Fetching IP location from ipapi.co...");
        var json = await _http.GetStringAsync("https://ipapi.co/json/");
        var doc  = JsonNode.Parse(json)!;
        var lat  = doc["latitude"]!.GetValue<float>();
        var lon  = doc["longitude"]!.GetValue<float>();
        var city = doc["city"]?.GetValue<string>() ?? "?";
        Logger.Log($"IP location: {city} ({lat:F3},{lon:F3})");
        return (lat, lon);
    }

    private async Task<(float lat, float lon)> GeocodeCityAsync(string city)
    {
        var encoded = Uri.EscapeDataString(city);
        var url     = $"https://geocoding-api.open-meteo.com/v1/search?name={encoded}&count=1&language=en&format=json";
        Logger.Log($"Geocoding: {url}");
        var json    = await _http.GetStringAsync(url);
        var doc     = JsonNode.Parse(json)!;
        var results = doc["results"]?.AsArray();
        if (results == null || results.Count == 0)
            throw new Exception($"City not found: \"{city}\"");
        var lat  = results[0]!["latitude"]!.GetValue<float>();
        var lon  = results[0]!["longitude"]!.GetValue<float>();
        var name = results[0]!["name"]?.GetValue<string>() ?? city;
        Logger.Log($"Geocoded \"{city}\" → {name} ({lat:F3},{lon:F3})");
        return (lat, lon);
    }

    // Used by settings Test button — returns a human-readable result or throws
    public async Task<string> TestAsync(string? city)
    {
        float lat, lon;
        string place;

        if (string.IsNullOrWhiteSpace(city))
        {
            var json = await _http.GetStringAsync("https://ipapi.co/json/");
            var doc  = JsonNode.Parse(json)!;
            lat   = doc["latitude"]!.GetValue<float>();
            lon   = doc["longitude"]!.GetValue<float>();
            place = doc["city"]?.GetValue<string>() ?? $"{lat:F2}°, {lon:F2}°";
        }
        else
        {
            var encoded = Uri.EscapeDataString(city);
            var geoJson = await _http.GetStringAsync(
                $"https://geocoding-api.open-meteo.com/v1/search?name={encoded}&count=1&language=en&format=json");
            var doc     = JsonNode.Parse(geoJson)!;
            var results = doc["results"]?.AsArray();
            if (results == null || results.Count == 0)
                throw new Exception($"City not found: \"{city}\"");
            lat   = results[0]!["latitude"]!.GetValue<float>();
            lon   = results[0]!["longitude"]!.GetValue<float>();
            var name    = results[0]!["name"]?.GetValue<string>() ?? city;
            var country = results[0]!["country_code"]?.GetValue<string>();
            place = country != null ? $"{name}, {country}" : name;
        }

        var wxJson = await _http.GetStringAsync(
            $"https://api.open-meteo.com/v1/forecast?latitude={lat:F4}&longitude={lon:F4}" +
            $"&current=temperature_2m,weathercode&timezone=auto");
        var wxDoc  = JsonNode.Parse(wxJson)!;
        var tempC  = wxDoc["current"]!["temperature_2m"]!.GetValue<float>();
        var wmo    = wxDoc["current"]!["weathercode"]!.GetValue<int>();
        var cond   = WmoMapper.Map(wmo);

        return $"{place} — {WmoMapper.Name(cond)}, {(int)MathF.Round(tempC)}°C";
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
