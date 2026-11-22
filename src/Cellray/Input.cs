using System.Text;

namespace Cellray;

public enum MouseButton { Left = 0, Middle = 1, Right = 2 }

/// <summary>
/// Raw terminal input: a background thread reads stdin bytes, and Poll() parses them once per frame
/// into key presses, typed characters and SGR mouse events.
/// </summary>
internal static class Input
{
    private static readonly object Lock = new();
    private static readonly List<byte> Incoming = new();
    private static readonly List<byte> Pending = new();
    private static Thread? _reader;
    private static bool _started;

    private static readonly HashSet<ConsoleKey> Pressed = new();
    private static readonly Queue<char> Chars = new();
    private static readonly bool[] Down = new bool[3];
    private static readonly bool[] MPressed = new bool[3];
    private static readonly bool[] MReleased = new bool[3];

    public static bool CtrlC;
    public static bool ShiftTab;
    public static int MouseX = -1, MouseY = -1;
    public static int Wheel;

    public static void Start()
    {
        if (_started || Console.IsInputRedirected) return;
        try { Console.InputEncoding = new UTF8Encoding(false); } catch { }
        Platform.EnableRawInput();
        _started = true;
        if (_reader is null)
        {
            var stream = Console.OpenStandardInput();
            _reader = new Thread(() => ReadLoop(stream)) { IsBackground = true, Name = "Cellray input" };
            _reader.Start();
        }
    }

    public static void Stop()
    {
        if (!_started) return;
        _started = false;
        Platform.RestoreInput();
    }

    private static void ReadLoop(Stream s)
    {
        var buf = new byte[256];
        try
        {
            while (true)
            {
                int n = s.Read(buf, 0, buf.Length);
                if (n <= 0) break;
                if (!_started) continue;
                lock (Lock)
                {
                    for (int i = 0; i < n; i++) Incoming.Add(buf[i]);
                }
            }
        }
        catch { /* stream closed */ }
    }

    // ---- test hooks (InternalsVisibleTo) --------------------------------

    /// <summary>Queues raw terminal input exactly as the stdin reader would, e.g. "abc", "\t", "\x1b[A", "\x1b[&lt;0;5;3M".</summary>
    internal static void TestFeed(string text)
    {
        lock (Lock)
        {
            foreach (byte b in Encoding.UTF8.GetBytes(text)) Incoming.Add(b);
        }
    }

    /// <summary>Forgets all queued and parsed input, mouse position and button state.</summary>
    internal static void ResetForTests()
    {
        lock (Lock) { Incoming.Clear(); }
        Pending.Clear();
        Pressed.Clear();
        Chars.Clear();
        for (int i = 0; i < 3; i++) { Down[i] = false; MPressed[i] = false; MReleased[i] = false; }
        CtrlC = false;
        ShiftTab = false;
        MouseX = MouseY = -1;
        Wheel = 0;
    }

    // ---- queries --------------------------------------------------------

    public static bool IsKeyPressed(ConsoleKey key) => Pressed.Contains(key);
    public static bool MouseDown(MouseButton b) => Down[(int)b];
    public static bool MousePressed(MouseButton b) => MPressed[(int)b];
    public static bool MouseReleased(MouseButton b) => MReleased[(int)b];
    public static char NextChar() => Chars.Count > 0 ? Chars.Dequeue() : '\0';

    // ---- per-frame poll -------------------------------------------------

    public static void Poll()
    {
        Pressed.Clear();
        Chars.Clear(); // typed characters are per-frame, so stale keystrokes never reach a later text box
        CtrlC = false;
        ShiftTab = false;
        Wheel = 0;
        for (int i = 0; i < 3; i++) { MPressed[i] = false; MReleased[i] = false; }

        lock (Lock)
        {
            if (Incoming.Count > 0) { Pending.AddRange(Incoming); Incoming.Clear(); }
        }
        if (Pending.Count == 0) return;

        int consumed = Parse(Pending);
        Pending.RemoveRange(0, consumed);
        if (Pending.Count > 64) Pending.Clear(); // garbage guard
        while (Chars.Count > 256) Chars.Dequeue();
    }

    /// <summary>Parses as many complete events as possible; returns bytes consumed.</summary>
    private static int Parse(List<byte> p)
    {
        int i = 0;
        while (i < p.Count)
        {
            byte b = p[i];

            if (b == 0x1B)
            {
                if (i + 1 >= p.Count) { Pressed.Add(ConsoleKey.Escape); i++; continue; } // lone ESC

                byte next = p[i + 1];
                if (next == (byte)'[')
                {
                    int j = i + 2;
                    while (j < p.Count && !(p[j] >= 0x40 && p[j] <= 0x7E)) j++;
                    if (j >= p.Count) return i; // incomplete sequence, wait for more bytes
                    var prm = Encoding.ASCII.GetString(p.GetRange(i + 2, j - (i + 2)).ToArray());
                    HandleCsi(prm, (char)p[j]);
                    i = j + 1;
                }
                else if (next == (byte)'O')
                {
                    if (i + 2 >= p.Count) return i;
                    HandleSs3((char)p[i + 2]);
                    i += 3;
                }
                else
                {
                    Pressed.Add(ConsoleKey.Escape); // ESC followed by something else (Alt+key): treat as Escape
                    i++;
                }
                continue;
            }

            if (b == 0x03) { CtrlC = true; i++; continue; }
            if (b == 0x0D) { Pressed.Add(ConsoleKey.Enter); i++; if (i < p.Count && p[i] == 0x0A) i++; continue; }
            if (b == 0x0A) { Pressed.Add(ConsoleKey.Enter); i++; continue; }
            if (b == 0x09) { Pressed.Add(ConsoleKey.Tab); i++; continue; }
            if (b == 0x7F || b == 0x08) { Pressed.Add(ConsoleKey.Backspace); i++; continue; }
            if (b < 0x20) { i++; continue; } // other control characters ignored

            // printable: decode one UTF-8 scalar
            int len = b < 0x80 ? 1 : b >= 0xF0 ? 4 : b >= 0xE0 ? 3 : b >= 0xC0 ? 2 : 1;
            if (i + len > p.Count) return i; // incomplete UTF-8
            string s = Encoding.UTF8.GetString(p.GetRange(i, len).ToArray());
            foreach (char c in s)
            {
                Chars.Enqueue(c);
                var key = KeyFromChar(c);
                if (key != 0) Pressed.Add(key);
            }
            i += len;
        }
        return i;
    }

    private static ConsoleKey KeyFromChar(char c)
    {
        if (c >= 'a' && c <= 'z') return (ConsoleKey)((int)ConsoleKey.A + (c - 'a'));
        if (c >= 'A' && c <= 'Z') return (ConsoleKey)((int)ConsoleKey.A + (c - 'A'));
        if (c >= '0' && c <= '9') return (ConsoleKey)((int)ConsoleKey.D0 + (c - '0'));
        if (c == ' ') return ConsoleKey.Spacebar;
        return 0;
    }

    private static void HandleSs3(char c)
    {
        switch (c)
        {
            case 'A': Pressed.Add(ConsoleKey.UpArrow); break;
            case 'B': Pressed.Add(ConsoleKey.DownArrow); break;
            case 'C': Pressed.Add(ConsoleKey.RightArrow); break;
            case 'D': Pressed.Add(ConsoleKey.LeftArrow); break;
            case 'H': Pressed.Add(ConsoleKey.Home); break;
            case 'F': Pressed.Add(ConsoleKey.End); break;
        }
    }

    private static void HandleCsi(string prm, char fin)
    {
        if (prm.Length > 0 && prm[0] == '<' && (fin == 'M' || fin == 'm'))
        {
            HandleMouse(prm.Substring(1), fin == 'M');
            return;
        }
        switch (fin)
        {
            case 'A': Pressed.Add(ConsoleKey.UpArrow); break;
            case 'B': Pressed.Add(ConsoleKey.DownArrow); break;
            case 'C': Pressed.Add(ConsoleKey.RightArrow); break;
            case 'D': Pressed.Add(ConsoleKey.LeftArrow); break;
            case 'H': Pressed.Add(ConsoleKey.Home); break;
            case 'F': Pressed.Add(ConsoleKey.End); break;
            case 'Z': Pressed.Add(ConsoleKey.Tab); ShiftTab = true; break;
            case '~':
                int semi = prm.IndexOf(';');
                if (int.TryParse(semi >= 0 ? prm.AsSpan(0, semi) : prm.AsSpan(), out int code))
                {
                    switch (code)
                    {
                        case 1: case 7: Pressed.Add(ConsoleKey.Home); break;
                        case 2: Pressed.Add(ConsoleKey.Insert); break;
                        case 3: Pressed.Add(ConsoleKey.Delete); break;
                        case 4: case 8: Pressed.Add(ConsoleKey.End); break;
                        case 5: Pressed.Add(ConsoleKey.PageUp); break;
                        case 6: Pressed.Add(ConsoleKey.PageDown); break;
                    }
                }
                break;
        }
    }

    /// <summary>SGR mouse: "button;x;y" with 'M' = press/motion, 'm' = release.</summary>
    private static void HandleMouse(string prm, bool pressOrMotion)
    {
        var parts = prm.Split(';');
        if (parts.Length < 3 ||
            !int.TryParse(parts[0], out int b) ||
            !int.TryParse(parts[1], out int x) ||
            !int.TryParse(parts[2], out int y)) return;

        MouseX = Math.Max(0, x - 1);
        MouseY = Math.Max(0, y - 1);

        if ((b & 64) != 0) { Wheel += (b & 1) == 0 ? 1 : -1; return; }

        int btn = b & 3;
        if (btn > 2) return; // motion with no button held
        bool motion = (b & 32) != 0;
        if (motion) return; // position already updated; button state is unchanged

        if (pressOrMotion) { Down[btn] = true; MPressed[btn] = true; }
        else { Down[btn] = false; MReleased[btn] = true; }
    }
}
