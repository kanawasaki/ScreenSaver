using System.Drawing.Drawing2D;
using System.Globalization;

namespace ClockScreensaver;

public static class ClockRenderer
{
    private static readonly CultureInfo _en = CultureInfo.GetCultureInfo("en-US");

    private static string FormatDate(DateTime d, DateFormat fmt) => fmt switch
    {
        DateFormat.Short    => d.ToString("ddd, MMM d", _en),    // "Wed, Sep 23"
        DateFormat.Long     => d.ToString("dddd, MMMM d", _en),  // "Wednesday, September 23"
        DateFormat.DayMonth => d.ToString("d MMMM", _en),        // "23 September"
        DateFormat.Numeric  => d.ToString("dd.MM.yyyy"),          // "23.09.2026"
        _                   => d.ToString("ddd, MMM d", _en),
    };

    // Weather icon paths (SVG-equivalent drawn with GDI+)
    // Each returns a list of drawing actions on a normalized 24x24 grid
    public static void DrawWeatherIcon(Graphics g, WeatherCondition cond, RectangleF rect, Color color)
    {
        using var pen = new Pen(color, 1.5f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        float s = rect.Width / 24f;
        float ox = rect.X, oy = rect.Y;
        PointF P(float x, float y) => new(ox + x * s, oy + y * s);

        g.SmoothingMode = SmoothingMode.AntiAlias;

        switch (cond)
        {
            case WeatherCondition.Clear:
                // Circle + rays
                g.DrawEllipse(pen, ox + 8*s, oy + 8*s, 8*s, 8*s);
                DrawLine(g, pen, P(12,2.5f), P(12,4.5f));
                DrawLine(g, pen, P(12,19.5f), P(12,21.5f));
                DrawLine(g, pen, P(2.5f,12), P(4.5f,12));
                DrawLine(g, pen, P(19.5f,12), P(21.5f,12));
                DrawLine(g, pen, P(5.3f,5.3f), P(6.7f,6.7f));
                DrawLine(g, pen, P(17.3f,17.3f), P(18.7f,18.7f));
                DrawLine(g, pen, P(5.3f,18.7f), P(6.7f,17.3f));
                DrawLine(g, pen, P(17.3f,6.7f), P(18.7f,5.3f));
                break;

            case WeatherCondition.PartlyCloudy:
                DrawCloud(g, pen, ox, oy, s, 0.4f);
                // Small sun behind-left
                using (var sunPen = new Pen(color, 1.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    g.DrawEllipse(sunPen, ox + 5f*s, oy + 4f*s, 6*s, 6*s);
                    DrawLine(g, sunPen, P(8,2), P(8,3.3f));
                    DrawLine(g, sunPen, P(3.5f,8), P(4.8f,8));
                    DrawLine(g, sunPen, P(4.8f,4.8f), P(5.7f,5.7f));
                    DrawLine(g, sunPen, P(11.2f,4.8f), P(10.3f,5.7f));
                }
                break;

            case WeatherCondition.Cloudy:
                DrawCloud(g, pen, ox, oy, s, 0f);
                break;

            case WeatherCondition.Rain:
                DrawCloud(g, pen, ox, oy, s, -1f);
                DrawLine(g, pen, P(8,18), P(7,20.5f));
                DrawLine(g, pen, P(12,18), P(11,20.5f));
                DrawLine(g, pen, P(16,18), P(15,20.5f));
                break;

            case WeatherCondition.Snow:
                DrawCloud(g, pen, ox, oy, s, -1f);
                using (var dotPen = new Pen(color, 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                {
                    DrawDot(g, dotPen, P(8,19)); DrawDot(g, dotPen, P(12,20));
                    DrawDot(g, dotPen, P(16,19)); DrawDot(g, dotPen, P(10,22));
                    DrawDot(g, dotPen, P(14,22));
                }
                break;

            case WeatherCondition.Storm:
                DrawCloud(g, pen, ox, oy, s, -1f);
                // Lightning bolt
                DrawLine(g, pen, P(12.5f,15), P(10.5f,18.5f));
                DrawLine(g, pen, P(10.5f,18.5f), P(13.5f,18.5f));
                DrawLine(g, pen, P(13.5f,18.5f), P(11.5f,22));
                break;
        }
    }

    private static void DrawCloud(Graphics g, Pen pen, float ox, float oy, float s, float yOff)
    {
        // Approximate cloud path
        float y = 14 + yOff;
        using var path = new GraphicsPath();
        path.AddArc(ox + 4*s, oy + (y-4)*s, 8*s, 8*s, 90, 180);   // left bump
        path.AddArc(ox + 6*s, oy + (y-8)*s, 7*s, 7*s, 200, -160); // top-left arc
        path.AddArc(ox + 11*s, oy + (y-9)*s, 6*s, 6*s, 190, -140); // top-right
        path.AddArc(ox + 13*s, oy + (y-5)*s, 6*s, 6*s, 270, 90);  // right bump
        path.CloseFigure();
        g.DrawPath(pen, path);
    }

    private static void DrawLine(Graphics g, Pen p, PointF a, PointF b) =>
        g.DrawLine(p, a, b);

    private static void DrawDot(Graphics g, Pen p, PointF pt) =>
        g.DrawLine(p, pt, new PointF(pt.X + 0.01f, pt.Y));

    public static void Draw(Graphics g, Settings settings, WeatherResult? weather, Rectangle bounds, float opacity, bool hasBackground = false)
    {
        if (opacity <= 0) return;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

        var now = DateTime.Now;

        // Clock face color
        byte alpha = (byte)(255 * opacity);
        var clockColor = Color.FromArgb(alpha, 217, 217, 217);

        float emSize = settings.Size;
        using var font = FontManager.GetFont(settings, emSize);

        // Build time string
        int h = now.Hour;
        string ampm = "";
        if (!settings.Hours24) { ampm = h < 12 ? "AM" : "PM"; h = h % 12 == 0 ? 12 : h % 12; }
        string timeStr = (settings.Hours24 ? h.ToString("D2") : h.ToString()) +
                         ":" + now.Minute.ToString("D2") +
                         (settings.ShowSeconds ? ":" + now.Second.ToString("D2") : "");

        // Measure
        SizeF timeSize = g.MeasureString(timeStr, font);

        // Sub-line font (32% of clock size)
        float subEm = emSize * 0.32f;
        using var subFont = FontManager.GetFont(settings, subEm);

        // Build sub-line (always English, regardless of system locale)
        string? datePart = settings.ShowDate ? FormatDate(now, settings.DateFmt) : null;

        string? tempPart = null;
        WeatherCondition? wxCond = null;
        if (settings.WeatherOn && weather != null)
        {
            float temp = weather.GetTemp(settings.Unit);
            string tempStr = ((int)MathF.Round(temp)) + "°" +
                (settings.ShowConditionName ? " " + WmoMapper.Name(weather.Condition) : "");
            tempPart = tempStr;
            wxCond = weather.Condition;
        }

        bool hasSub = datePart != null || tempPart != null;

        // Calculate block height
        float lineGap = emSize * 0.5f;
        float totalHeight = timeSize.Height + (hasSub ? lineGap + subEm * 1.6f : 0);

        // AMPM suffix
        float ampmEm = emSize * 0.4f;
        using var ampmFont = FontManager.GetFont(settings, ampmEm);

        // Position the block
        PointF origin = GetOrigin(settings, bounds, timeSize.Width, totalHeight);

        // Apply text shadow when there's a background image
        if (hasBackground)
        {
            using var shadowBrush = new SolidBrush(Color.FromArgb((byte)(alpha * 0.55), 0, 0, 0));
            g.DrawString(timeStr, font, shadowBrush, origin.X + 0, origin.Y + 2);
        }

        // Draw time
        using var brush = new SolidBrush(clockColor);
        g.DrawString(timeStr, font, brush, origin.X, origin.Y);

        // AM/PM
        if (!string.IsNullOrEmpty(ampm))
        {
            float ampmX = origin.X + timeSize.Width;
            float ampmY = origin.Y + emSize * 0.1f;
            g.DrawString(ampm, ampmFont, brush, ampmX, ampmY);
        }

        if (!hasSub) return;

        // Sub-line
        float subY = origin.Y + timeSize.Height + lineGap * 0.25f;
        float subAlpha = 0.75f;
        using var subBrush = new SolidBrush(Color.FromArgb((byte)(alpha * subAlpha), 217, 217, 217));

        float subX = origin.X;
        float iconSize = subEm * 1.25f;

        bool rightAlign = settings.Position is ClockPosition.TopRight or ClockPosition.BottomRight;
        bool centerAlign = settings.Position is ClockPosition.Center;

        if (rightAlign || centerAlign)
        {
            float totalSubW = 0;
            float dateW = datePart != null ? g.MeasureString(datePart, subFont).Width : 0;
            float gap = subEm * 0.6f;
            float wxW = tempPart != null ? iconSize + subEm * 0.35f + g.MeasureString(tempPart, subFont).Width : 0;

            if (datePart != null) totalSubW += dateW;
            if (datePart != null && tempPart != null) totalSubW += gap;
            if (tempPart != null) totalSubW += wxW;

            if (rightAlign)
                subX = origin.X + timeSize.Width - totalSubW;
            else // center
                subX = origin.X + (timeSize.Width - totalSubW) / 2f;
        }

        if (datePart != null)
        {
            g.DrawString(datePart, subFont, subBrush, subX, subY);
            float dw = g.MeasureString(datePart, subFont).Width;
            if (tempPart != null)
                subX += dw + subEm * 0.6f;
        }

        if (tempPart != null && wxCond.HasValue)
        {
            float iconY = subY + (subEm * 1.6f - iconSize) / 2f;
            DrawWeatherIcon(g, wxCond.Value, new RectangleF(subX, iconY, iconSize, iconSize), subBrush.Color);
            subX += iconSize + subEm * 0.35f;
            g.DrawString(tempPart, subFont, subBrush, subX, subY);
        }
    }

    private static PointF GetOrigin(Settings s, Rectangle b, float w, float h)
    {
        int m = s.Margin;
        return s.Position switch
        {
            ClockPosition.TopLeft     => new(b.X + m, b.Y + m),
            ClockPosition.TopRight    => new(b.X + b.Width - m - w, b.Y + m),
            ClockPosition.BottomLeft  => new(b.X + m, b.Y + b.Height - m - h),
            ClockPosition.BottomRight => new(b.X + b.Width - m - w, b.Y + b.Height - m - h),
            ClockPosition.Center      => new(b.X + (b.Width - w) / 2f, b.Y + (b.Height - h) / 2f),
            _                         => new(b.X + m, b.Y + m),
        };
    }
}
