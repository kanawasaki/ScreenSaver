using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

namespace ClockScreensaver;

public class EffectsRenderer : IDisposable
{
    private readonly Random _rng = new();
    private List<Streak> _streaks = new();
    private List<Drop> _drops = new();
    private List<Flake> _flakes = new();
    private List<Star> _stars = new();
    private List<Ray> _rays = new();
    private RaySource? _raySource;

    private float _flashAlpha = 0;
    private double _nextFlash = 0;
    private WeatherCondition? _currentCond;
    private bool _isNight;
    private int _maxDrops;
    private float _strength;

    // Image with applied color filter
    private Bitmap? _filteredBg;
    private string? _lastPicPath;
    private WeatherCondition? _lastPicCond;
    private bool _lastPicNight;
    private int _lastPicBright;
    private PictureFit _lastPicFit;

    public void Build(int width, int height, WeatherCondition? cond, bool isNight, float strength,
                      ClockPosition clockPos, Settings settings, double tSec = 0)
    {
        _currentCond = cond;
        _isNight = isNight;
        _strength = strength;
        float k = strength;
        float area = (width * height) / (1280f * 800f);

        _streaks.Clear(); _drops.Clear(); _flakes.Clear(); _stars.Clear(); _rays.Clear();
        _maxDrops = 0;
        _flashAlpha = 0;

        if (cond == WeatherCondition.Rain || cond == WeatherCondition.Storm)
        {
            int n = (int)((cond == WeatherCondition.Storm ? 220 : 140) * k * area);
            for (int i = 0; i < n; i++) _streaks.Add(NewStreak(width, height, true));
            _maxDrops = (int)((cond == WeatherCondition.Storm ? 55 : 40) * k * MathF.Sqrt(area));
            for (int i = 0; i < _maxDrops * 0.6f; i++) _drops.Add(NewDrop(width, height, true));
        }

        if (cond == WeatherCondition.Snow)
        {
            int n = (int)(130 * k * area);
            for (int i = 0; i < n; i++) _flakes.Add(NewFlake(width, height, true));
        }

        if ((cond == WeatherCondition.Clear || cond == WeatherCondition.PartlyCloudy) && isNight)
        {
            int n = (int)((cond == WeatherCondition.Clear ? 110 : 45) * k * area);
            for (int i = 0; i < n; i++)
                _stars.Add(new Star(Rnd(0, width), Rnd(0, height * 0.75f),
                    Rnd(0.4f, 1.3f), Rnd(0.25f, 0.8f), Rnd(0.3f, 1.4f), Rnd(0, MathF.PI * 2)));
        }

        if ((cond == WeatherCondition.Clear || cond == WeatherCondition.PartlyCloudy) && !isNight)
        {
            bool fromLeft = clockPos != ClockPosition.TopLeft;
            float srcX = fromLeft ? -width * 0.05f : width * 1.05f;
            float srcY = -height * 0.08f;
            int dir = fromLeft ? 1 : -1;
            float power = cond == WeatherCondition.Clear ? 1f : 0.55f;
            int n = cond == WeatherCondition.Clear ? 7 : 4;
            _raySource = new RaySource(srcX, srcY, dir, power);
            for (int i = 0; i < n; i++)
                _rays.Add(new Ray(Rnd(12, 75), Rnd(2.5f, 6), Rnd(0.5f, 1), Rnd(0.05f, 0.15f), Rnd(0, MathF.PI * 2)));
        }
        else _raySource = null;

        if (cond == WeatherCondition.Storm)
            _nextFlash = tSec + Rnd(6, 14);

        RebuildBackground(settings, width, height, cond, isNight);
    }

    public void RebuildBackground(Settings settings, int width, int height,
                                   WeatherCondition? cond, bool isNight)
    {
        string path = settings.PicturePath;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            _filteredBg?.Dispose(); _filteredBg = null;
            _lastPicPath = null;
            return;
        }

        // Only rebuild if something changed
        if (path == _lastPicPath && cond == _lastPicCond && isNight == _lastPicNight
            && settings.PictureBrightness == _lastPicBright && settings.PictureFit == _lastPicFit
            && _filteredBg != null) return;

        _lastPicPath = path; _lastPicCond = cond; _lastPicNight = isNight;
        _lastPicBright = settings.PictureBrightness; _lastPicFit = settings.PictureFit;

        try
        {
            using var orig = Image.FromFile(path);
            var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using var gr = Graphics.FromImage(bmp);

            var attrs = new ImageAttributes();
            attrs.SetColorMatrix(BuildColorMatrix(cond, isNight, settings.PictureBrightness / 100f));

            // Scale to cover/contain
            Rectangle dest = settings.PictureFit == PictureFit.Cover
                ? CoverRect(orig.Size, new Size(width, height))
                : ContainRect(orig.Size, new Size(width, height));

            gr.DrawImage(orig, dest, 0, 0, orig.Width, orig.Height, GraphicsUnit.Pixel, attrs);
            _filteredBg?.Dispose();
            _filteredBg = bmp;
        }
        catch (Exception ex)
        {
            Logger.Log($"RebuildBackground FAILED for \"{path}\" ({width}x{height}): {ex.GetType().Name}: {ex.Message}");
            _filteredBg = null;
        }
    }

    private static ColorMatrix BuildColorMatrix(WeatherCondition? cond, bool isNight, float picBright)
    {
        // Start with brightness scaling
        float b = picBright; // 0..1

        float sat = 1f, brightMul = 1f, contrastAdj = 0f;

        if (cond.HasValue)
        {
            switch (cond.Value)
            {
                case WeatherCondition.Clear:        brightMul = 1.05f; sat = 1.12f; break;
                case WeatherCondition.PartlyCloudy: sat = 1.04f; break;
                case WeatherCondition.Cloudy:       brightMul = 0.92f; sat = 0.7f; break;
                case WeatherCondition.Rain:         brightMul = 0.85f; sat = 0.7f; contrastAdj = -0.05f; break;
                case WeatherCondition.Storm:        brightMul = 0.75f; sat = 0.6f; contrastAdj = 0.05f; break;
                case WeatherCondition.Snow:         brightMul = 1.02f; sat = 0.75f; break;
            }
        }

        if (isNight) { brightMul *= 0.5f; sat *= 0.7f; }

        float total = b * brightMul;

        // Build saturation + brightness color matrix
        // Luminance weights (BT.709)
        float lr = (1 - sat) * 0.2126f;
        float lg = (1 - sat) * 0.7152f;
        float lb = (1 - sat) * 0.0722f;

        // ColorMatrix row=input col=output, transform: [R',G',B',A',1] = [R,G,B,A,1] × M
        // Off-diagonal: each output component receives the input's luminance contribution
        float t = contrastAdj;
        return new ColorMatrix(new float[][]
        {
            new[] { (lr + sat) * total, lr * total,         lr * total,         0, 0 },
            new[] { lg * total,         (lg + sat) * total, lg * total,         0, 0 },
            new[] { lb * total,         lb * total,         (lb + sat)* total,  0, 0 },
            new[] { 0f,                 0f,                 0f,                 1f,0 },
            new[] { t,                  t,                  t,                  0, 1f},
        });
    }

    private static Rectangle CoverRect(Size img, Size screen)
    {
        float scale = MathF.Max((float)screen.Width / img.Width, (float)screen.Height / img.Height);
        int w = (int)(img.Width * scale), h = (int)(img.Height * scale);
        return new Rectangle((screen.Width - w) / 2, (screen.Height - h) / 2, w, h);
    }

    private static Rectangle ContainRect(Size img, Size screen)
    {
        float scale = MathF.Min((float)screen.Width / img.Width, (float)screen.Height / img.Height);
        int w = (int)(img.Width * scale), h = (int)(img.Height * scale);
        return new Rectangle((screen.Width - w) / 2, (screen.Height - h) / 2, w, h);
    }

    public void Draw(Graphics g, double tSec, float dt, int width, int height, bool reducedMotion)
    {
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Background image
        if (_filteredBg != null)
            g.DrawImageUnscaled(_filteredBg, 0, 0);

        if (reducedMotion)
        {
            // Static frame — draw stars and rays at a fixed phase
            DrawStars(g, tSec, 0, width, height, true);
            DrawRays(g, tSec, width, height);
            return;
        }

        DrawRays(g, tSec, width, height);
        DrawStars(g, tSec, dt, width, height, false);
        DrawStreaks(g, dt, width, height);
        DrawDrops(g, dt, width, height);
        DrawFlakes(g, tSec, dt, width, height);
        DrawLightning(g, tSec, dt, width, height);
    }

    private void DrawRays(Graphics g, double t, int width, int height)
    {
        if (_raySource == null || _rays.Count == 0) return;
        var R = _raySource;
        float len = MathF.Sqrt(width * width + height * height) * 1.15f;
        var state = g.Save();
        g.CompositingMode = CompositingMode.SourceOver;

        // Ambient glow
        float glowR = MathF.Max(width, height) * 0.55f;
        using var radialGlow = new SolidBrush(Color.FromArgb((int)(255 * 0.08f * _strength * R.Power), 255, 224, 170));
        g.FillEllipse(radialGlow, R.X - glowR, R.Y - glowR, glowR * 2, glowR * 2);

        foreach (var ray in _rays)
        {
            float ang = (ray.Angle + MathF.Sin((float)(t * ray.Speed + ray.Phase)) * 2.2f) * MathF.PI / 180f;
            float half = ray.Width * MathF.PI / 360f;
            float pulse = 0.75f + 0.25f * MathF.Sin((float)(t * ray.Speed * 2 + ray.Phase));
            float a1 = ang - half, a2 = ang + half;

            float ex = R.X + R.Dir * MathF.Cos(ang) * len;
            float ey = R.Y + MathF.Sin(ang) * len;

            int alpha1 = (int)(255 * 0.20f * _strength * ray.Alpha * pulse * R.Power);
            int alpha2 = (int)(255 * 0.05f * _strength * ray.Alpha * pulse * R.Power);
            if (alpha1 <= 0) continue;

            using var path = new GraphicsPath();
            path.AddLines(new PointF[]
            {
                new(R.X, R.Y),
                new(R.X + R.Dir * MathF.Cos(a1) * len, R.Y + MathF.Sin(a1) * len),
                new(R.X + R.Dir * MathF.Cos(a2) * len, R.Y + MathF.Sin(a2) * len),
            });
            path.CloseFigure();

            using var brush = new LinearGradientBrush(
                new PointF(R.X, R.Y), new PointF(ex, ey),
                Color.FromArgb(alpha1, 255, 236, 190),
                Color.FromArgb(0, 255, 236, 190));
            g.FillPath(brush, path);
        }

        g.Restore(state);
    }

    private readonly SolidBrush _starBrush = new(Color.White);
    private void DrawStars(Graphics g, double t, float dt, int width, int height, bool staticMode)
    {
        if (_stars.Count == 0) return;
        foreach (var st in _stars)
        {
            float a = st.Alpha * _strength * (0.55f + 0.45f * MathF.Sin((float)(t * st.Speed + st.Phase)));
            if (staticMode) a = st.Alpha * _strength * 0.7f;
            if (a <= 0) continue;
            _starBrush.Color = Color.FromArgb((int)(255 * a), 230, 236, 255);
            g.FillEllipse(_starBrush, st.X - st.Radius, st.Y - st.Radius, st.Radius * 2, st.Radius * 2);
        }
    }

    private void DrawStreaks(Graphics g, float dt, int width, int height)
    {
        if (_streaks.Count == 0) return;
        using var pen = new Pen(Color.White, 1f) { StartCap = LineCap.Round, EndCap = LineCap.Round };
        foreach (var d in _streaks)
        {
            d.Y += d.Speed * dt; d.X += d.Speed * 0.1f * dt;
            if (d.Y > height + 30) ResetStreak(d, width, height);
            int a = (int)(255 * d.Alpha * (0.4f + 0.6f * _strength));
            if (a <= 0) continue;
            pen.Color = Color.FromArgb(a, 205, 218, 235);
            g.DrawLine(pen, d.X, d.Y, d.X - d.Length * 0.1f, d.Y - d.Length);
        }
    }

    private readonly SolidBrush _dropBodyBrush = new(Color.White);
    private readonly Pen _dropRimPen = new(Color.White, 0.8f);
    private void DrawDrops(Graphics g, float dt, int width, int height)
    {
        if (_maxDrops == 0) return;
        if (dt > 0 && _drops.Count < _maxDrops && _rng.NextDouble() < dt * 6)
            _drops.Add(NewDrop(width, height, false));

        for (int i = _drops.Count - 1; i >= 0; i--)
        {
            var d = _drops[i];
            d.Age += dt;
            if (d.Slide > 0 && d.Age > d.Wait)
                d.Y += d.Slide * dt * (1 + MathF.Sin(d.Age * 3) * 0.4f);

            float fadeIn = MathF.Min(d.Age / 0.4f, 1);
            float fadeOut = MathF.Min((d.Life - d.Age) / 1.5f, 1);
            float a = MathF.Max(0, MathF.Min(fadeIn, fadeOut));
            if (d.Age > d.Life || d.Y > height + 10) { _drops.RemoveAt(i); continue; }

            float r = d.Radius;
            // Simple translucent drop
            _dropBodyBrush.Color = Color.FromArgb((int)(255 * 0.15f * a), 180, 200, 230);
            g.FillEllipse(_dropBodyBrush, d.X - r, d.Y - r * 1.12f, r * 2, r * 2.24f);
            _dropRimPen.Color = Color.FromArgb((int)(255 * 0.35f * a), 255, 255, 255);
            g.DrawArc(_dropRimPen, d.X - r * 0.85f, d.Y - r * 0.95f, r * 1.7f, r * 1.9f, 11, 92);
        }
    }

    private readonly SolidBrush _flakeBrush = new(Color.White);
    private void DrawFlakes(Graphics g, double t, float dt, int width, int height)
    {
        foreach (var f in _flakes)
        {
            f.Y += f.Speed * dt;
            f.X += MathF.Sin((float)(t * f.Sway + f.Phase)) * 18 * dt;
            if (f.Y > height + 10) ResetFlake(f, width, height);
            float a = f.Alpha * (0.4f + 0.6f * _strength);
            _flakeBrush.Color = Color.FromArgb((int)(255 * a), 255, 255, 255);
            g.FillEllipse(_flakeBrush, f.X - f.Radius, f.Y - f.Radius, f.Radius * 2, f.Radius * 2);
        }
    }

    private void DrawLightning(Graphics g, double t, float dt, int width, int height)
    {
        if (_currentCond != WeatherCondition.Storm) return;
        if (t > _nextFlash) { _flashAlpha = 1f; _nextFlash = t + Rnd(12, 28); }
        if (_flashAlpha > 0)
        {
            int a = (int)(255 * 0.14f * _strength * _flashAlpha);
            using var flash = new SolidBrush(Color.FromArgb(a, 215, 225, 255));
            g.FillRectangle(flash, 0, 0, width, height);
            _flashAlpha = MathF.Max(0, _flashAlpha - dt * 1.6f);
        }
    }

    // --- Factories ---
    private Streak NewStreak(int w, int h, bool anywhere) => new(
        Rnd(-w * 0.1f, w * 1.05f), anywhere ? Rnd(-h, h) : Rnd(-h * 0.3f, -20f),
        Rnd(12, 26), Rnd(750, 1150), Rnd(0.12f, 0.32f));

    private void ResetStreak(Streak d, int w, int h) =>
        (d.X, d.Y, d.Length, d.Speed, d.Alpha) =
        (Rnd(-w * 0.1f, w * 1.05f), Rnd(-h * 0.3f, -20f), Rnd(12, 26), Rnd(750, 1150), Rnd(0.12f, 0.32f));

    private Drop NewDrop(int w, int h, bool anywhere)
    {
        float r = Rnd(1.8f, 6.5f);
        return new Drop(Rnd(0, w), anywhere ? Rnd(0, h) : Rnd(-40, -5f), r,
            anywhere ? Rnd(0, 6) : 0, Rnd(5, 14),
            r > 4.8f && _rng.NextDouble() < 0.5 ? Rnd(15, 55) : 0, Rnd(0.5f, 4));
    }

    private Flake NewFlake(int w, int h, bool anywhere) => new(
        Rnd(0, w), anywhere ? Rnd(0, h) : Rnd(-40, -5f),
        Rnd(1, 3.4f), Rnd(25, 70), Rnd(0, MathF.PI * 2), Rnd(0.4f, 1.2f), Rnd(0.45f, 0.9f));

    private void ResetFlake(Flake f, int w, int h) =>
        (f.X, f.Y) = (Rnd(0, w), Rnd(-40, -5f));

    private float Rnd(float a, float b) => a + (float)_rng.NextDouble() * (b - a);

    public void Dispose()
    {
        _filteredBg?.Dispose();
        _starBrush.Dispose();
        _dropBodyBrush.Dispose();
        _dropRimPen.Dispose();
        _flakeBrush.Dispose();
    }

    // Private particle data types
    private class Streak(float x, float y, float len, float spd, float alpha)
    {
        public float X = x, Y = y, Length = len, Speed = spd, Alpha = alpha;
    }
    private class Drop(float x, float y, float r, float age, float life, float slide, float wait)
    {
        public float X = x, Y = y, Radius = r, Age = age, Life = life, Slide = slide, Wait = wait;
    }
    private class Flake(float x, float y, float r, float spd, float ph, float sw, float alpha)
    {
        public float X = x, Y = y, Radius = r, Speed = spd, Phase = ph, Sway = sw, Alpha = alpha;
    }
    private record Star(float X, float Y, float Radius, float Alpha, float Speed, float Phase);
    private record Ray(float Angle, float Width, float Alpha, float Speed, float Phase);
    private record RaySource(float X, float Y, int Dir, float Power);
}
