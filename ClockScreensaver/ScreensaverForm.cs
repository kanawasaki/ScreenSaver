using System.Runtime.InteropServices;

namespace ClockScreensaver;

public class ScreensaverForm : Form
{
    [DllImport("user32.dll")] private static extern bool SystemParametersInfo(uint a, uint b, ref bool pv, uint f);
    private const uint SPI_GETCLIENTAREAANIMATION = 0x1042;

    private readonly Settings _settings;
    private readonly WeatherService? _weather;
    private readonly bool _isPrimary;
    private readonly bool _isPreview;
    private IntPtr _previewParentHwnd; // set before CreateHandle

    private readonly EffectsRenderer _fx = new();
    private System.Windows.Forms.Timer _fxTimer = new();
    private System.Windows.Forms.Timer _rotateTimer = new();
    private System.Windows.Forms.Timer _parentMonitor = new();
    private System.Windows.Forms.Timer _landscapeTimer = new();

    private Point _mouseOrigin;
    private bool _started = false;
    private double _tSec = 0;
    private long _lastTick = Environment.TickCount64;

    // Fade for rotation
    private float _clockOpacity = 1f;
    private bool _fading = false;
    private int _fadeDir = -1; // -1 = fade out, +1 = fade in

    private bool _reducedMotion = false;

    // Double-buffer bitmap
    private Bitmap? _buffer;
    private int _bufW, _bufH;

    // CreateParams is called when the window handle is first created.
    // For preview mode we need WS_CHILD so GDI paints within the parent pane.
    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            if (_isPreview && _previewParentHwnd != IntPtr.Zero)
            {
                cp.Style    = 0x56000000; // WS_VISIBLE | WS_CHILD | WS_CLIPSIBLINGS | WS_CLIPCHILDREN
                cp.ExStyle  = 0;
                cp.Parent   = _previewParentHwnd;
                cp.X        = 0;
                cp.Y        = 0;
            }
            return cp;
        }
    }

    public ScreensaverForm(Settings settings, WeatherService? weather, Screen screen, bool isPrimary, bool isPreview = false)
    {
        _previewParentHwnd = IntPtr.Zero; // must be set before Show() if preview
        _settings = settings;
        _weather = weather;
        _isPrimary = isPrimary;
        _isPreview = isPreview;

        // Check reduce motion
        bool anim = true;
        SystemParametersInfo(SPI_GETCLIENTAREAANIMATION, 0, ref anim, 0);
        _reducedMotion = !anim;

        // Window setup
        Text = "Clock Screensaver";
        BackColor = Color.Black;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        // Hide cursor (WinForms has no Cursors.None; override WndProc instead)
        // OptimizedDoubleBuffer is skipped for the preview: this Form is reparented as a
        // WS_CHILD of a foreign process's HWND, and the built-in double-buffer surface does
        // not composite reliably across that process boundary. We already do our own manual
        // buffering via _buffer below, so it isn't needed anyway.
        var styles = ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint;
        if (!isPreview) styles |= ControlStyles.OptimizedDoubleBuffer;
        SetStyle(styles, true);

        if (!isPreview)
        {
            Bounds = screen.Bounds;
            TopMost = true;
            WindowState = FormWindowState.Maximized;
        }

        // Weather updates → rebuild fx (guard against disposal)
        if (weather != null)
            weather.Updated += () => { if (IsHandleCreated && !IsDisposed) Invoke(() => RebuildScene(true)); };

        // Effects timer also drives the clock repaint (100ms is frequent enough for
        // 1-second clock resolution) — a separate clock timer at the same rate was pure
        // redundant repaint work.
        _fxTimer.Interval = 100; // ~10fps — ambient rain/snow/rays read as smooth at this rate and it keeps CPU use down
        _fxTimer.Tick += FxTick;

        // Rotation timer (10 minutes)
        if (settings.Rotate && isPrimary && !isPreview)
        {
            _rotateTimer.Interval = 10 * 60 * 1000;
            _rotateTimer.Tick += RotateTick;
            _rotateTimer.Start();
        }

        _fxTimer.Start();

        // Scene time (dawn/dusk/day/night) can change independently of weather polling —
        // re-evaluate the matched landscape picture periodically without disturbing particles.
        if (settings.PictureSource == PictureSource.MatchingLandscape && !isPreview)
        {
            _landscapeTimer.Interval = 60 * 1000;
            _landscapeTimer.Tick += (_, _) => RefreshLandscapeBackground();
            _landscapeTimer.Start();
        }

        if (weather != null && isPrimary)
            weather.Start();

        Load += (_, _) =>
        {
            RebuildScene(true);
            if (!isPreview) Activate();
        };
        Resize += (_, _) => { _buffer = null; RebuildScene(true); };
    }

    private void FxTick(object? s, EventArgs e)
    {
        long now = Environment.TickCount64;
        float dt = Math.Min((now - _lastTick) / 1000f, 0.05f);
        _lastTick = now;
        _tSec += dt;

        // Fade animation
        if (_fading)
        {
            _clockOpacity = Math.Clamp(_clockOpacity + _fadeDir * dt * 1.5f, 0f, 1f);
            if (_fadeDir == -1 && _clockOpacity <= 0)
            {
                // Rotate position
                var corners = new[] { ClockPosition.TopLeft, ClockPosition.TopRight, ClockPosition.BottomRight, ClockPosition.BottomLeft };
                int idx = Array.IndexOf(corners, _settings.Position);
                _settings.Position = corners[(idx + 1) % corners.Length];
                _settings.Save();
                _fadeDir = 1;
            }
            else if (_fadeDir == 1 && _clockOpacity >= 1)
            {
                _fading = false;
                _clockOpacity = 1f;
            }
        }
        Invalidate();
    }

    private void RotateTick(object? s, EventArgs e)
    {
        if (_reducedMotion)
        {
            var corners = new[] { ClockPosition.TopLeft, ClockPosition.TopRight, ClockPosition.BottomRight, ClockPosition.BottomLeft };
            int idx = Array.IndexOf(corners, _settings.Position);
            _settings.Position = corners[(idx + 1) % corners.Length];
            _settings.Save();
        }
        else
        {
            _fading = true;
            _fadeDir = -1;
        }
    }

    private (WeatherCondition? cond, bool isNight) ComputeConditions()
    {
        WeatherCondition? cond = null;
        bool isNight = false;

        if (_settings.EffectsOn)
        {
            // Determine effective condition
            WeatherCondition? liveCond = _weather?.Current?.Condition;
            if (_settings.PreviewCondition != PreviewCondition.Auto)
            {
                cond = _settings.PreviewCondition switch
                {
                    PreviewCondition.Clear       => WeatherCondition.Clear,
                    PreviewCondition.PartlyCloudy=> WeatherCondition.PartlyCloudy,
                    PreviewCondition.Cloudy      => WeatherCondition.Cloudy,
                    PreviewCondition.Rain        => WeatherCondition.Rain,
                    PreviewCondition.Snow        => WeatherCondition.Snow,
                    PreviewCondition.Storm       => WeatherCondition.Storm,
                    _                            => liveCond,
                };
            }
            else if (_settings.WeatherOn)
                cond = liveCond;

            isNight = _weather?.Current?.IsNight(_settings.TimeOfDay)
                      ?? DefaultIsNight(_settings.TimeOfDay);

            // Ambient effects (stars/rays) even when no weather data yet
            if (cond == null)
                cond = WeatherCondition.Clear;
        }

        return (cond, isNight);
    }

    // Picture matching uses its own finer-grained time-of-day (dawn/dusk) and season,
    // computed independently of whether ambient effects are enabled.
    private (SceneTime time, Season season) ComputeSceneContext()
    {
        var time = _weather?.Current?.GetSceneTime(_settings.TimeOfDay) ?? DefaultSceneTime(_settings.TimeOfDay);
        float lat = _weather?.Current?.Latitude ?? 0f;
        var season = SeasonCalc.GetSeason(DateTime.Now, lat);
        return (time, season);
    }

    private void RebuildScene(bool force)
    {
        if (Width <= 0 || Height <= 0) return;

        var (cond, isNight) = ComputeConditions();
        var (sceneTime, season) = ComputeSceneContext();

        Logger.Log($"RebuildScene: cond={cond}, isNight={isNight}, sceneTime={sceneTime}, season={season}, effects={_settings.EffectsOn}, previewCond={_settings.PreviewCondition}");
        float strength = _settings.EffectsStrength / 100f;
        _fx.Build(Width, Height, cond, isNight, strength, _settings.Position, _settings, _tSec, sceneTime, season);
        _buffer = null; // force redraw
        Invalidate();
    }

    // Re-evaluates just the matched landscape picture (e.g. a dawn/dusk window elapsing)
    // without resetting particle positions the way a full RebuildScene would.
    private void RefreshLandscapeBackground()
    {
        if (Width <= 0 || Height <= 0) return;
        var (cond, isNight) = ComputeConditions();
        var (sceneTime, season) = ComputeSceneContext();
        _fx.RebuildBackground(_settings, Width, Height, cond, isNight, sceneTime, season, _tSec);
    }

    private static bool DefaultIsNight(TimeOfDay tod)
    {
        if (tod == TimeOfDay.Day) return false;
        if (tod == TimeOfDay.Night) return true;
        int h = DateTime.Now.Hour;
        return h >= 20 || h < 6;
    }

    private static SceneTime DefaultSceneTime(TimeOfDay tod)
    {
        if (tod == TimeOfDay.Day) return SceneTime.Day;
        if (tod == TimeOfDay.Night) return SceneTime.Night;
        int h = DateTime.Now.Hour;
        return (h >= 20 || h < 6) ? SceneTime.Night : SceneTime.Day;
    }

    private static bool _loggedFirstPaint = false;
    protected override void OnPaint(PaintEventArgs e)
    {
        int w = Width, h = Height;
        if (_isPreview && !_loggedFirstPaint)
        {
            Logger.Log($"ScreensaverForm.OnPaint (preview): size={w}x{h}, isPrimary={_isPrimary}, handleCreated={IsHandleCreated}");
            _loggedFirstPaint = true;
        }
        if (w <= 0 || h <= 0) return;

        // Maintain buffer
        if (_buffer == null || _bufW != w || _bufH != h)
        {
            _buffer?.Dispose();
            _buffer = new Bitmap(w, h);
            _bufW = w; _bufH = h;
        }

        using (var g = Graphics.FromImage(_buffer))
        {
            g.Clear(Color.Black);

            bool isNight = _weather?.Current?.IsNight(_settings.TimeOfDay)
                           ?? DefaultIsNight(_settings.TimeOfDay);

            _fx.Draw(g, _tSec, 0.033f, w, h, _reducedMotion);

            if (_isPrimary)
            {
                float opacity = (_settings.Brightness / 100f)
                    * (_settings.EffectsOn && isNight ? 0.75f : 1f)
                    * _clockOpacity;
                ClockRenderer.Draw(g, _settings, _weather?.Current, new Rectangle(0, 0, w, h), opacity, _fx.HasBackground);
            }
        }

        e.Graphics.DrawImageUnscaled(_buffer, 0, 0);
    }

    // --- Input handling (ignored in preview mode) ---
    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_isPreview) return;
        if (!_started) { _mouseOrigin = e.Location; _started = true; return; }
        if (Math.Abs(e.X - _mouseOrigin.X) > 5 || Math.Abs(e.Y - _mouseOrigin.Y) > 5)
            ExitAll();
    }
    protected override void OnMouseDown(MouseEventArgs e) { if (!_isPreview) ExitAll(); }
    protected override void OnKeyDown(KeyEventArgs e) { if (!_isPreview) ExitAll(); }

    private void ExitAll()
    {
        foreach (Form f in Application.OpenForms.Cast<Form>().ToArray())
            f.Close();
    }

    [DllImport("user32.dll")] private static extern IntPtr LoadCursor(IntPtr hInst, IntPtr id);
    [DllImport("user32.dll")] private static extern IntPtr SetCursor(IntPtr hCursor);
    private const int WM_SETCURSOR = 0x0020;

    protected override void WndProc(ref Message m)
    {
        if (!_isPreview && m.Msg == WM_SETCURSOR)
        {
            SetCursor(IntPtr.Zero); // hide cursor
            m.Result = IntPtr.Zero;
            return;
        }
        base.WndProc(ref m);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _fxTimer.Dispose(); _rotateTimer.Dispose();
            _parentMonitor.Dispose(); _landscapeTimer.Dispose();
            _fx.Dispose(); _buffer?.Dispose();
        }
        base.Dispose(disposing);
    }

    // Create a child window inside the Windows preview pane.
    // No weather service — preview should be low-CPU with no network requests.
    public static ScreensaverForm CreatePreview(IntPtr hwnd, Settings settings)
    {
        var rect = new RECT();
        GetClientRect(hwnd, ref rect);
        int w = Math.Max(rect.Right - rect.Left, 1);
        int h = Math.Max(rect.Bottom - rect.Top, 1);
        Logger.Log($"CreatePreview: parentHwnd={hwnd}, clientRect={w}x{h}");

        var form = new ScreensaverForm(settings, null, Screen.PrimaryScreen!, true, true);
        // _previewParentHwnd must be set before Show() triggers CreateHandle → CreateParams
        form._previewParentHwnd = hwnd;
        form.Size = new Size(w, h);

        // Exit if the parent pane is destroyed (user navigates away in Screen Saver Settings)
        form._parentMonitor.Interval = 500;
        form._parentMonitor.Tick += (_, _) =>
        {
            if (!IsWindow(hwnd))
            {
                Logger.Log("Preview parent closed — exiting");
                Application.Exit();
            }
        };
        form._parentMonitor.Start();

        form.Show();
        // The preview HWND is a sibling within a foreign process's monitor-bezel control;
        // newly created children aren't guaranteed to land on top of existing siblings there,
        // and if a sibling fully covers our rect we never receive a paintable region at all.
        BringWindowToTop(form.Handle);
        form.Invalidate();
        form.Update();
        Logger.Log($"Preview form shown, handleCreated={form.IsHandleCreated}, visible={form.Visible}");
        return form;
    }

    [DllImport("user32.dll")] private static extern IntPtr SetParent(IntPtr child, IntPtr parent);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hwnd, ref RECT rect);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool BringWindowToTop(IntPtr hwnd);
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }
}
