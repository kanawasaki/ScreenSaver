using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClockScreensaver;

public enum ClockPosition { BottomRight, BottomLeft, TopLeft, TopRight, Center }
public enum ClockFont { PlexMono, SpaceGrotesk, Fraunces, MajorMono, Syne, System }
public enum ClockWeight { Light, Bold }
public enum PictureFit { Cover, Contain }
public enum TimeOfDay { Auto, Day, Night }
public enum WeatherUnit { C, F }
public enum PreviewCondition { Auto, Clear, PartlyCloudy, Cloudy, Rain, Snow, Storm }
public enum DateFormat { Short, Long, DayMonth, Numeric }

public class Settings
{
    // Storage paths — %LOCALAPPDATA%\ClockScreensaver\
    public static readonly string DataDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClockScreensaver");
    private static readonly string SettingsFile = Path.Combine(DataDir, "settings.json");
    public static readonly string PictureDir = Path.Combine(DataDir, "picture");

    // Clock
    public ClockPosition Position   { get; set; } = ClockPosition.BottomRight;
    public ClockFont     Font       { get; set; } = ClockFont.PlexMono;
    public ClockWeight   Weight     { get; set; } = ClockWeight.Light;
    public int  Size        { get; set; } = 48;
    public int  Brightness  { get; set; } = 80;
    public int  Margin      { get; set; } = 40;
    public bool Hours24     { get; set; } = true;
    public bool ShowSeconds { get; set; } = false;
    public bool ShowDate    { get; set; } = false;
    public bool Rotate      { get; set; } = false;
    public DateFormat DateFmt { get; set; } = DateFormat.Short;

    // Weather
    public bool   WeatherOn         { get; set; } = true;
    public WeatherUnit Unit          { get; set; } = WeatherUnit.C;
    public bool   ShowConditionName  { get; set; } = false;
    public bool   LocationAuto       { get; set; } = true;
    public string CityName           { get; set; } = "";
    public PreviewCondition PreviewCondition { get; set; } = PreviewCondition.Auto;

    // Picture
    public string    PicturePath       { get; set; } = "";
    public PictureFit PictureFit       { get; set; } = PictureFit.Cover;
    public int       PictureBrightness { get; set; } = 50;

    // Effects
    public bool      EffectsOn       { get; set; } = true;
    public int       EffectsStrength  { get; set; } = 70;
    public TimeOfDay TimeOfDay        { get; set; } = TimeOfDay.Auto;

    private static readonly JsonSerializerOptions _opts = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static Settings Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                var json = File.ReadAllText(SettingsFile);
                var s = JsonSerializer.Deserialize<Settings>(json, _opts);
                if (s != null)
                {
                    Logger.Log($"Settings loaded from {SettingsFile}");
                    s.RepairPicturePath();
                    return s;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Log($"Settings.Load error: {ex.GetType().Name}: {ex.Message}");
        }
        Logger.Log("Using default settings");
        return new Settings();
    }

    // Older builds saved only the file name; recover by using the newest file in PictureDir.
    private void RepairPicturePath()
    {
        if (string.IsNullOrEmpty(PicturePath)) return;
        if (Path.IsPathRooted(PicturePath) && File.Exists(PicturePath)) return;

        Logger.Log($"PicturePath \"{PicturePath}\" is not a full path or the file is missing");
        try
        {
            var newest = Directory.Exists(PictureDir)
                ? new DirectoryInfo(PictureDir).GetFiles().OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault()
                : null;
            if (newest != null)
            {
                PicturePath = newest.FullName;
                Logger.Log($"PicturePath recovered → {PicturePath}");
            }
            else Logger.Log($"No picture found in {PictureDir}");
        }
        catch (Exception ex)
        {
            Logger.Log($"PicturePath recovery failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    // Returns null on success, error message on failure.
    public string? TrySave()
    {
        try
        {
            Directory.CreateDirectory(DataDir);
            var json = JsonSerializer.Serialize(this, _opts);
            File.WriteAllText(SettingsFile, json);
            Logger.Log($"Settings saved → {SettingsFile}");
            return null;
        }
        catch (Exception ex)
        {
            var msg = $"{ex.GetType().Name}: {ex.Message}";
            Logger.Log($"Settings.Save FAILED: {msg}");
            return msg;
        }
    }

    public void Save() => TrySave();
}
