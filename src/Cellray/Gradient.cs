namespace Cellray;

/// <summary>
/// A 101-step colour ramp (0..100) built from a start, optional mid and an end colour,
/// like btop's graph/meter gradients.
/// </summary>
public sealed class Gradient
{
    private readonly Color[] _steps = new Color[101];

    public Gradient(Color start, Color end)
    {
        for (int i = 0; i <= 100; i++) _steps[i] = Color.Lerp(start, end, i / 100f);
    }

    public Gradient(Color start, Color mid, Color end)
    {
        for (int i = 0; i <= 100; i++)
            _steps[i] = i <= 50 ? Color.Lerp(start, mid, i / 50f) : Color.Lerp(mid, end, (i - 50) / 50f);
    }

    /// <summary>Colour at a percentage 0..100.</summary>
    public Color At(int percent) => _steps[Math.Clamp(percent, 0, 100)];

    /// <summary>Colour at a fraction 0..1.</summary>
    public Color At(float t) => _steps[Math.Clamp((int)MathF.Round(t * 100f), 0, 100)];
}
