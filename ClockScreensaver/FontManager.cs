using System.Drawing.Text;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ClockScreensaver;

public static class FontManager
{
    private static PrivateFontCollection? _pfc;
    private static readonly Dictionary<string, FontFamily> _families = new(StringComparer.OrdinalIgnoreCase);

    public static void Load()
    {
        _pfc = new PrivateFontCollection();
        var asm = Assembly.GetExecutingAssembly();
        foreach (var name in asm.GetManifestResourceNames())
        {
            if (!name.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)) continue;
            using var stream = asm.GetManifestResourceStream(name)!;
            var data = new byte[stream.Length];
            stream.ReadExactly(data);
            var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try { _pfc.AddMemoryFont(handle.AddrOfPinnedObject(), data.Length); }
            finally { handle.Free(); }
        }
        foreach (var ff in _pfc.Families)
            _families[ff.Name] = ff;
    }

    // Returns (family name prefix, style) for each (font, weight) pair
    // These names match what PrivateFontCollection registers from the bundled TTFs
    private static (string prefix, FontStyle style) Resolve(ClockFont font, ClockWeight weight) => font switch
    {
        ClockFont.PlexMono     => weight == ClockWeight.Light ? ("IBM Plex Mono Light",   FontStyle.Regular)
                                                              : ("IBM Plex Mono Medium",  FontStyle.Regular),
        ClockFont.SpaceGrotesk => weight == ClockWeight.Light ? ("Space Grotesk Light",   FontStyle.Regular)
                                                              : ("Space Grotesk Medium",  FontStyle.Regular),
        ClockFont.Fraunces     => weight == ClockWeight.Light ? ("Fraunces 72pt",         FontStyle.Regular)
                                                              : ("Fraunces 72pt",         FontStyle.Regular),
        ClockFont.MajorMono    => ("Major Mono Display", FontStyle.Regular),
        ClockFont.Syne         => ("Syne", weight == ClockWeight.Bold ? FontStyle.Bold : FontStyle.Regular),
        _                      => ("", FontStyle.Regular),
    };

    public static Font GetFont(Settings s, float emSize)
    {
        var (prefix, style) = Resolve(s.Font, s.Weight);

        // For Fraunces, prefer Light then SemiBold family variants
        if (s.Font == ClockFont.Fraunces)
        {
            string target = s.Weight == ClockWeight.Light ? "Light" : "SemiBold";
            var ff = _families.Values.FirstOrDefault(f =>
                f.Name.StartsWith("Fraunces", StringComparison.OrdinalIgnoreCase) &&
                f.Name.Contains(target, StringComparison.OrdinalIgnoreCase));
            if (ff != null)
                return new Font(ff, emSize, FontStyle.Regular, GraphicsUnit.Pixel);
        }

        if (!string.IsNullOrEmpty(prefix))
        {
            var ff = _families.Values.FirstOrDefault(f =>
                f.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            if (ff != null)
            {
                var actual = ff.IsStyleAvailable(style) ? style : FontStyle.Regular;
                return new Font(ff, emSize, actual, GraphicsUnit.Pixel);
            }
        }

        // System font fallback
        bool bold = s.Weight == ClockWeight.Bold;
        return new Font("Segoe UI", emSize, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
    }
}
