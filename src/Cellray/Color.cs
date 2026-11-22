namespace Cellray;

/// <summary>
/// 24-bit RGB colour, Raylib-style. A == 0 means "the terminal's own default colour"
/// (see <see cref="Default"/>), so the terminal's background, transparency or blur shows through.
/// </summary>
public readonly record struct Color(byte R, byte G, byte B, byte A = 255)
{
    /// <summary>Use the terminal's default foreground/background colour.</summary>
    public static readonly Color Default = new(0, 0, 0, 0);

    public static readonly Color Black = new(0, 0, 0);
    public static readonly Color White = new(255, 255, 255);
    public static readonly Color Gray = new(130, 130, 130);
    public static readonly Color DarkGray = new(80, 80, 80);
    public static readonly Color Red = new(230, 41, 55);
    public static readonly Color Green = new(0, 228, 48);
    public static readonly Color Blue = new(0, 121, 241);
    public static readonly Color Yellow = new(253, 249, 0);
    public static readonly Color Orange = new(255, 161, 0);
    public static readonly Color Purple = new(200, 122, 255);
    public static readonly Color SkyBlue = new(102, 191, 255);

    public bool IsDefault => A == 0;

    /// <summary>Parses "#rrggbb" or "rrggbb".</summary>
    public static Color FromHex(string hex)
    {
        ReadOnlySpan<char> s = hex.AsSpan();
        if (s.Length > 0 && s[0] == '#') s = s.Slice(1);
        if (s.Length != 6) throw new FormatException("Expected #rrggbb");
        return new Color(
            byte.Parse(s.Slice(0, 2), System.Globalization.NumberStyles.HexNumber),
            byte.Parse(s.Slice(2, 2), System.Globalization.NumberStyles.HexNumber),
            byte.Parse(s.Slice(4, 2), System.Globalization.NumberStyles.HexNumber));
    }

    /// <summary>Linear blend between two colours, t in [0,1].</summary>
    public static Color Lerp(Color a, Color b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new Color(
            (byte)MathF.Round(a.R + (b.R - a.R) * t),
            (byte)MathF.Round(a.G + (b.G - a.G) * t),
            (byte)MathF.Round(a.B + (b.B - a.B) * t));
    }
}
