namespace ClockScreensaver;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.ThreadException += (_, e) => Logger.Log($"Application.ThreadException: {e.Exception.GetType().Name}: {e.Exception.Message}\n{e.Exception.StackTrace}");
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Logger.Log($"AppDomain.UnhandledException: {e.ExceptionObject}");
        FontManager.Load();

        Logger.Log($"ClockScreensaver v{BuildInfo.Version} ({BuildInfo.GitHash}, built {BuildInfo.BuiltAt})");
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

        if (mode == "--screenshot-settings") { ScreenshotSettings(args.Length > 1 ? args[1] : null); return; }

        if (mode == "/render" && args.Length >= 3)
        {
            RenderFrame(args[1], args[2]);
            return;
        }

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

    // Headless single-frame renderer for automated verification:
    //   ClockScreensaver.scr /render <settings.json> <out.png>
    static void RenderFrame(string settingsPath, string outPath)
    {
        if (!File.Exists(settingsPath))
        {
            Console.Error.WriteLine($"Settings file not found: {settingsPath}");
            Environment.ExitCode = 1;
            return;
        }
        var settings = Settings.LoadFrom(settingsPath);

        const int W = 1920, H = 1080;
        using var fx = new EffectsRenderer();

        WeatherCondition cond = settings.PreviewCondition switch
        {
            PreviewCondition.Clear        => WeatherCondition.Clear,
            PreviewCondition.PartlyCloudy => WeatherCondition.PartlyCloudy,
            PreviewCondition.Cloudy       => WeatherCondition.Cloudy,
            PreviewCondition.Rain         => WeatherCondition.Rain,
            PreviewCondition.Snow         => WeatherCondition.Snow,
            PreviewCondition.Storm        => WeatherCondition.Storm,
            _                             => WeatherCondition.Clear,
        };
        bool isNight = settings.TimeOfDay == TimeOfDay.Night ||
                       (settings.TimeOfDay == TimeOfDay.Auto && (DateTime.Now.Hour >= 20 || DateTime.Now.Hour < 6));
        var weather = new WeatherResult
        {
            Condition = cond, TempC = 14f,
            Sunrise = DateTime.Today.AddHours(6),
            Sunset  = DateTime.Today.AddHours(20),
            FetchedAt = DateTime.Now,
        };

        fx.Build(W, H, settings.EffectsOn ? cond : null, isNight, settings.EffectsStrength / 100f,
            settings.Position, settings, 2.0);

        using var bmp = new Bitmap(W, H);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Black);
            fx.Draw(g, 2.0, 0.033f, W, H, false);
            float opacity = settings.Brightness / 100f;
            ClockRenderer.Draw(g, settings, settings.WeatherOn ? weather : null, new Rectangle(0, 0, W, H), opacity);
        }

        var fullOut = Path.GetFullPath(outPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullOut)!);
        bmp.Save(fullOut, System.Drawing.Imaging.ImageFormat.Png);
        Logger.Log($"/render: settings={settingsPath} → {fullOut}");
        Console.WriteLine($"Rendered {fullOut}");
    }

    static void ScreenshotSettings(string? outDirOverride)
    {
        var outDir = outDirOverride ?? Path.Combine(Settings.DataDir, "screenshots");
        Logger.Log($"ScreenshotSettings: outDir={outDir}");
        Directory.CreateDirectory(outDir);
        Logger.Log($"ScreenshotSettings: directory created/exists={Directory.Exists(outDir)}");

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
        form.Shown += (_, _) => { Logger.Log("ScreenshotSettings: form Shown"); form.BeginInvoke(CaptureNext); };
        Logger.Log("ScreenshotSettings: calling Application.Run");
        Application.Run(form);
        Logger.Log($"ScreenshotSettings: done, saved to {outDir}");
        return;

        void CaptureNext()
        {
            try
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
            Logger.Log($"ScreenshotSettings: saved {path}");
            tab++;
            form.BeginInvoke(CaptureNext);
            }
            catch (Exception ex)
            {
                Logger.Log($"ScreenshotSettings: CaptureNext EXCEPTION: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                form.Close();
            }
        }
    }
}
