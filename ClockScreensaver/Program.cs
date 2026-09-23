namespace ClockScreensaver;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        FontManager.Load();

        string mode = args.Length > 0 ? args[0].ToLowerInvariant() : "/s";
        string extra = args.Length > 1 ? args[1] : "";

        // /s or no args → run screensaver
        // /c or /c:hwnd → settings dialog
        // /p hwnd → preview

        if (mode.StartsWith("/p") || mode.StartsWith("-p"))
        {
            // Preview mode: render into the settings dialog preview pane
            string hwndStr = extra.Length > 0 ? extra : mode.Length > 2 ? mode[2..].TrimStart(':') : "";
            if (long.TryParse(hwndStr, out long hwndLong) && hwndLong != 0)
            {
                var settings = Settings.Load();
                var weather = new WeatherService(settings);
                weather.Start();
                var previewForm = ScreensaverForm.CreatePreview(new IntPtr(hwndLong), settings, weather);
                previewForm.FormClosed += (_, _) => Application.Exit();
                Application.Run();
                weather.Dispose();
            }
            return;
        }

        if (mode.StartsWith("/c") || mode.StartsWith("-c"))
        {
            var settings = Settings.Load();
            using var form = new SettingsForm(settings);
            form.ShowDialog();
            return;
        }

        // /s → run on all screens
        var s = Settings.Load();
        var wx = new WeatherService(s);

        var forms = new List<ScreensaverForm>();
        foreach (var screen in Screen.AllScreens)
        {
            bool isPrimary = screen.Primary;
            var form = new ScreensaverForm(s, isPrimary ? wx : null, screen, isPrimary);
            forms.Add(form);
        }

        foreach (var f in forms) f.Show();
        Application.Run(forms[0]);

        wx.Dispose();
        foreach (var f in forms.Skip(1)) f.Dispose();
    }
}
