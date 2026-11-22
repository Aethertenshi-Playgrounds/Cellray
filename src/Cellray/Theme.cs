namespace Cellray;

/// <summary>
/// Named colours used by the Gui widgets, modelled on btop's themes: thin dim lines, bright titles,
/// one accent colour for hover/focus, and the terminal's own background. Change fields to re-theme.
/// </summary>
public static class Theme
{
    /// <summary>Background. Color.Default = use the terminal's own background.</summary>
    public static Color MainBg = Color.Default;
    public static Color MainFg = Color.FromHex("#cfc9c2");
    public static Color Title = Color.FromHex("#cfc9c2");
    /// <summary>Accent for hover, focus and shortcut hints.</summary>
    public static Color HiFg = Color.FromHex("#7dcfff");
    public static Color SelectedBg = Color.FromHex("#414868");
    public static Color SelectedFg = Color.FromHex("#cfc9c2");
    /// <summary>Disabled / secondary text.</summary>
    public static Color InactiveFg = Color.FromHex("#565f89");
    /// <summary>Box outlines and idle widget lines.</summary>
    public static Color Box = Color.FromHex("#565f89");
    /// <summary>Unfilled part of a meter.</summary>
    public static Color MeterBg = Color.FromHex("#3b4261");
    /// <summary>"On" / success colour (checked boxes, enabled switches).</summary>
    public static Color Ok = Color.FromHex("#9ece6a");
    /// <summary>Value gradient for meters, graphs and sliders (low -> high).</summary>
    public static Gradient Heat = new(Color.FromHex("#9ece6a"), Color.FromHex("#e0af68"), Color.FromHex("#f7768e"));

    /// <summary>Tokyo Night (the default), as in btop's tokyo-night theme.</summary>
    public static void UseTokyoNight()
    {
        MainBg = Color.Default;
        MainFg = Title = SelectedFg = Color.FromHex("#cfc9c2");
        HiFg = Color.FromHex("#7dcfff");
        SelectedBg = Color.FromHex("#414868");
        InactiveFg = Box = Color.FromHex("#565f89");
        MeterBg = Color.FromHex("#3b4261");
        Ok = Color.FromHex("#9ece6a");
        Heat = new Gradient(Color.FromHex("#9ece6a"), Color.FromHex("#e0af68"), Color.FromHex("#f7768e"));
    }
}
