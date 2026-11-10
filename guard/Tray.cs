using Microsoft.Win32;

namespace OtzariaCompanionGuard;

internal enum TrayState { Waiting, Running, Paused }

/// <summary>Tray icon + owner-drawn context menu styled like the settings window (warm brown, Material 3 feel).</summary>
internal sealed unsafe class Tray
{
    public const uint CallbackMsg = 0x8001; // WM_APP + 1
    public const int CmdOpen = 1, CmdPause = 2, CmdExit = 3, CmdInfo = 4;
    public const string AppTitle = "מעקב תוכנות נלוות לאוצריא";

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
        d.szTip[n] = 0;
        Native.Shell_NotifyIconW(op, ref d);
    }

    public void Show(string tip) { Op(Visible ? Native.NIM_MODIFY : Native.NIM_ADD, tip); Visible = true; }
    public void Modify(string tip) { if (Visible) Op(Native.NIM_MODIFY, tip); }
    public void Hide() { if (Visible) { Op(Native.NIM_DELETE, ""); Visible = false; } }
    public void Readd(string tip) { if (Visible) { Visible = false; Show(tip); } }

    // ------------------------------------------------------------------ menu

    enum Kind { Header, Item, Sep }
    sealed record Def(Kind Kind, int Id, string Text, string Glyph);

    sealed record Palette(uint Bg, uint Card, uint Outline, uint Text, uint Muted, uint Primary, uint Hover, uint OnHover,
                          uint DotRun, uint DotWait, uint DotPause);

    // Colours mirror ui/lib/main.dart (seed 0xFF7F5218). 0xRRGGBB -> COLORREF
    static uint C(int rgb) => (uint)(((rgb & 0xFF) << 16) | (rgb & 0xFF00) | ((rgb >> 16) & 0xFF));

    static readonly Palette Light = new(C(0xF6EDE3), C(0xFFF8F3), C(0xDCC8B2), C(0x1F1B16), C(0x6B5D4F), C(0x7F5218), C(0xFADDBB), C(0x3F2706),
                                        C(0x2E8B57), C(0xA29586), C(0xD08A1E));
    static readonly Palette Dark = new(C(0x19120C), C(0x251D15), C(0x52443A), C(0xEEE0D5), C(0xBFAE9F), C(0xF5BB7C), C(0x623F0F), C(0xFFDDB9),
                                       C(0x5FCB8A), C(0x8C7F72), C(0xE8A93C));

    static Def[] defs = Array.Empty<Def>();
    static Palette pal = Light;
    static TrayState state;
    static string subtitle = "";
    static double scale = 1.0;
    static nint fTitle, fText, fSub, fIcon;

    static int S(int v) => (int)Math.Round(v * scale);

    static bool IsDark(string theme)
    {
        if (theme == "dark") return true;
        if (theme == "light") return false;
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return k?.GetValue("AppsUseLightTheme") is int v && v == 0;
        }
        catch { return false; }
    }

    public int ShowMenu(string status, TrayState st, bool paused, string theme)
    {
        const uint rtl = 0x2000 | 0x4000;                // MF_RIGHTORDER | MF_RIGHTJUSTIFY
        const uint own = 0x100, dis = 0x2;               // MF_OWNERDRAW, MF_DISABLED

        pal = IsDark(theme) ? Dark : Light;
        state = st;
        subtitle = status;
        var dpi = Native.GetDpiForWindow(hwnd);
        scale = (dpi == 0 ? 96 : dpi) / 96.0;

        defs = new[]
        {
            new Def(Kind.Header, CmdInfo, AppTitle, ""),
            new Def(Kind.Sep, 0, "", ""),
            new Def(Kind.Item, CmdOpen, "פתח את ההגדרות", "\uE713"),
            new Def(Kind.Item, CmdPause, paused ? "המשך פיקוח" : "השהה פיקוח", paused ? "\uE768" : "\uE769"),
            new Def(Kind.Sep, 0, "", ""),
            new Def(Kind.Item, CmdExit, "יציאה", "\uE7E8"),
        };

        fTitle = Native.CreateFontW(-S(15), 0, 0, 0, 700, 0, 0, 0, 1, 0, 0, 5, 0, "Segoe UI");
        fText = Native.CreateFontW(-S(14), 0, 0, 0, 400, 0, 0, 0, 1, 0, 0, 5, 0, "Segoe UI");
        fSub = Native.CreateFontW(-S(12), 0, 0, 0, 400, 0, 0, 0, 1, 0, 0, 5, 0, "Segoe UI");
        fIcon = Native.CreateFontW(-S(16), 0, 0, 0, 400, 0, 0, 0, 1, 0, 0, 5, 0, "Segoe MDL2 Assets");

        var m = Native.CreatePopupMenu();
        var bg = Native.CreateSolidBrush(pal.Bg);
        var mi = new MENUINFO { cbSize = (uint)sizeof(MENUINFO), fMask = 0x2 /* MIM_BACKGROUND */, hbrBack = bg };
        Native.SetMenuInfo(m, ref mi);

        for (int i = 0; i < defs.Length; i++)
        {
            var d = defs[i];
            // header + separators are disabled: they never highlight and never return a command
            uint flags = rtl | own | (d.Kind == Kind.Item ? 0u : dis);
            Native.AppendMenuOwner(m, flags, (nuint)d.Id, i);
        }

        Native.GetCursorPos(out var p);
        Native.SetForegroundWindow(hwnd);
        // TPM_RIGHTBUTTON | TPM_RIGHTALIGN | TPM_BOTTOMALIGN | TPM_RETURNCMD
        var cmd = Native.TrackPopupMenu(m, 0x2 | 0x8 | 0x20 | 0x100, p.X, p.Y, 0, hwnd, 0);
        Native.PostMessageW(hwnd, 0, 0, 0);
        Native.DestroyMenu(m);

        Native.DeleteObject(bg);
        Native.DeleteObject(fTitle); Native.DeleteObject(fText); Native.DeleteObject(fSub); Native.DeleteObject(fIcon);
        return cmd;
    }

    public static void Measure(nint lParam)
    {
        var m = (MEASUREITEMSTRUCT*)lParam;
        if (m->CtlType != 1 /* ODT_MENU */ || m->itemData >= (nuint)defs.Length) return;
        m->itemWidth = (uint)S(260);
        m->itemHeight = (uint)S(defs[(int)m->itemData].Kind switch { Kind.Header => 62, Kind.Sep => 9, _ => 36 });
    }

    public static void Draw(nint lParam)
    {
        var d = (DRAWITEMSTRUCT*)lParam;
        if (d->CtlType != 1 || d->itemData >= (nuint)defs.Length) return;
        var def = defs[(int)d->itemData];
        var dc = d->hDC;
        var rc = d->rcItem;

        var bg = Native.CreateSolidBrush(pal.Bg);
        Native.FillRect(dc, ref rc, bg);
        Native.DeleteObject(bg);
        Native.SetBkMode(dc, 1 /* TRANSPARENT */);

        const uint rtlText = Native.DT_RIGHT | Native.DT_RTLREADING | Native.DT_VCENTER | Native.DT_SINGLELINE | Native.DT_NOPREFIX | Native.DT_END_ELLIPSIS;

        switch (def.Kind)
        {
            case Kind.Sep:
            {
                var y = (rc.Top + rc.Bottom) / 2;
                var line = new RECT { Left = rc.Left + S(12), Right = rc.Right - S(12), Top = y, Bottom = y + 1 };
                var br = Native.CreateSolidBrush(pal.Outline);
                Native.FillRect(dc, ref line, br);
                Native.DeleteObject(br);
                break;
            }

            case Kind.Header:
            {
                var card = new RECT { Left = rc.Left + S(6), Top = rc.Top + S(4), Right = rc.Right - S(6), Bottom = rc.Bottom - S(4) };
                Round(dc, card, pal.Card, pal.Outline, S(10));

                // status dot (right side, RTL)
                var dotColor = state switch { TrayState.Running => pal.DotRun, TrayState.Paused => pal.DotPause, _ => pal.DotWait };
                int ds = S(10), cy = (card.Top + card.Bottom) / 2;
                var dotR = card.Right - S(14);
                var br = Native.CreateSolidBrush(dotColor);
                var pen = Native.CreatePen(0, 1, dotColor);
                var ob = Native.SelectObject(dc, br); var op = Native.SelectObject(dc, pen);
                Native.Ellipse(dc, dotR - ds, cy - ds / 2, dotR, cy + ds / 2);
                Native.SelectObject(dc, ob); Native.SelectObject(dc, op);
                Native.DeleteObject(br); Native.DeleteObject(pen);

                var tr = card.Right - S(14) - ds - S(10);
                var title = new RECT { Left = card.Left + S(12), Right = tr, Top = card.Top + S(7), Bottom = card.Top + S(28) };
                Native.SelectObject(dc, fTitle);
                Native.SetTextColor(dc, pal.Text);                      // title: always dark/readable
                Native.DrawTextW(dc, def.Text, -1, ref title, rtlText);

                var sub = new RECT { Left = card.Left + S(12), Right = tr, Top = card.Top + S(28), Bottom = card.Bottom - S(6) };
                Native.SelectObject(dc, fSub);
                Native.SetTextColor(dc, state == TrayState.Running ? pal.Primary : pal.Muted);   // status: changes with state
                Native.DrawTextW(dc, subtitle, -1, ref sub, rtlText);
                break;
            }

            default:
            {
                bool sel = (d->itemState & 0x1 /* ODS_SELECTED */) != 0;
                var pill = new RECT { Left = rc.Left + S(6), Top = rc.Top + S(2), Right = rc.Right - S(6), Bottom = rc.Bottom - S(2) };
                if (sel) Round(dc, pill, pal.Hover, pal.Hover, S(8));

                var ic = new RECT { Right = pill.Right - S(12), Top = pill.Top, Bottom = pill.Bottom };
                ic.Left = ic.Right - S(20);
                Native.SelectObject(dc, fIcon);
                Native.SetTextColor(dc, sel ? pal.OnHover : pal.Primary);
                Native.DrawTextW(dc, def.Glyph, -1, ref ic, Native.DT_CENTER | Native.DT_VCENTER | Native.DT_SINGLELINE | Native.DT_NOPREFIX);

                var tx = new RECT { Left = pill.Left + S(12), Right = ic.Left - S(10), Top = pill.Top, Bottom = pill.Bottom };
                Native.SelectObject(dc, fText);
                Native.SetTextColor(dc, sel ? pal.OnHover : pal.Text);
                Native.DrawTextW(dc, def.Text, -1, ref tx, rtlText);
                break;
            }
        }
    }

    static void Round(nint dc, RECT r, uint fill, uint border, int radius)
    {
        var br = Native.CreateSolidBrush(fill);
        var pen = Native.CreatePen(0, 1, border);
        var ob = Native.SelectObject(dc, br); var op = Native.SelectObject(dc, pen);
        Native.RoundRect(dc, r.Left, r.Top, r.Right, r.Bottom, radius * 2, radius * 2);
        Native.SelectObject(dc, ob); Native.SelectObject(dc, op);
        Native.DeleteObject(br); Native.DeleteObject(pen);
    }
}
