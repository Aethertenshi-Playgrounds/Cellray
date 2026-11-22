using Cellray;

// Gui, Tui and Input are process-wide static state, so tests must not run in parallel.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Cellray.Tests;

/// <summary>Drives real frames (input parsing, widgets, diffing) against an in-memory 80x24 screen.</summary>
internal static class Ui
{
    public static void Init()
    {
        Tui.InitHeadless(80, 24);
        Input.ResetForTests();
        Gui.ResetState();
        Gui.Disabled = false;
        Gui.StateTimeoutFrames = 600;
    }

    /// <summary>One frame: feed raw terminal input, BeginDrawing, run the drawing code, EndDrawing.</summary>
    public static void Frame(Action draw, string input = "")
    {
        if (input.Length > 0) Input.TestFeed(input);
        Tui.BeginDrawing();
        draw();
        Tui.EndDrawing();
    }

    /// <summary>SGR mouse press + release at a cell (0-based) within a single frame, i.e. a click.</summary>
    public static string Click(int x, int y) => $"\x1b[<0;{x + 1};{y + 1}M\x1b[<0;{x + 1};{y + 1}m";

    public const string Tab = "\t";
    public const string Down = "\x1b[B";
    public const string Up = "\x1b[A";
}
