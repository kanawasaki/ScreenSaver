namespace ClockScreensaver;

public enum WeatherCondition { Clear, PartlyCloudy, Cloudy, Rain, Snow, Storm }

public class WeatherResult
{
    public WeatherCondition Condition { get; set; }
    public float TempC { get; set; }
    public DateTime Sunrise { get; set; }
    public DateTime Sunset { get; set; }
    public DateTime FetchedAt { get; set; }
    public float Latitude { get; set; }
    public float Longitude { get; set; }

    public bool IsNight(TimeOfDay tod)
    {
        if (tod == TimeOfDay.Day) return false;
        if (tod == TimeOfDay.Night) return true;
        var now = DateTime.Now;
        if (Sunrise == default || Sunset == default)
        {
            // Fallback: 8pm–6am
            var h = now.Hour;
            return h >= 20 || h < 6;
        }
        return now < Sunrise || now > Sunset;
    }

    public float GetTemp(WeatherUnit unit) =>
        unit == WeatherUnit.F ? MathF.Round(TempC * 9f / 5f + 32f) : TempC;
}

public static class WmoMapper
{
    // WMO weather interpretation codes → our 6 conditions
    public static WeatherCondition Map(int code) => code switch
    {
        0                         => WeatherCondition.Clear,
        1 or 2                    => WeatherCondition.PartlyCloudy,
        3                         => WeatherCondition.Cloudy,
        45 or 48                  => WeatherCondition.Cloudy,       // fog
        51 or 53 or 55            => WeatherCondition.Rain,         // drizzle
        56 or 57                  => WeatherCondition.Rain,         // freezing drizzle
        61 or 63 or 65            => WeatherCondition.Rain,
        66 or 67                  => WeatherCondition.Rain,         // freezing rain
        71 or 73 or 75 or 77      => WeatherCondition.Snow,
        80 or 81 or 82            => WeatherCondition.Rain,         // showers
        85 or 86                  => WeatherCondition.Snow,         // snow showers
        95                        => WeatherCondition.Storm,
        96 or 99                  => WeatherCondition.Storm,
        _                         => WeatherCondition.Cloudy,
    };

    public static string Name(WeatherCondition c) => c switch
    {
        WeatherCondition.Clear        => "Clear",
        WeatherCondition.PartlyCloudy => "Partly cloudy",
        WeatherCondition.Cloudy       => "Cloudy",
        WeatherCondition.Rain         => "Rain",
        WeatherCondition.Snow         => "Snow",
        WeatherCondition.Storm        => "Thunderstorm",
        _                             => "",
    };
}
