using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace OtzariaCompanionGuard;

/// <summary>Opens (or focuses) the settings window.</summary>
internal static class Ui
{
    const string ExeName = "otzaria_companion_settings";

    static string? Exe()
    {
        var b = AppContext.BaseDirectory;
        foreach (var c in new[] { System.IO.Path.Combine(b, "ui", ExeName + ".exe"), System.IO.Path.Combine(b, ExeName + ".exe") })
            if (File.Exists(c)) return c;
        return null;
    }

    public static void Open()
    {
        try
        {
            var procs = Process.GetProcessesByName(ExeName);
            bool running = procs.Length > 0;
            foreach (var p in procs)
            {
                var h = p.MainWindowHandle;
                if (h != 0) { Native.ShowWindow(h, 9 /* SW_RESTORE */); Native.SetForegroundWindow(h); }
                p.Dispose();
            }
            if (running) return;

            var exe = Exe();
            if (exe != null)
                Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = System.IO.Path.GetDirectoryName(exe)! });
        }
        catch { }
    }
}

internal static unsafe class App
{
    static Supervisor sup = null!;
    static Tray tray = null!;
    static nint hwnd;
    static uint taskbarCreated;
    static string lastTip = "";

    public static void Run(bool openUi)
    {
        sup = new Supervisor();
        SyncAutostart(sup.Cfg.Autostart);

        const string className = "OtzariaCompanionGuardWnd";
        var hInst = Native.GetModuleHandleW(null);
        var wc = new WNDCLASSEX
        {
            cbSize = (uint)sizeof(WNDCLASSEX),
            lpfnWndProc = (nint)(delegate* unmanaged[Stdcall]<nint, uint, nint, nint, nint>)&WndProc,
            hInstance = hInst,
            lpszClassName = Marshal.StringToHGlobalUni(className),
        };
        Native.RegisterClassExW(ref wc);
        hwnd = Native.CreateWindowExW(0, className, "", 0, 0, 0, 0, 0, 0, 0, hInst, 0);
        taskbarCreated = Native.RegisterWindowMessageW("TaskbarCreated");
        tray = new Tray(hwnd);

        OnTick();
        Native.SetTimer(hwnd, 1, 2000, 0);
        if (openUi) Ui.Open();

        while (Native.GetMessageW(out var msg, 0, 0, 0) > 0)
        {
            Native.TranslateMessage(ref msg);
            Native.DispatchMessageW(ref msg);
        }
    }

    static string Tip()
    {
        if (sup.Paused) return "מעקב תוכנות נלוות לאוצריא - מושהה";
        return sup.TargetRunning
            ? $"אוצריא פועלת · {sup.ActiveCount} תוכנות נלוות"
            : "ממתין לפתיחת אוצריא";
    }

    static void ApplyTray()
    {
        if (sup.Cfg.ShowTray)
        {
            var tip = Tip();
            if (!tray.Visible) tray.Show(tip);
            else if (tip != lastTip) tray.Modify(tip);
            lastTip = tip;
        }
        else tray.Hide();
    }

    static void OnTick()
    {
        try
        {
            sup.Tick();
            if (sup.ConfigChanged) { sup.ConfigChanged = false; SyncAutostart(sup.Cfg.Autostart); }
            ApplyTray();
        }
        catch { }
    }

    static void SyncAutostart(bool on)
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            if (k == null) return;
            if (on) k.SetValue("OtzariaCompanionTracker", $"\"{Environment.ProcessPath}\" --background");
            else k.DeleteValue("OtzariaCompanionTracker", false);
        }
        catch { }
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    static nint WndProc(nint h, uint msg, nint wParam, nint lParam)
    {
        try
        {
            if (msg == Native.WM_TIMER) { OnTick(); return 0; }

            if (msg == Tray.CallbackMsg)
            {
                var ev = (uint)((long)lParam & 0xFFFF);
                if (ev == Native.WM_LBUTTONUP) Ui.Open();
                else if (ev == Native.WM_RBUTTONUP || ev == Native.WM_CONTEXTMENU)
                {
                    var status = Tip();
                    switch (tray.ShowMenu(status, sup.Paused))
                    {
                        case Tray.CmdOpen: Ui.Open(); break;
                        case Tray.CmdPause: sup.Paused = !sup.Paused; ApplyTray(); break;
                        case Tray.CmdExit:
                            tray.Hide();
                            Native.KillTimer(hwnd, 1);
                            Native.PostQuitMessage(0);
                            break;
                    }
                }
                return 0;
            }

            if (taskbarCreated != 0 && msg == taskbarCreated) { tray.Readd(Tip()); return 0; }
            if (msg == Native.WM_DESTROY) { Native.PostQuitMessage(0); return 0; }
        }
        catch { }
        return Native.DefWindowProcW(h, msg, wParam, lParam);
    }
}
