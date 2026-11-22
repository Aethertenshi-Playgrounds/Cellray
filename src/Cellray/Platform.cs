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

    public static (int Width, int Height) GetSize()
    {
        try { return (Console.WindowWidth, Console.WindowHeight); }
        catch { return (80, 24); } // output redirected
    }
}
