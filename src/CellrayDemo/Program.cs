using Cellray;

// Optional: pass a frame count to run for N frames and exit (used for smoke tests).
int maxFrames = args.Length > 0 && int.TryParse(args[0], out var n) ? n : int.MaxValue;

Tui.InitTerminal();
Tui.SetTargetFPS(60);

int frame = 0, clicks = 0;
double bx = 100, by = 5, vx = 1, vy = 0.4;
bool showBlock = true, fast = false;
float speed = 30f;

// live data for the graph and meters
var rng = new Random(1);
var cpu = new History(256);
double lastSample = 0;
float cpuNow = 0, memNow = 0, swapNow = 0;

// form state
string name = "";
int mode = 1;
int quality = 2;
string[] modes = { "Easy", "Normal", "Hard" };
string[] qualities = { "Low", "Medium", "High", "Ultra", "Insane", "Overkill" };
bool submitted = false;

while (!Tui.ShouldClose() && frame < maxFrames)
{
    Tui.BeginDrawing();
    float dt = Tui.GetFrameTime() > 0 ? Tui.GetFrameTime() : 1f / 60f;
    double now = Tui.GetTime();

    // sample the fake load ~15 times a second (frame-count fallback keeps headless runs moving)
    if (now - lastSample > 1.0 / 15 || frame % 4 == 0 && maxFrames != int.MaxValue)
    {
        lastSample = now;
        double wave = 50 + 38 * Math.Sin(now * 1.3) + 10 * Math.Sin(now * 4.1);
        cpuNow = (float)Math.Clamp(wave + rng.NextDouble() * 8 - 4, 0, 100);
        memNow = (float)Math.Clamp(55 + 20 * Math.Sin(now * 0.4), 0, 100);
        swapNow = (float)Math.Clamp(12 + 8 * Math.Sin(now * 0.7), 0, 100);
        cpu.Push(cpuNow);
    }

    // bouncing block (speed controlled by the slider)
    double s = speed * (fast ? 2 : 1);
    bx += Math.Sign(vx) * s * dt; by += Math.Sign(vy) * s * 0.4 * dt;
    if (bx < 0 || bx > Tui.GetScreenWidth() - 6) { vx = -vx; bx = Math.Clamp(bx, 0, Tui.GetScreenWidth() - 6); }
    if (by < 2 || by > Tui.GetScreenHeight() - 3) { vy = -vy; by = Math.Clamp(by, 2, Tui.GetScreenHeight() - 3); }

    // terminal's own background shows through
    Tui.ClearBackground(Theme.MainBg);

    Tui.DrawText("Cellray", 1, 0, Theme.HiFg);
    Tui.DrawText(" demo   Tab / mouse to interact   Esc to quit", 8, 0, Theme.InactiveFg);

    if (showBlock) Tui.DrawRectangle((int)bx, (int)by, 6, 2, Color.Orange);

    // ---- panel 1: basic controls ----
    Gui.Panel(new Rect(2, 2, 38, 17), "controls", number: 1);

    if (Gui.Button(new Rect(4, 4, 14, 3), "Click me")) clicks++;
    Gui.Label(20, 5, $"clicked {clicks} times");

    Gui.Checkbox(4, 8, "Show block", ref showBlock);
    Gui.Toggle(4, 10, "Fast mode", ref fast);

    Gui.Label(4, 12, $"Speed: {speed:F0}");
    Gui.Slider(4, 13, 30, ref speed, 5f, 80f);

    Gui.Label(4, 15, $"mouse {Tui.GetMouseX()},{Tui.GetMouseY()}", Theme.InactiveFg);
    Gui.Label(4, 16, $"{Tui.GetFPS()} fps  {Tui.GetScreenWidth()}x{Tui.GetScreenHeight()}", Theme.InactiveFg);

    // ---- panel 2: Braille graph + meters ----
    Gui.Panel(new Rect(42, 2, 50, 13), "load", number: 2);
    Gui.Graph(44, 3, 46, 5, cpu);
    Gui.Label(44, 9, "cpu", Theme.InactiveFg);
    Gui.Meter(49, 9, 36, cpuNow);
    Gui.Label(86, 9, $"{cpuNow,3:F0}%", Theme.MainFg);
    Gui.Label(44, 10, "mem", Theme.InactiveFg);
    Gui.Meter(49, 10, 36, memNow);
    Gui.Label(86, 10, $"{memNow,3:F0}%", Theme.MainFg);
    Gui.Label(44, 11, "swp", Theme.InactiveFg);
    Gui.Meter(49, 11, 36, swapNow);
    Gui.Label(86, 11, $"{swapNow,3:F0}%", Theme.MainFg);
    Gui.Label(44, 13, "braille dots: 2 samples x 4 levels per cell", Theme.InactiveFg);

    // ---- panel 3: form ----
    Gui.Panel(new Rect(42, 16, 50, 13), "form", number: 3);
    Gui.Label(44, 17, "Name", Theme.InactiveFg);
    Gui.TextBox(new Rect(44, 18, 30, 3), ref name, out bool enter, "type here...");
    if (enter) submitted = true;

    Gui.Label(44, 22, "Difficulty", Theme.InactiveFg);
    Gui.RadioGroup(44, 23, modes, ref mode);

    Gui.Label(62, 22, "Quality", Theme.InactiveFg);
    // list pops up over the widgets below it
    Gui.Dropdown(62, 23, 22, qualities, ref quality, maxVisible: 4);

    string who = name.Length > 0 ? name : "stranger";
    Gui.Label(44, 27, $"Hello {who}! {modes[mode]} / {qualities[quality]}{(submitted ? "  (entered)" : "")}", Theme.HiFg);

    Tui.EndDrawing();
    frame++;
}

Tui.CloseTerminal();
Console.WriteLine($"Rendered {frame} frames.");
