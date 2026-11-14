namespace ClockScreensaver;

// Everything that decides what the screen shows at a given moment, worked out in one
// place so the screensaver, the Windows preview and the test renderer agree.
public readonly record struct SceneState(
    WeatherCondition? EffectsCondition, // which particle effects to draw; null = effects off
    bool IsNight,                       // night darkening + dimmed clock (only when effects are on)
    WeatherCondition PictureWeather,    // which landscape picture to choose; independent of effects
    SceneTime Time,
    Season Season)
{
    public static WeatherCondition? FromPreview(PreviewCondition p) => p switch
    {
        PreviewCondition.Clear        => WeatherCondition.Clear,
        PreviewCondition.PartlyCloudy => WeatherCondition.PartlyCloudy,
        PreviewCondition.Cloudy       => WeatherCondition.Cloudy,
        PreviewCondition.Rain         => WeatherCondition.Rain,
        PreviewCondition.Snow         => WeatherCondition.Snow,
        PreviewCondition.Storm        => WeatherCondition.Storm,
        _                             => null,
    };

    public static SceneState Compute(Settings s, WeatherResult? weather)
    {
        // The weather the scene should represent: the preview override, otherwise live
        // weather. Live weather is used for the picture even when "Show weather" is off,
        // because that option only hides the line under the clock.
        WeatherCondition? actual = FromPreview(s.PreviewCondition) ?? weather?.Condition;

        WeatherCondition? effects = null;
        bool isNight = false;
        if (s.EffectsOn)
        {
            effects = FromPreview(s.PreviewCondition)
                      ?? (s.WeatherOn ? weather?.Condition : null)
                      ?? WeatherCondition.Clear; // ambient stars/rays even before weather arrives
            isNight = weather?.IsNight(s.TimeOfDay) ?? DefaultIsNight(s.TimeOfDay);
        }

        var time = weather?.GetSceneTime(s.TimeOfDay) ?? DefaultSceneTime(s.TimeOfDay);
        var season = SeasonCalc.GetSeason(DateTime.Now, weather?.Latitude ?? 0f);
        return new SceneState(effects, isNight, actual ?? WeatherCondition.Clear, time, season);
    }

    public static bool DefaultIsNight(TimeOfDay tod)
    {
        if (tod == TimeOfDay.Day) return false;
        if (tod == TimeOfDay.Night) return true;
        int h = DateTime.Now.Hour;
        return h >= 20 || h < 6;
    }

    public static SceneTime DefaultSceneTime(TimeOfDay tod) =>
        DefaultIsNight(tod) ? SceneTime.Night : SceneTime.Day;
}
