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

    private readonly EffectsRenderer _fx = new();
    private System.Windows.Forms.Timer _fxTimer = new();
    private System.Windows.Forms.Timer _rotateTimer = new();
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

    public ScreensaverForm(Settings settings, WeatherService? weather, Screen screen, bool isPrimary, bool isPreview = false)
    {
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
        // Cursor is hidden in WndProc (WinForms has no Cursors.None).
        // The Windows settings preview is drawn by PreviewWindow, not by this form.
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);

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

    private SceneState CurrentScene() => SceneState.Compute(_settings, _weather?.Current);

    private void RebuildScene(bool force)
    {
        if (Width <= 0 || Height <= 0) return;

        var sc = CurrentScene();
        Logger.Log($"RebuildScene: effects={sc.EffectsCondition}, picture={sc.PictureWeather}, isNight={sc.IsNight}, " +
                   $"sceneTime={sc.Time}, season={sc.Season}, effectsOn={_settings.EffectsOn}, previewCond={_settings.PreviewCondition}");
        float strength = _settings.EffectsStrength / 100f;
        _fx.Build(Width, Height, sc.EffectsCondition, sc.IsNight, strength, _settings.Position, _settings, _tSec,
                  sc.Time, sc.Season, sc.PictureWeather);
        _buffer = null; // force redraw
        Invalidate();
    }

    // Re-evaluates just the matched landscape picture (e.g. a dawn/dusk window elapsing)
    // without resetting particle positions the way a full RebuildScene would.
    private void RefreshLandscapeBackground()
    {
        if (Width <= 0 || Height <= 0) return;
        var sc = CurrentScene();
        _fx.RebuildBackground(_settings, Width, Height, sc.EffectsCondition, sc.IsNight, sc.Time, sc.Season, _tSec,
                              sc.PictureWeather);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        int w = Width, h = Height;
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
                           ?? SceneState.DefaultIsNight(_settings.TimeOfDay);

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
        // Only screensaver windows: "Preview full screen" in Settings must not close
        // the Settings dialog itself when the preview is dismissed.
        foreach (var f in Application.OpenForms.OfType<ScreensaverForm>().ToArray())
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
            _landscapeTimer.Dispose();
            _fx.Dispose(); _buffer?.Dispose();
        }
        base.Dispose(disposing);
    }
}
