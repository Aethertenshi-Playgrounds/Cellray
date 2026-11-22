namespace Cellray;

/// <summary>24-bit RGB colour, Raylib-style.</summary>
public readonly record struct Color(byte R, byte G, byte B)
{
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
}
