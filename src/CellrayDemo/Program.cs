using Cellray;

// Optional: pass a frame count to run for N frames and exit (used for smoke tests).
int maxFrames = args.Length > 0 && int.TryParse(args[0], out var n) ? n : int.MaxValue;

Tui.InitTerminal();
Tui.SetTargetFPS(60);

int frame = 0;
double bx = 5, by = 5, vx = 30, vy = 12;

while (!Tui.ShouldClose() && frame < maxFrames)
{
    Tui.BeginDrawing();
    float dt = Tui.GetFrameTime() > 0 ? Tui.GetFrameTime() : 1f / 60f;

    // bouncing block
    bx += vx * dt; by += vy * dt;
    if (bx < 0 || bx > Tui.GetScreenWidth() - 6) { vx = -vx; bx = Math.Clamp(bx, 0, Tui.GetScreenWidth() - 6); }
    if (by < 2 || by > Tui.GetScreenHeight() - 3) { vy = -vy; by = Math.Clamp(by, 2, Tui.GetScreenHeight() - 3); }

    Tui.ClearBackground(new Color(20, 20, 30));
    Tui.DrawRectangle(0, 0, Tui.GetScreenWidth(), 1, Color.Blue);
    Tui.DrawText(" Cellray demo - press ESC to quit ", 1, 0, Color.White, Color.Blue);
    Tui.DrawRectangle((int)bx, (int)by, 6, 2, Color.Orange);
    Tui.DrawRectangleLines(2, 3, 30, 5, Color.SkyBlue);
    Tui.DrawText($"FPS: {Tui.GetFPS()}", 4, 4, Color.Green);
    Tui.DrawText($"Size: {Tui.GetScreenWidth()}x{Tui.GetScreenHeight()}", 4, 5, Color.Yellow);
    Tui.DrawText($"Frame: {frame}", 4, 6, Color.Gray);

    Tui.EndDrawing();
    frame++;
}

Tui.CloseTerminal();
Console.WriteLine($"Rendered {frame} frames.");
