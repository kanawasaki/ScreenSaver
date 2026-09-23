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

        Logger.Log($"Launched from: {AppDomain.CurrentDomain.BaseDirectory}");
        Logger.Log($"Args ({args.Length}): [{string.Join("] [", args)}]");

        // Parse the mode and optional HWND from Windows screensaver argument forms:
        //   /s              — run screensaver
        //   /c              — settings dialog
        //   /c:HWND         — settings dialog (parent HWND ignored, same dialog)
        //   /p HWND         — preview (HWND as separate arg)
        //   /p:HWND         — preview (HWND embedded after colon)
        // Also handles -p, -c, /P, /C (case-insensitive).
        string rawFirst = args.Length > 0 ? args[0].Trim() : "/s";
        string mode;
        string hwndStr;

        int colon = rawFirst.IndexOf(':');
        if (colon >= 0)
        {
            mode    = rawFirst[..colon].ToLowerInvariant();  // "/p" or "/c"
            hwndStr = rawFirst[(colon + 1)..].Trim();        // "12345"
        }
        else
        {
            mode    = rawFirst.ToLowerInvariant();
            hwndStr = args.Length > 1 ? args[1].Trim() : "";
        }

        Logger.Log($"Mode resolved: \"{mode}\", hwndStr: \"{hwndStr}\"");

        if (mode == "--screenshot-settings") { ScreenshotSettings(); return; }

        if (mode is "/p" or "-p")
        {
            if (long.TryParse(hwndStr, out long hwndLong) && hwndLong != 0)
            {
                Logger.Log($"Preview mode: HWND={hwndLong}");
                var settings = Settings.Load();
                var form = ScreensaverForm.CreatePreview(new IntPtr(hwndLong), settings);
                form.FormClosed += (_, _) => Application.Exit();
                Application.Run();
            }
            else
            {
                Logger.Log($"Preview: no valid HWND, falling through to screensaver");
                RunScreensaver();
            }
            return;
        }

        if (mode is "/c" or "-c")
        {
            Logger.Log("Settings mode");
            var settings = Settings.Load();
            using var form = new SettingsForm(settings);
            form.ShowDialog();
            return;
        }

        // /s, no args, or anything else → run screensaver
        Logger.Log("Screensaver mode");
        RunScreensaver();
    }

    static void RunScreensaver()
    {
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

    static void ScreenshotSettings()
    {
        var outDir = Path.Combine(Settings.DataDir, "screenshots");
        Directory.CreateDirectory(outDir);

        var settings = Settings.Load();
        settings.WeatherOn        = true;
        settings.ShowDate         = true;
        settings.DateFmt          = DateFormat.Short;
        settings.EffectsOn        = true;
        settings.PreviewCondition = PreviewCondition.Clear;
        settings.TimeOfDay        = TimeOfDay.Day;
        settings.Brightness       = 100;

        var form = new SettingsForm(settings);
        int tab = 0;
        string[] tabNames = { "clock", "weather", "picture", "effects" };

        form.TopMost = true;
        form.Shown += (_, _) => form.BeginInvoke(CaptureNext);
        Application.Run(form);
        Console.WriteLine($"Screenshots saved to: {outDir}");
        return;

        void CaptureNext()
        {
            if (tab >= tabNames.Length) { form.Close(); return; }
            form.SwitchTab(tab);
            Application.DoEvents();
            System.Threading.Thread.Sleep(500);
            Application.DoEvents();

            using var bmp = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
            form.DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height));

            string path = Path.Combine(outDir, $"tab{tab}_{tabNames[tab]}.png");
            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            Console.WriteLine($"  Saved {path}");
            tab++;
            form.BeginInvoke(CaptureNext);
        }
    }
}
