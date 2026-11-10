using System.Runtime.InteropServices;

namespace OtzariaCompanionGuard;

[StructLayout(LayoutKind.Sequential)]
internal struct POINT { public int X, Y; }

[StructLayout(LayoutKind.Sequential)]
internal struct MSG
{
    public nint hwnd; public uint message; public nint wParam; public nint lParam; public uint time; public POINT pt;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WNDCLASSEX
{
    public uint cbSize, style;
    public nint lpfnWndProc;
    public int cbClsExtra, cbWndExtra;
    public nint hInstance, hIcon, hCursor, hbrBackground, lpszMenuName, lpszClassName, hIconSm;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct NOTIFYICONDATA
{
    public uint cbSize;
    public nint hWnd;
    public uint uID, uFlags, uCallbackMessage;
    public nint hIcon;
    public fixed char szTip[128];
    public uint dwState, dwStateMask;
    public fixed char szInfo[256];
    public uint uTimeoutOrVersion;
    public fixed char szInfoTitle[64];
    public uint dwInfoFlags;
    public Guid guidItem;
    public nint hBalloonIcon;
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct PROCESSENTRY32
{
    public uint dwSize, cntUsage, th32ProcessID;
    public nuint th32DefaultHeapID;
    public uint th32ModuleID, cntThreads, th32ParentProcessID;
    public int pcPriClassBase;
    public uint dwFlags;
    public fixed char szExeFile[260];
}

internal readonly record struct ProcEntry(int Pid, int Ppid, string Name);

internal static unsafe class Native
{
    public const uint WM_DESTROY = 0x0002, WM_TIMER = 0x0113, WM_LBUTTONUP = 0x0202, WM_RBUTTONUP = 0x0205, WM_CONTEXTMENU = 0x007B;
    public const uint NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2;
    public const uint NIF_MESSAGE = 1, NIF_ICON = 2, NIF_TIP = 4;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern ushort RegisterClassExW(ref WNDCLASSEX wc);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern nint CreateWindowExW(uint ex, string cls, string title, uint style, int x, int y, int w, int h, nint parent, nint menu, nint inst, nint param);
    [DllImport("user32.dll")] public static extern nint DefWindowProcW(nint h, uint m, nint w, nint l);
    [DllImport("user32.dll")] public static extern int GetMessageW(out MSG msg, nint h, uint min, uint max);
    [DllImport("user32.dll")] public static extern bool TranslateMessage(ref MSG msg);
    [DllImport("user32.dll")] public static extern nint DispatchMessageW(ref MSG msg);
    [DllImport("user32.dll")] public static extern void PostQuitMessage(int code);
    [DllImport("user32.dll")] public static extern nint SetTimer(nint h, nint id, uint ms, nint proc);
    [DllImport("user32.dll")] public static extern bool KillTimer(nint h, nint id);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern uint RegisterWindowMessageW(string s);
    [DllImport("user32.dll")] public static extern nint CreatePopupMenu();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool AppendMenuW(nint menu, uint flags, nuint id, string? item);
    [DllImport("user32.dll")] public static extern int TrackPopupMenu(nint menu, uint flags, int x, int y, int reserved, nint hwnd, nint rect);
    [DllImport("user32.dll")] public static extern bool DestroyMenu(nint menu);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT p);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(nint h);
    [DllImport("user32.dll")] public static extern bool ShowWindow(nint h, int cmd);
    [DllImport("user32.dll")] public static extern bool PostMessageW(nint h, uint m, nint w, nint l);
    [DllImport("user32.dll")] public static extern nint LoadIconW(nint inst, nint name);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] public static extern bool Shell_NotifyIconW(uint msg, ref NOTIFYICONDATA data);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] public static extern nint ExtractIconW(nint inst, string file, uint idx);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] public static extern nint GetModuleHandleW(string? name);
    [DllImport("kernel32.dll")] static extern nint OpenProcess(uint access, int inherit, uint pid);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(nint h);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool QueryFullProcessImageNameW(nint h, uint flags, char* buf, ref uint size);
    [DllImport("kernel32.dll")] static extern nint CreateToolhelp32Snapshot(uint flags, uint pid);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool Process32FirstW(nint snap, ref PROCESSENTRY32 e);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool Process32NextW(nint snap, ref PROCESSENTRY32 e);

    /// <summary>Full image path of a process, or null if inaccessible. Cheap (no module enumeration).</summary>
    public static string? GetPath(int pid)
    {
        var h = OpenProcess(0x1000 /* PROCESS_QUERY_LIMITED_INFORMATION */, 0, (uint)pid);
        if (h == 0) return null;
        try
        {
            char* buf = stackalloc char[2048];
            uint size = 2048;
            if (QueryFullProcessImageNameW(h, 0, buf, ref size)) return new string(buf, 0, (int)size);
            return null;
        }
        finally { CloseHandle(h); }
    }

    /// <summary>Lightweight process list (pid, parent pid, exe file name).</summary>
    public static List<ProcEntry> Snapshot()
    {
        var res = new List<ProcEntry>();
        var h = CreateToolhelp32Snapshot(0x2 /* TH32CS_SNAPPROCESS */, 0);
        if (h == 0 || h == -1) return res;
        try
        {
            var e = new PROCESSENTRY32 { dwSize = (uint)sizeof(PROCESSENTRY32) };
            if (Process32FirstW(h, ref e))
            {
                do { res.Add(new ProcEntry((int)e.th32ProcessID, (int)e.th32ParentProcessID, new string(e.szExeFile))); }
                while (Process32NextW(h, ref e));
            }
        }
        finally { CloseHandle(h); }
        return res;
    }
}
