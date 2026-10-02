namespace ClockScreensaver;

public enum SceneTime { Day, Night, Dawn, Dusk }
public enum Season { Spring, Summer, Autumn, Winter }

public record LandscapePicture(string Path, WeatherCondition Weather, SceneTime? Time, Season? Season);

public record LandscapeScanResult(string Folder, List<LandscapePicture> Pictures, List<string> IgnoredFiles);

public static class SeasonCalc
{
    // Meteorological seasons by month; mirrored across the equator.
    public static Season GetSeason(DateTime date, float latitude)
    {
        Season northern = date.Month switch
        {
            12 or 1 or 2 => Season.Winter,
            3 or 4 or 5  => Season.Spring,
            6 or 7 or 8  => Season.Summer,
            _            => Season.Autumn,
        };
        if (latitude >= 0) return northern;
        return northern switch
        {
            Season.Winter => Season.Summer,
            Season.Summer => Season.Winter,
            Season.Spring => Season.Autumn,
            _             => Season.Spring,
        };
    }
}

// Parses, caches, and picks background pictures from a folder of weather-tagged landscape
// images named like "rain-night-winter.jpg" (words separated by - _ or space, any order,
// case-insensitive, unknown words ignored).
public class LandscapeLibrary
{
    private static readonly Dictionary<string, WeatherCondition> WeatherWords =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["clear"] = WeatherCondition.Clear,
            ["partly"] = WeatherCondition.PartlyCloudy,
            ["cloudy"] = WeatherCondition.Cloudy,
            ["rain"] = WeatherCondition.Rain,
            ["snow"] = WeatherCondition.Snow,
            ["storm"] = WeatherCondition.Storm,
        };

    private static readonly Dictionary<string, SceneTime> TimeWords =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["day"] = SceneTime.Day,
            ["night"] = SceneTime.Night,
            ["dawn"] = SceneTime.Dawn,
            ["dusk"] = SceneTime.Dusk,
        };

    private static readonly Dictionary<string, Season> SeasonWords =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["spring"] = Season.Spring,
            ["summer"] = Season.Summer,
            ["autumn"] = Season.Autumn,
            ["winter"] = Season.Winter,
        };

    private static readonly HashSet<string> ValidExt =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };

    private LandscapeScanResult? _result;
    private string? _lastPicked;

    public LandscapeScanResult? Result => _result;

    // Rescans only when the folder differs from what's cached — cheap to call often.
    public LandscapeScanResult EnsureScanned(string folder)
    {
        folder ??= "";
        if (_result == null || !string.Equals(_result.Folder, folder, StringComparison.OrdinalIgnoreCase))
            _result = Scan(folder);
        return _result;
    }

    public static bool TryParse(string fileName, out WeatherCondition weather, out SceneTime? time, out Season? season)
    {
        weather = default; time = null; season = null;
        string ext = Path.GetExtension(fileName);
        if (!ValidExt.Contains(ext)) return false;

        string stem = Path.GetFileNameWithoutExtension(fileName);
        var words = stem.Split(new[] { '-', '_', ' ' }, StringSplitOptions.RemoveEmptyEntries);

        WeatherCondition? w = null;
        foreach (var word in words)
        {
            if (w == null && WeatherWords.TryGetValue(word, out var wv)) w = wv;
            else if (time == null && TimeWords.TryGetValue(word, out var tv)) time = tv;
            else if (season == null && SeasonWords.TryGetValue(word, out var sv)) season = sv;
        }
        if (w == null) return false;
        weather = w.Value;
        return true;
    }

    public static LandscapeScanResult Scan(string folder)
    {
        var pics = new List<LandscapePicture>();
        var ignored = new List<string>();
        try
        {
            if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
            {
                foreach (var file in Directory.GetFiles(folder))
                {
                    string name = Path.GetFileName(file);
                    if (TryParse(name, out var w, out var t, out var s))
                        pics.Add(new LandscapePicture(file, w, t, s));
                    else
                        ignored.Add(name);
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"Landscape scan FAILED for \"{folder}\": {ex.GetType().Name}: {ex.Message}");
        }

        if (ignored.Count > 0)
            Logger.Log($"Landscape scan: ignored {ignored.Count} file(s) without a weather word: {string.Join(", ", ignored)}");
        Logger.Log($"Landscape scan: {pics.Count} picture(s) found in \"{folder}\"");
        return new LandscapeScanResult(folder, pics, ignored);
    }

    // Matching order: weather+time+season -> weather+time -> weather+nearest-time ->
    // related weather (same time rules) -> null (caller falls back to "My picture" or black).
    public LandscapePicture? Pick(WeatherCondition weather, SceneTime time, Season season, Random rng)
    {
        var pictures = _result?.Pictures;
        if (pictures == null || pictures.Count == 0) return null;

        foreach (var w in WeatherFallbackChain(weather))
        {
            var exact = pictures.Where(p => p.Weather == w
                && (p.Time == null || p.Time == time)
                && (p.Season == null || p.Season == season)).ToList();
            if (exact.Count > 0)
                return LogChoice(Choose(exact, rng), weather, time, season, $"weather+time+season (matched {w})");

            var anySeason = pictures.Where(p => p.Weather == w
                && (p.Time == null || p.Time == time)).ToList();
            if (anySeason.Count > 0)
                return LogChoice(Choose(anySeason, rng), weather, time, season, $"weather+time (matched {w})");

            foreach (var nt in NearestTimes(time))
            {
                var nearest = pictures.Where(p => p.Weather == w
                    && (p.Time == null || p.Time == nt)).ToList();
                if (nearest.Count > 0)
                    return LogChoice(Choose(nearest, rng), weather, time, season, $"weather+nearestTime({nt}) (matched {w})");
            }
        }

        Logger.Log($"Landscape pick: no match for {weather}/{time}/{season} among {pictures.Count} picture(s)");
        return null;
    }

    private LandscapePicture LogChoice(LandscapePicture c, WeatherCondition weather, SceneTime time, Season season, string tier)
    {
        Logger.Log($"Landscape pick: {weather}/{time}/{season} -> \"{Path.GetFileName(c.Path)}\" (tier={tier})");
        return c;
    }

    private LandscapePicture Choose(List<LandscapePicture> candidates, Random rng)
    {
        var pool = candidates;
        if (candidates.Count > 1 && _lastPicked != null)
        {
            var filtered = candidates.Where(p => p.Path != _lastPicked).ToList();
            if (filtered.Count > 0) pool = filtered;
        }
        var chosen = pool[rng.Next(pool.Count)];
        _lastPicked = chosen.Path;
        return chosen;
    }

    private static IEnumerable<WeatherCondition> WeatherFallbackChain(WeatherCondition w) => w switch
    {
        WeatherCondition.Storm        => new[] { WeatherCondition.Storm, WeatherCondition.Rain, WeatherCondition.Cloudy },
        WeatherCondition.PartlyCloudy => new[] { WeatherCondition.PartlyCloudy, WeatherCondition.Clear, WeatherCondition.Cloudy },
        WeatherCondition.Snow         => new[] { WeatherCondition.Snow, WeatherCondition.Cloudy },
        _                             => new[] { w },
    };

    private static IEnumerable<SceneTime> NearestTimes(SceneTime t) => t switch
    {
        SceneTime.Dawn  => new[] { SceneTime.Day, SceneTime.Night },
        SceneTime.Dusk  => new[] { SceneTime.Day, SceneTime.Night },
        SceneTime.Day   => new[] { SceneTime.Night },
        SceneTime.Night => new[] { SceneTime.Day },
        _               => Array.Empty<SceneTime>(),
    };
}

public static class LandscapeSummary
{
    private static readonly (WeatherCondition cond, string word)[] WeatherFileWord =
    {
        (WeatherCondition.Clear, "clear"),
        (WeatherCondition.PartlyCloudy, "partly"),
        (WeatherCondition.Cloudy, "cloudy"),
        (WeatherCondition.Rain, "rain"),
        (WeatherCondition.Snow, "snow"),
        (WeatherCondition.Storm, "storm"),
    };

    public static string Describe(LandscapeScanResult scan)
    {
        if (scan.Pictures.Count == 0)
            return "No pictures yet — add files named like rain-night-01.jpg";

        var missing = new List<string>();
        foreach (var (cond, word) in WeatherFileWord)
        {
            foreach (var t in new[] { SceneTime.Day, SceneTime.Night })
            {
                bool has = scan.Pictures.Any(p => p.Weather == cond && (p.Time == null || p.Time == t));
                if (!has) missing.Add($"{word} {t.ToString().ToLowerInvariant()}");
            }
        }

        string summary = $"{scan.Pictures.Count} picture{(scan.Pictures.Count == 1 ? "" : "s")} found";
        if (missing.Count > 0) summary += " — missing: " + string.Join(", ", missing);
        return summary;
    }
}
