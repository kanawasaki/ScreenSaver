using System.Runtime.InteropServices;

namespace ClockScreensaver;

// Draws the screensaver into the small monitor in Windows' Screen Saver Settings
// dialog (launched as "/p <hwnd>").
//
// The old preview reused the full-screen Form and turned it into a child window.
// A Form keeps top-level window behaviour (default positioning, minimum sizes,
// activation, built-in double buffering) that misbehaves when it is a WS_CHILD of a
// window that belongs to another process, and it stayed blank. This is a plain child
// window instead: no Form, no layout, it just paints a finished bitmap.
internal sealed class PreviewWindow : NativeWindow, IDisposable
{
    private const int WS_CHILD = 0x40000000, WS_VISIBLE = 0x10000000;
    private const int WS_CLIPSIBLINGS = 0x04000000, WS_CLIPCHILDREN = 0x02000000;
    private const int WM_PAINT = 0x000F, WM_ERASEBKGND = 0x0014, WM_DESTROY = 0x0002;

    // The preview is rendered at this width and scaled down, with the clock size and
    // margin scaled by the same factor, so it looks like a miniature of the real screen.
    private const int VirtualWidth = 640;

    private readonly IntPtr _parent;
    private readonly Settings _settings;
    private readonly WeatherService _weather; // cached weather only; the preview never goes online
    private readonly EffectsRenderer _fx = new();
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 100 };
    private int _w, _h, _vw, _vh;
    private double _t;
    private long _lastTick = Environment.TickCount64;
    private Bitmap? _frame;
    private bool _loggedPaint;

    public static void Run(IntPtr parent, Settings settings)
    {
        using var win = new PreviewWindow(parent, settings);
        if (win.Handle == IntPtr.Zero) return;
        Application.Run();
    }

    private PreviewWindow(IntPtr parent, Settings settings)
    {
        _parent = parent;

        var screen = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);
        _vw = VirtualWidth;
        _vh = Math.Max(1, (int)Math.Round(VirtualWidth * (double)screen.Height / screen.Width));
        float f = (float)VirtualWidth / screen.Width;

        _settings = settings.Clone();
        _settings.Size   = Math.Max(8, (int)Math.Round(settings.Size * f));
        _settings.Margin = Math.Max(2, (int)Math.Round(settings.Margin * f));
        _settings.Rotate = false;
        _weather = new WeatherService(_settings);

        // Use the same DPI awareness as the window we're placed in. Mismatched awareness
        // between a child window and its parent is a known cause of blank or misplaced
        // child windows.
        try
        {
            var ctx = GetWindowDpiAwarenessContext(parent);
            if (ctx != IntPtr.Zero) SetThreadDpiAwarenessContext(ctx);
        }
        catch (Exception ex) { Logger.Log($"Preview: DPI context not set: {ex.Message}"); }

        GetClientRect(parent, out var r);
        _w = Math.Max(r.Right - r.Left, 1);
        _h = Math.Max(r.Bottom - r.Top, 1);

        var cp = new CreateParams
        {
            Caption = "ClockScreensaverPreview",
            Parent  = parent,
            Style   = WS_CHILD | WS_VISIBLE | WS_CLIPSIBLINGS | WS_CLIPCHILDREN,
            X = 0, Y = 0, Width = _w, Height = _h,
        };
        try
        {
            CreateHandle(cp);
        }
        catch (Exception ex)
        {
            Logger.Log($"Preview: CreateHandle FAILED: {ex.GetType().Name}: {ex.Message}");
            return;
        }
        Logger.Log($"Preview: child window {Handle} in parent {parent}, size {_w}x{_h}, rendering {_vw}x{_vh}");

        BuildScene();
        Render();
        Present();

        _timer.Tick += (_, _) => Tick();
        _timer.Start();
    }

    private void BuildScene()
    {
        var sc = SceneState.Compute(_settings, _weather.Current);
        _fx.Build(_vw, _vh, sc.EffectsCondition, sc.IsNight, _settings.EffectsStrength / 100f,
                  _settings.Position, _settings, _t, sc.Time, sc.Season, sc.PictureWeather);
    }

    private void Tick()
    {
        if (!IsWindow(_parent))
        {
            Logger.Log("Preview: parent window closed, exiting");
            Application.Exit();
            return;
        }

        long now = Environment.TickCount64;
        _t += Math.Min((now - _lastTick) / 1000.0, 0.25);
        _lastTick = now;

        GetClientRect(_parent, out var r);
        int w = Math.Max(r.Right - r.Left, 1), h = Math.Max(r.Bottom - r.Top, 1);
        if (w != _w || h != _h)
        {
            _w = w; _h = h;
            MoveWindow(Handle, 0, 0, w, h, true);
        }

        Render();
        Present();
    }

    private void Render()
    {
        using var virt = new Bitmap(_vw, _vh);
        using (var g = Graphics.FromImage(virt))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Black);
            _fx.Draw(g, _t, 0.033f, _vw, _vh, false); // same step as the full screen

            var sc = SceneState.Compute(_settings, _weather.Current);
            float opacity = (_settings.Brightness / 100f) * (sc.IsNight ? 0.75f : 1f);
            ClockRenderer.Draw(g, _settings, _weather.Current, new Rectangle(0, 0, _vw, _vh), opacity, _fx.HasBackground);
        }

        var frame = new Bitmap(_w, _h);
        using (var g = Graphics.FromImage(frame))
        {
            g.Clear(Color.Black);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
            float scale = Math.Min((float)_w / _vw, (float)_h / _vh);
            float dw = _vw * scale, dh = _vh * scale;
            g.DrawImage(virt, (_w - dw) / 2f, (_h - dh) / 2f, dw, dh);
        }
        _frame?.Dispose();
        _frame = frame;
    }

    private void Present()
    {
        if (_frame == null || Handle == IntPtr.Zero) return;
        try
        {
            using var g = Graphics.FromHwnd(Handle);
            g.DrawImageUnscaled(_frame, 0, 0);
            if (!_loggedPaint) { Logger.Log("Preview: first frame drawn"); _loggedPaint = true; }
        }
        catch (Exception ex)
        {
            Logger.Log($"Preview: draw failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    protected override void WndProc(ref Message m)
    {
        switch (m.Msg)
        {
            case WM_ERASEBKGND:
                m.Result = (IntPtr)1; // we paint everything ourselves
                return;
            case WM_PAINT:
                Present();
                ValidateRect(Handle, IntPtr.Zero);
                m.Result = IntPtr.Zero;
                return;
            case WM_DESTROY:
                _timer.Stop();
                base.WndProc(ref m);
                Application.Exit();
                return;
        }
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        _timer.Dispose();
        _fx.Dispose();
        _frame?.Dispose();
        _weather.Dispose();
        if (Handle != IntPtr.Zero) DestroyHandle();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hwnd, out RECT rect);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern bool MoveWindow(IntPtr hwnd, int x, int y, int w, int h, bool repaint);
    [DllImport("user32.dll")] private static extern bool ValidateRect(IntPtr hwnd, IntPtr rect);
    [DllImport("user32.dll")] private static extern IntPtr GetWindowDpiAwarenessContext(IntPtr hwnd);
    [DllImport("user32.dll")] private static extern IntPtr SetThreadDpiAwarenessContext(IntPtr ctx);
}
