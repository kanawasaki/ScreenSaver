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
            string? sceneTimeArg = args.Length >= 4 ? args[3] : null;
            string? seasonArg    = args.Length >= 5 ? args[4] : null;
            RenderFrame(args[1], args[2], sceneTimeArg, seasonArg);
            return;
        }

        // Headless test hook for the background cross-fade, independent of the matching logic:
        //   ClockScreensaver.scr /testfade <settingsA.json> <settingsB.json> <outDir>
        if (mode == "/testfade" && args.Length >= 4)
        {
            TestFade(args[1], args[2], args[3]);
            return;
        }

        // Headless test hook for the landscape-matching algorithm, independent of rendering.
        // Writes results to a file rather than stdout — this is a WinExe with no attached
        // console, so Console output is silently swallowed when run from a terminal.
        //   ClockScreensaver.scr /testpick <folder> <weather> <time> <season> <outFile> [count]
        if (mode == "/testpick" && args.Length >= 6)
        {
            int count = args.Length >= 7 && int.TryParse(args[6], out var c) ? c : 10;
            TestPick(args[1], args[2], args[3], args[4], args[5], count);
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
    //   ClockScreensaver.scr /render <settings.json> <out.png> [sceneTime] [season]
    // sceneTime/season are test-only overrides (Day|Night|Dawn|Dusk, Spring|Summer|Autumn|Winter)
    // so landscape-matching fallback tiers can be exercised deterministically, independent of
    // the real wall-clock time and date.
    static void RenderFrame(string settingsPath, string outPath, string? sceneTimeArg = null, string? seasonArg = null)
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

        SceneTime sceneTime = Enum.TryParse<SceneTime>(sceneTimeArg, true, out var stOverride)
            ? stOverride : weather.GetSceneTime(settings.TimeOfDay);
        Season season = Enum.TryParse<Season>(seasonArg, true, out var seasonOverride)
            ? seasonOverride : SeasonCalc.GetSeason(DateTime.Now, 0f);

        fx.Build(W, H, settings.EffectsOn ? cond : null, isNight, settings.EffectsStrength / 100f,
            settings.Position, settings, 2.0, sceneTime, season);

        using var bmp = new Bitmap(W, H);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Black);
            fx.Draw(g, 2.0, 0.033f, W, H, false);
            float opacity = settings.Brightness / 100f;
            ClockRenderer.Draw(g, settings, settings.WeatherOn ? weather : null, new Rectangle(0, 0, W, H), opacity, fx.HasBackground);
        }

        var fullOut = Path.GetFullPath(outPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullOut)!);
        bmp.Save(fullOut, System.Drawing.Imaging.ImageFormat.Png);
        Logger.Log($"/render: settings={settingsPath} → {fullOut}");
        Console.WriteLine($"Rendered {fullOut}");
    }

    // Drives EffectsRenderer directly (no window) to confirm the ~3s cross-fade blends two
    // backgrounds rather than hard-cutting between them.
    static void TestFade(string settingsAPath, string settingsBPath, string outDir)
    {
        var sa = Settings.LoadFrom(settingsAPath);
        var sb = Settings.LoadFrom(settingsBPath);
        const int W = 640, H = 360;
        using var fx = new EffectsRenderer();
        var fullOutDir = Path.GetFullPath(outDir);
        Directory.CreateDirectory(fullOutDir);

        fx.Build(W, H, null, false, 0f, sa.Position, sa, 0.0, SceneTime.Day, Season.Spring);
        SaveFxFrame(fx, 0.0, W, H, Path.Combine(fullOutDir, "frame_a_initial.png"));

        const double fadeStartT = 10.0;
        fx.Build(W, H, null, false, 0f, sb.Position, sb, fadeStartT, SceneTime.Day, Season.Spring);

        foreach (var dt in new[] { 0.0, 0.75, 1.5, 2.25, 3.0, 4.0 })
            SaveFxFrame(fx, fadeStartT + dt, W, H, Path.Combine(fullOutDir, $"frame_b_t{dt:0.00}.png"));

        Logger.Log($"/testfade: wrote frames to {fullOutDir}");
    }

    static void SaveFxFrame(EffectsRenderer fx, double t, int w, int h, string path)
    {
        using var bmp = new Bitmap(w, h);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.Black);
            fx.Draw(g, t, 0.033f, w, h, false);
        }
        bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }

    static void TestPick(string folder, string weatherArg, string timeArg, string seasonArg, string outFile, int count)
    {
        var lines = new List<string>();
        if (!Enum.TryParse<WeatherCondition>(weatherArg, true, out var weather))
            lines.Add($"ERROR: bad weather \"{weatherArg}\"");
        if (!Enum.TryParse<SceneTime>(timeArg, true, out var time))
            lines.Add($"ERROR: bad time \"{timeArg}\"");
        if (!Enum.TryParse<Season>(seasonArg, true, out var season))
            lines.Add($"ERROR: bad season \"{seasonArg}\"");

        if (lines.Count == 0)
        {
            var lib = new LandscapeLibrary();
            var scan = lib.EnsureScanned(folder);
            lines.Add($"Scanned \"{folder}\": {scan.Pictures.Count} picture(s), {scan.IgnoredFiles.Count} ignored");
            lines.Add(LandscapeSummary.Describe(scan));

            var rng = new Random();
            for (int i = 0; i < count; i++)
            {
                var pick = lib.Pick(weather, time, season, rng);
                lines.Add(pick != null ? Path.GetFileName(pick.Path) : "(none)");
            }
        }

        var fullOut = Path.GetFullPath(outFile);
        Directory.CreateDirectory(Path.GetDirectoryName(fullOut)!);
        File.WriteAllLines(fullOut, lines);
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
