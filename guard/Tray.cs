namespace OtzariaCompanionGuard;

internal sealed unsafe class Tray
{
    public const uint CallbackMsg = 0x8001; // WM_APP + 1
    public const int CmdOpen = 1, CmdPause = 2, CmdExit = 3;

    readonly nint hwnd;
    readonly nint icon;
    public bool Visible { get; private set; }

    public Tray(nint hwnd)
    {
        this.hwnd = hwnd;
        var exe = Environment.ProcessPath ?? "";
        icon = Native.ExtractIconW(Native.GetModuleHandleW(null), exe, 0);
        if (icon == 0) icon = Native.LoadIconW(0, 32512); // IDI_APPLICATION
    }

    void Op(uint op, string tip)
    {
        var d = new NOTIFYICONDATA
        {
            cbSize = (uint)sizeof(NOTIFYICONDATA),
            hWnd = hwnd,
            uID = 1,
            uFlags = Native.NIF_MESSAGE | Native.NIF_ICON | Native.NIF_TIP,
            uCallbackMessage = CallbackMsg,
            hIcon = icon,
        };
        var n = Math.Min(tip.Length, 127);
        for (int i = 0; i < n; i++) d.szTip[i] = tip[i];
        d.szTip[n] = '\0';
        Native.Shell_NotifyIconW(op, ref d);
    }

    public void Show(string tip) { Op(Visible ? Native.NIM_MODIFY : Native.NIM_ADD, tip); Visible = true; }
    public void Modify(string tip) { if (Visible) Op(Native.NIM_MODIFY, tip); }
    public void Hide() { if (Visible) { Op(Native.NIM_DELETE, ""); Visible = false; } }
    public void Readd(string tip) { if (Visible) { Visible = false; Show(tip); } }

    public int ShowMenu(string status, bool paused)
    {
        const uint rtl = 0x2000 | 0x4000; // MF_RIGHTORDER | MF_RIGHTJUSTIFY
        var m = Native.CreatePopupMenu();
        Native.AppendMenuW(m, rtl | 0x1, 0, status);       // greyed status line
        Native.AppendMenuW(m, 0x800, 0, null);              // separator
        Native.AppendMenuW(m, rtl, CmdOpen, "פתח את ההגדרות");
        Native.AppendMenuW(m, rtl, CmdPause, paused ? "המשך פיקוח" : "השהה פיקוח");
        Native.AppendMenuW(m, 0x800, 0, null);
        Native.AppendMenuW(m, rtl, CmdExit, "יציאה");
        Native.GetCursorPos(out var p);
        Native.SetForegroundWindow(hwnd);
        // TPM_RIGHTBUTTON | TPM_RIGHTALIGN | TPM_BOTTOMALIGN | TPM_RETURNCMD
        var cmd = Native.TrackPopupMenu(m, 0x2 | 0x8 | 0x20 | 0x100, p.X, p.Y, 0, hwnd, 0);
        Native.PostMessageW(hwnd, 0, 0, 0);
        Native.DestroyMenu(m);
        return cmd;
    }
}
