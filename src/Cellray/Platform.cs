using System.Runtime.InteropServices;

namespace Cellray;

/// <summary>Thin platform layer. Everything else in the library is plain ANSI/VT.</summary>
internal static partial class Platform
{
    private const int STD_OUTPUT_HANDLE = -11;
    private const uint ENABLE_PROCESSED_OUTPUT = 0x0001;
    private const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial nint GetStdHandle(int nStdHandle);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetConsoleMode(nint hConsole, out uint mode);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetConsoleMode(nint hConsole, uint mode);

    [LibraryImport("winmm.dll")]
    private static partial uint timeBeginPeriod(uint ms);

    [LibraryImport("winmm.dll")]
    private static partial uint timeEndPeriod(uint ms);

    private static bool _timerRaised;

    /// <summary>Asks Windows for 1 ms sleep granularity (default is ~15.6 ms). No-op elsewhere.</summary>
    public static void RaiseTimerResolution()
    {
        if (!OperatingSystem.IsWindows() || _timerRaised) return;
        try { timeBeginPeriod(1); _timerRaised = true; } catch { }
    }

    public static void RestoreTimerResolution()
    {
        if (!_timerRaised) return;
        try { timeEndPeriod(1); } catch { }
        _timerRaised = false;
    }

    /// <summary>Turns on ANSI escape processing on Windows. No-op elsewhere.</summary>
    public static void EnableVirtualTerminal()
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            nint h = GetStdHandle(STD_OUTPUT_HANDLE);
            if (GetConsoleMode(h, out uint mode))
                SetConsoleMode(h, mode | ENABLE_PROCESSED_OUTPUT | ENABLE_VIRTUAL_TERMINAL_PROCESSING);
        }
        catch { /* best effort: Windows Terminal already handles VT */ }
    }

    // ---- raw input ------------------------------------------------------

    private const int STD_INPUT_HANDLE = -10;
    private const uint ENABLE_PROCESSED_INPUT = 0x0001;
    private const uint ENABLE_LINE_INPUT = 0x0002;
    private const uint ENABLE_ECHO_INPUT = 0x0004;
    private const uint ENABLE_QUICK_EDIT_MODE = 0x0040;
    private const uint ENABLE_EXTENDED_FLAGS = 0x0080;
    private const uint ENABLE_VIRTUAL_TERMINAL_INPUT = 0x0200;
    private const int TermiosBufferSize = 512; // generously larger than struct termios on Linux/macOS

    private static uint _oldInputMode;
    private static bool _inputModeSaved;
    private static byte[]? _savedTermios;

    [LibraryImport("libc", EntryPoint = "tcgetattr")]
    private static partial int tcgetattr(int fd, nint termios);

    [LibraryImport("libc", EntryPoint = "tcsetattr")]
    private static partial int tcsetattr(int fd, int optionalActions, nint termios);

    [LibraryImport("libc", EntryPoint = "cfmakeraw")]
    private static partial void cfmakeraw(nint termios);

    /// <summary>Unbuffered, no-echo, no-signal input so keys and VT mouse reports arrive as raw bytes.</summary>
    public static void EnableRawInput()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                nint h = GetStdHandle(STD_INPUT_HANDLE);
                if (GetConsoleMode(h, out uint mode))
                {
                    _oldInputMode = mode;
                    _inputModeSaved = true;
                    uint raw = (mode & ~(ENABLE_PROCESSED_INPUT | ENABLE_LINE_INPUT | ENABLE_ECHO_INPUT | ENABLE_QUICK_EDIT_MODE))
                               | ENABLE_EXTENDED_FLAGS | ENABLE_VIRTUAL_TERMINAL_INPUT;
                    SetConsoleMode(h, raw);
                }
            }
            else
            {
                nint buf = Marshal.AllocHGlobal(TermiosBufferSize);
                try
                {
                    Marshal.Copy(new byte[TermiosBufferSize], 0, buf, TermiosBufferSize);
                    if (tcgetattr(0, buf) == 0)
                    {
                        _savedTermios = new byte[TermiosBufferSize];
                        Marshal.Copy(buf, _savedTermios, 0, TermiosBufferSize);
                        cfmakeraw(buf);
                        tcsetattr(0, 0, buf); // TCSANOW
                    }
                }
                finally { Marshal.FreeHGlobal(buf); }
            }
        }
        catch { /* best effort */ }
    }

    public static void RestoreInput()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                if (_inputModeSaved) SetConsoleMode(GetStdHandle(STD_INPUT_HANDLE), _oldInputMode);
                _inputModeSaved = false;
            }
            else if (_savedTermios is not null)
            {
                nint buf = Marshal.AllocHGlobal(TermiosBufferSize);
                try
                {
                    Marshal.Copy(_savedTermios, 0, buf, TermiosBufferSize);
                    tcsetattr(0, 0, buf);
                }
                finally { Marshal.FreeHGlobal(buf); }
                _savedTermios = null;
            }
        }
        catch { }
    }

    public static (int Width, int Height) GetSize()
    {
        try { return (Console.WindowWidth, Console.WindowHeight); }
        catch { return (80, 24); } // output redirected
    }
}
