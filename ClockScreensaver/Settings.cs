using Microsoft.Win32;

namespace ClockScreensaver;

public enum ClockPosition { BottomRight, BottomLeft, TopLeft, TopRight, Center }
public enum ClockFont { PlexMono, SpaceGrotesk, Fraunces, MajorMono, Syne, System }
public enum ClockWeight { Light, Bold }
public enum PictureFit { Cover, Contain }
public enum TimeOfDay { Auto, Day, Night }
public enum WeatherUnit { C, F }
public enum PreviewCondition { Auto, Clear, PartlyCloudy, Cloudy, Rain, Snow, Storm }

public class Settings
{
    private const string RegKey = @"Software\ClockScreensaver";

    // Clock
    public ClockPosition Position { get; set; } = ClockPosition.BottomRight;
    public ClockFont Font { get; set; } = ClockFont.PlexMono;
    public ClockWeight Weight { get; set; } = ClockWeight.Light;
    public int Size { get; set; } = 48;
    public int Brightness { get; set; } = 80;
    public int Margin { get; set; } = 40;
    public bool Hours24 { get; set; } = true;
    public bool ShowSeconds { get; set; } = false;
    public bool ShowDate { get; set; } = false;
    public bool Rotate { get; set; } = false;

    // Weather
    public bool WeatherOn { get; set; } = true;
    public WeatherUnit Unit { get; set; } = WeatherUnit.C;
    public bool ShowConditionName { get; set; } = false;
    public bool LocationAuto { get; set; } = true;
    public string CityName { get; set; } = "";
    public PreviewCondition PreviewCondition { get; set; } = PreviewCondition.Auto;

    // Picture
    public string PicturePath { get; set; } = "";
    public PictureFit PictureFit { get; set; } = PictureFit.Cover;
    public int PictureBrightness { get; set; } = 50;

    // Effects
    public bool EffectsOn { get; set; } = true;
    public int EffectsStrength { get; set; } = 70;
    public TimeOfDay TimeOfDay { get; set; } = TimeOfDay.Auto;

    public static Settings Load()
    {
        var s = new Settings();
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegKey);
            if (key == null) return s;

            s.Position = Enum<ClockPosition>(key, "Position", s.Position);
            s.Font = Enum<ClockFont>(key, "Font", s.Font);
            s.Weight = Enum<ClockWeight>(key, "Weight", s.Weight);
            s.Size = Int(key, "Size", s.Size);
            s.Brightness = Int(key, "Brightness", s.Brightness);
            s.Margin = Int(key, "Margin", s.Margin);
            s.Hours24 = Bool(key, "Hours24", s.Hours24);
            s.ShowSeconds = Bool(key, "ShowSeconds", s.ShowSeconds);
            s.ShowDate = Bool(key, "ShowDate", s.ShowDate);
            s.Rotate = Bool(key, "Rotate", s.Rotate);

            s.WeatherOn = Bool(key, "WeatherOn", s.WeatherOn);
            s.Unit = Enum<WeatherUnit>(key, "Unit", s.Unit);
            s.ShowConditionName = Bool(key, "ShowConditionName", s.ShowConditionName);
            s.LocationAuto = Bool(key, "LocationAuto", s.LocationAuto);
            s.CityName = Str(key, "CityName", s.CityName);
            s.PreviewCondition = Enum<PreviewCondition>(key, "PreviewCondition", s.PreviewCondition);

            s.PicturePath = Str(key, "PicturePath", s.PicturePath);
            s.PictureFit = Enum<PictureFit>(key, "PictureFit", s.PictureFit);
            s.PictureBrightness = Int(key, "PictureBrightness", s.PictureBrightness);

            s.EffectsOn = Bool(key, "EffectsOn", s.EffectsOn);
            s.EffectsStrength = Int(key, "EffectsStrength", s.EffectsStrength);
            s.TimeOfDay = Enum<TimeOfDay>(key, "TimeOfDay", s.TimeOfDay);
        }
        catch { /* return defaults on any error */ }
        return s;
    }

    public void Save()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RegKey);
            key.SetValue("Position", Position.ToString());
            key.SetValue("Font", Font.ToString());
            key.SetValue("Weight", Weight.ToString());
            key.SetValue("Size", Size, RegistryValueKind.DWord);
            key.SetValue("Brightness", Brightness, RegistryValueKind.DWord);
            key.SetValue("Margin", Margin, RegistryValueKind.DWord);
            key.SetValue("Hours24", Hours24 ? 1 : 0, RegistryValueKind.DWord);
            key.SetValue("ShowSeconds", ShowSeconds ? 1 : 0, RegistryValueKind.DWord);
            key.SetValue("ShowDate", ShowDate ? 1 : 0, RegistryValueKind.DWord);
            key.SetValue("Rotate", Rotate ? 1 : 0, RegistryValueKind.DWord);

            key.SetValue("WeatherOn", WeatherOn ? 1 : 0, RegistryValueKind.DWord);
            key.SetValue("Unit", Unit.ToString());
            key.SetValue("ShowConditionName", ShowConditionName ? 1 : 0, RegistryValueKind.DWord);
            key.SetValue("LocationAuto", LocationAuto ? 1 : 0, RegistryValueKind.DWord);
            key.SetValue("CityName", CityName);
            key.SetValue("PreviewCondition", PreviewCondition.ToString());

            key.SetValue("PicturePath", PicturePath);
            key.SetValue("PictureFit", PictureFit.ToString());
            key.SetValue("PictureBrightness", PictureBrightness, RegistryValueKind.DWord);

            key.SetValue("EffectsOn", EffectsOn ? 1 : 0, RegistryValueKind.DWord);
            key.SetValue("EffectsStrength", EffectsStrength, RegistryValueKind.DWord);
            key.SetValue("TimeOfDay", TimeOfDay.ToString());
        }
        catch { }
    }

    private static int Int(RegistryKey k, string name, int def)
    {
        var v = k.GetValue(name);
        return v is int i ? i : def;
    }
    private static bool Bool(RegistryKey k, string name, bool def)
    {
        var v = k.GetValue(name);
        return v is int i ? i != 0 : def;
    }
    private static string Str(RegistryKey k, string name, string def)
    {
        var v = k.GetValue(name);
        return v is string s ? s : def;
    }
    private static T Enum<T>(RegistryKey k, string name, T def) where T : struct
    {
        var v = k.GetValue(name);
        if (v is string s && System.Enum.TryParse<T>(s, out var result)) return result;
        return def;
    }
}
