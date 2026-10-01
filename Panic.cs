using System.Runtime.InteropServices;

// System-wide panic hotkey: fires even when the game window is unfocused.
// Win32 RegisterHotKey on a dedicated thread with its own message loop; the game loop polls Toggled().
static class Panic
{
    public const string KeyName = "Ctrl+Alt+Shift+Q";
    const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_NOREPEAT = 0x4000, VK_Q = 0x51;
    const uint WM_HOTKEY = 0x0312;

    static int _presses;
    public static bool Registered { get; private set; }

    public static void Start()
    {
        var ready = new ManualResetEventSlim();  // not disposed: the thread may Set() after a Wait timeout
        var t = new Thread(() =>
        {
            Registered = RegisterHotKey(0, 1, MOD_CONTROL | MOD_ALT | MOD_SHIFT | MOD_NOREPEAT, VK_Q);
            ready.Set();
            if (!Registered) return;
            // Hotkey dies with the thread/process; OS releases it on exit.
            while (GetMessage(out var msg, 0, 0, 0) > 0)
                if (msg.message == WM_HOTKEY) Interlocked.Increment(ref _presses);
        }) { IsBackground = true, Name = "PanicHotkey" };
        t.Start();
        ready.Wait(2000);
    }

    // True if the hotkey was pressed an odd number of times since the last poll.
    public static bool Toggled() => (Interlocked.Exchange(ref _presses, 0) & 1) == 1;

    [StructLayout(LayoutKind.Sequential)]
    struct MSG { public nint hwnd; public uint message; public nuint wParam; public nint lParam; public uint time; public int x, y; public uint lPrivate; }

    [DllImport("user32.dll", SetLastError = true)] static extern bool RegisterHotKey(nint hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] static extern int GetMessage(out MSG msg, nint hWnd, uint min, uint max);
}
