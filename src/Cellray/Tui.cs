using System.Diagnostics;
using System.Text;

namespace Cellray;

/// <summary>
/// Raylib-shaped terminal API. Coordinates are character cells, (0,0) is top-left.
/// Usage: InitTerminal(); while (!ShouldClose()) { BeginDrawing(); ...; EndDrawing(); } CloseTerminal();
/// </summary>
public static class Tui
{
    private struct Cell : IEquatable<Cell>
    {
        public char Ch;
        public Color Fg;
        public Color Bg;
        public readonly bool Equals(Cell o) => Ch == o.Ch && Fg == o.Fg && Bg == o.Bg;
    }

    private static Cell[] _back = Array.Empty<Cell>();
    private static Cell[] _front = Array.Empty<Cell>();
    private static int _w, _h;
    private static bool _forceFull;
    private static bool _initialized;
    private static bool _exitHooked;
    private static bool _escCaptured; // Esc belonged to a widget (open dropdown / focused text box) this frame
    private static bool _headless;    // test mode: fixed size, nothing is written to a terminal
    private static int _headlessW, _headlessH;
    private static bool _shouldClose;
    private static ConsoleKey _exitKey = ConsoleKey.Escape;

    private static readonly Stopwatch _clock = Stopwatch.StartNew();
    private static long _lastFrameTicks;
    private static float _frameTime;
    private static double _targetFrameSeconds; // 0 = unlimited

    private static Stream? _stdout;
    private static readonly StringBuilder _sb = new(64 * 1024);
    private static Color _clear = Color.Black;

    // ---- lifetime -------------------------------------------------------

    public static void InitTerminal()
    {
        if (_initialized) return;
        Platform.EnableVirtualTerminal();
        Platform.RaiseTimerResolution();
        Console.OutputEncoding = Encoding.UTF8;
        _stdout = Console.OpenStandardOutput();
        _initialized = true;
        _shouldClose = false;

        if (!_exitHooked)
        {
            _exitHooked = true;
            AppDomain.CurrentDomain.ProcessExit += (_, _) => CloseTerminal(); // restore terminal even on unhandled exit
        }

        Input.Start();

        // alternate screen, hide cursor, enable mouse (press/release + any-motion, SGR encoding), clear
        Write("\x1b[?1049h\x1b[?25l\x1b[?1000h\x1b[?1003h\x1b[?1006h\x1b[2J");
        Resize();
        _lastFrameTicks = _clock.ElapsedTicks;
    }

    public static void CloseTerminal()
    {
        if (!_initialized) return;
        _initialized = false;
        // mouse off, reset attributes, show cursor, leave alternate screen
        Write("\x1b[?1006l\x1b[?1003l\x1b[?1000l\x1b[0m\x1b[?25h\x1b[?1049l");
        Input.Stop();
        Platform.RestoreTimerResolution();
    }

    public static bool ShouldClose() => _shouldClose;
    /// <summary>Asks the main loop to finish (ShouldClose becomes true).</summary>
    public static void RequestClose() => _shouldClose = true;
    public static void SetExitKey(ConsoleKey key) => _exitKey = key;
    public static void SetTargetFPS(int fps) => _targetFrameSeconds = fps > 0 ? 1.0 / fps : 0;

    // ---- info -----------------------------------------------------------

    /// <summary>Width in columns.</summary>
    public static int GetScreenWidth() => _w;
    /// <summary>Height in rows.</summary>
    public static int GetScreenHeight() => _h;
    /// <summary>Seconds the last frame took (including the frame limiter wait).</summary>
    public static float GetFrameTime() => _frameTime;
    /// <summary>Smoothed frames per second.</summary>
    public static int GetFPS() => (int)MathF.Round(_fpsSmoothed);
    public static double GetTime() => _clock.Elapsed.TotalSeconds;

    // ---- input ----------------------------------------------------------

    /// <summary>True if the key was pressed (or auto-repeated) since the last BeginDrawing.</summary>
    public static bool IsKeyPressed(ConsoleKey key) => Input.IsKeyPressed(key);

    /// <summary>Next typed character this frame, or '\0' when none are left (Raylib's GetCharPressed).</summary>
    public static char GetCharPressed() => Input.NextChar();

    /// <summary>
    /// True when Esc was pressed this frame and no widget claimed it (an open dropdown or focused text box uses Esc itself).
    /// Use it for app-level "back"/"quit" together with SetExitKey(0).
    /// </summary>
    public static bool IsEscapePressed() => Input.IsKeyPressed(ConsoleKey.Escape) && !_escCaptured;

    public static int GetMouseX() => Input.MouseX;
    public static int GetMouseY() => Input.MouseY;
    public static (int X, int Y) GetMousePosition() => (Input.MouseX, Input.MouseY);
    public static bool IsMouseButtonPressed(MouseButton b) => Input.MousePressed(b);
    public static bool IsMouseButtonReleased(MouseButton b) => Input.MouseReleased(b);
    public static bool IsMouseButtonDown(MouseButton b) => Input.MouseDown(b);
    /// <summary>Scroll wheel movement this frame: positive = up.</summary>
    public static int GetMouseWheelMove() => Input.Wheel;

    private static void PollInput()
    {
        Input.Poll();
        _escCaptured = Gui.CapturesEscape;
        // Esc is left to an open dropdown / focused text box instead of quitting the app.
        bool exitKey = Input.IsKeyPressed(_exitKey) && (_exitKey != ConsoleKey.Escape || !Gui.CapturesEscape);
        if (exitKey || Input.CtrlC) _shouldClose = true;
    }

    // ---- drawing --------------------------------------------------------

    public static void BeginDrawing()
    {
        PollInput();
        var (w, h) = TerminalSize();
        if (w != _w || h != _h) Resize();
        Gui.BeginFrame();
    }

    public static void ClearBackground(Color color)
    {
        _clear = color;
        var blank = new Cell { Ch = ' ', Fg = Color.White, Bg = color };
        Array.Fill(_back, blank);
    }

    public static void DrawText(string text, int x, int y, Color fg)
        => DrawTextEx(text, x, y, fg, null);

    public static void DrawText(string text, int x, int y, Color fg, Color bg)
        => DrawTextEx(text, x, y, fg, bg);

    private static void DrawTextEx(string text, int x, int y, Color fg, Color? bg)
    {
        if ((uint)y >= (uint)_h) return;
        foreach (char c in text)
        {
            if ((uint)x < (uint)_w)
            {
                ref Cell cell = ref _back[y * _w + x];
                cell.Ch = c;
                cell.Fg = fg;
                if (bg is Color b) cell.Bg = b;
            }
            x++;
        }
    }

    /// <summary>Draws one character, keeping the cell's existing background.</summary>
    public static void DrawChar(char c, int x, int y, Color fg) => Put(x, y, c, fg);

    public static void DrawRectangle(int x, int y, int width, int height, Color color)
    {
        int x0 = Math.Max(x, 0), y0 = Math.Max(y, 0);
        int x1 = Math.Min(x + width, _w), y1 = Math.Min(y + height, _h);
        for (int j = y0; j < y1; j++)
            for (int i = x0; i < x1; i++)
            {
                ref Cell cell = ref _back[j * _w + i];
                cell.Ch = ' ';
                cell.Bg = color;
            }
    }

    /// <summary>Thin box outline; rounded = btop-style corners.</summary>
    public static void DrawRectangleLines(int x, int y, int width, int height, Color color, bool rounded = false)
    {
        if (width < 2 || height < 2) return;
        // Box-drawing: square ┌ ┐ └ ┘, rounded ╭ ╮ ╰ ╯, lines ─ │
        Put(x, y, rounded ? '╭' : '┌', color);
        Put(x + width - 1, y, rounded ? '╮' : '┐', color);
        Put(x, y + height - 1, rounded ? '╰' : '└', color);
        Put(x + width - 1, y + height - 1, rounded ? '╯' : '┘', color);
        for (int i = 1; i < width - 1; i++) { Put(x + i, y, '─', color); Put(x + i, y + height - 1, '─', color); }
        for (int j = 1; j < height - 1; j++) { Put(x, y + j, '│', color); Put(x + width - 1, y + j, '│', color); }
    }

    private static void Put(int x, int y, char c, Color fg)
    {
        if ((uint)x >= (uint)_w || (uint)y >= (uint)_h) return;
        ref Cell cell = ref _back[y * _w + x];
        cell.Ch = c;
        cell.Fg = fg;
    }

    // ---- present --------------------------------------------------------

    public static void EndDrawing()
    {
        Gui.EndFrame();
        Flush();
        LimitFrameRate();
    }

    private static void Flush()
    {
        _sb.Clear();
        bool havePos = false; int curX = -1, curY = -1;
        Color? curFg = null, curBg = null;

        for (int y = 0; y < _h; y++)
        {
            for (int x = 0; x < _w; x++)
            {
                int i = y * _w + x;
                Cell c = _back[i];
                if (!_forceFull && c.Equals(_front[i])) continue;
                _front[i] = c;

                if (!havePos || curY != y || curX != x)
                    _sb.Append("\x1b[").Append(y + 1).Append(';').Append(x + 1).Append('H');
                if (curFg != c.Fg)
                {
                    if (c.Fg.IsDefault) _sb.Append("\x1b[39m");
                    else _sb.Append("\x1b[38;2;").Append(c.Fg.R).Append(';').Append(c.Fg.G).Append(';').Append(c.Fg.B).Append('m');
                    curFg = c.Fg;
                }
                if (curBg != c.Bg)
                {
                    if (c.Bg.IsDefault) _sb.Append("\x1b[49m");
                    else _sb.Append("\x1b[48;2;").Append(c.Bg.R).Append(';').Append(c.Bg.G).Append(';').Append(c.Bg.B).Append('m');
                    curBg = c.Bg;
                }
                _sb.Append(c.Ch);
                havePos = true; curX = x + 1; curY = y;
            }
        }
        _forceFull = false;
        if (_sb.Length > 0) Write(_sb.ToString());
    }

    private static long _deadlineTicks;
    private static float _fpsSmoothed;

    private static void LimitFrameRate()
    {
        if (_targetFrameSeconds > 0)
        {
            long frameTicks = (long)(_targetFrameSeconds * Stopwatch.Frequency);
            // Fixed deadline: a late frame doesn't push every following frame back.
            _deadlineTicks = _deadlineTicks == 0 ? _lastFrameTicks + frameTicks : _deadlineTicks + frameTicks;
            long now0 = _clock.ElapsedTicks;
            if (now0 - _deadlineTicks > frameTicks * 4) _deadlineTicks = now0; // fell far behind: resync

            // Coarse sleep in 1 ms steps, then spin for the final ~1.5 ms.
            long spinTicks = Stopwatch.Frequency * 3 / 2000;
            while (_deadlineTicks - _clock.ElapsedTicks > spinTicks) Thread.Sleep(1);
            while (_clock.ElapsedTicks < _deadlineTicks) Thread.SpinWait(20);
        }
        else _deadlineTicks = 0;

        long now = _clock.ElapsedTicks;
        _frameTime = (now - _lastFrameTicks) / (float)Stopwatch.Frequency;
        _lastFrameTicks = now;

        // Smooth the displayed FPS so single hiccups don't make the counter jump.
        float inst = _frameTime > 0 ? 1f / _frameTime : 0f;
        _fpsSmoothed = _fpsSmoothed == 0 ? inst : _fpsSmoothed * 0.9f + inst * 0.1f;
    }

    // ---- internals ------------------------------------------------------

    private static (int Width, int Height) TerminalSize() => _headless ? (_headlessW, _headlessH) : Platform.GetSize();

    /// <summary>Test hook: runs the frame loop against an in-memory screen of a fixed size, with no terminal and no stdin reader.</summary>
    internal static void InitHeadless(int width, int height)
    {
        _headless = true;
        _headlessW = width;
        _headlessH = height;
        _stdout = null; // Write() then does nothing
        _shouldClose = false;
        Resize();
    }

    private static void Resize()
    {
        (_w, _h) = TerminalSize();
        if (_w < 1) _w = 1;
        if (_h < 1) _h = 1;
        _back = new Cell[_w * _h];
        _front = new Cell[_w * _h];
        Array.Fill(_back, new Cell { Ch = ' ', Fg = Color.White, Bg = _clear });
        _forceFull = true;
        Write("\x1b[2J");
    }

    private static void Write(string s)
    {
        if (_stdout is null) return;
        var bytes = Encoding.UTF8.GetBytes(s);
        _stdout.Write(bytes, 0, bytes.Length);
        _stdout.Flush();
    }
}
