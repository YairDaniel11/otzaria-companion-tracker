using System.Text;
using System.Text.Json;

namespace OtzariaCompanionGuard;

internal static class Program
{
    static string? ArgValue(string[] a, string name)
    {
        var i = Array.IndexOf(a, name);
        return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
    }

    static int Main(string[] args)
    {
        try
        {
            // CLI mode used by the settings UI: write a JSON list to --out and exit.
            var mode = args.Contains("--list-running") ? "running"
                     : args.Contains("--list-installed") ? "installed"
                     : args.Contains("--list-related") ? "related" : null;
            if (mode != null) return RunList(mode, ArgValue(args, "--out"));

            bool first = !Store.Exists;

            var auto = ArgValue(args, "--autostart");
            if (auto != null)
            {
                var c = Store.Load();
                c.Autostart = auto == "on";
                Store.Save(c);
            }

            bool show = args.Contains("--show");

            // Single instance: a second launch only opens/focuses the settings window.
            using var mutex = new Mutex(true, @"Local\OtzariaCompanionTracker.Guard", out var created);
            if (!created)
            {
                if (show) Ui.Open();
                return 0;
            }

            if (!Store.Exists) Store.Save(new Config());
            App.Run(show || first);
            GC.KeepAlive(mutex);
        }
        catch { }
        return 0;
    }

    static int RunList(string mode, string? outPath)
    {
        string json;
        try
        {
            var list = mode switch
            {
                "running" => Discovery.Running(),
                "installed" => Discovery.Installed(),
                _ => Discovery.Related(),
            };
            json = JsonSerializer.Serialize(list, AppJson.Default.ListProgramInfo);
        }
        catch { json = "[]"; }

        if (outPath != null) File.WriteAllText(outPath, json, new UTF8Encoding(false));
        else Console.Out.Write(json);
        return 0;
    }
}
