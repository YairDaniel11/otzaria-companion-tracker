using System.Diagnostics;
using Microsoft.Win32;

namespace OtzariaCompanionGuard;

/// <summary>Builds the program lists the settings UI shows (invoked via --list-running / --list-installed / --list-related).</summary>
internal static class Discovery
{
    static readonly string WinDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

    static bool IsSystem(string p) => p.StartsWith(WinDir, StringComparison.OrdinalIgnoreCase);
    static bool IsSelf(string p) => p.StartsWith(AppContext.BaseDirectory, StringComparison.OrdinalIgnoreCase);
    static bool MatchesOtzaria(string s) =>
        s.Contains("otzaria", StringComparison.OrdinalIgnoreCase) || s.Contains("אוצריא", StringComparison.OrdinalIgnoreCase);

    static string Describe(string path)
    {
        try
        {
            var d = FileVersionInfo.GetVersionInfo(path).FileDescription;
            if (!string.IsNullOrWhiteSpace(d)) return d.Trim();
        }
        catch { }
        return System.IO.Path.GetFileNameWithoutExtension(path);
    }

    // ---------- A. running processes ----------
    public static List<ProgramInfo> Running()
    {
        var map = new Dictionary<string, ProgramInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                var path = Native.GetPath(p.Id);
                if (path == null || IsSystem(path) || IsSelf(path) || Helpers.IsHelper(path)) continue;
                var mb = p.WorkingSet64 / 1048576.0;
                if (map.TryGetValue(path, out var e)) e.MemoryMb = Math.Round(e.MemoryMb + mb, 1);
                else map[path] = new ProgramInfo { Path = path, Name = Describe(path), MemoryMb = Math.Round(mb, 1) };
            }
            catch { }
            finally { p.Dispose(); }
        }
        return map.Values.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    // ---------- B. installed programs ----------
    public static List<ProgramInfo> Installed()
    {
        var map = new Dictionary<string, ProgramInfo>(StringComparer.OrdinalIgnoreCase);
        void Add(string name, string exe)
        {
            if (!map.ContainsKey(exe)) map[exe] = new ProgramInfo { Name = name, Path = exe };
        }

        var roots = new[]
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall",
        };
        foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            foreach (var r in roots)
            {
                try
                {
                    using var k = hive.OpenSubKey(r);
                    if (k == null) continue;
                    foreach (var sub in k.GetSubKeyNames())
                    {
                        try
                        {
                            using var s = k.OpenSubKey(sub);
                            if (s == null) continue;
                            var name = s.GetValue("DisplayName") as string;
                            if (string.IsNullOrWhiteSpace(name)) continue;
                            if (s.GetValue("SystemComponent") is int sc && sc == 1) continue;
                            var exe = ExeFromIcon(s.GetValue("DisplayIcon") as string)
                                      ?? ExeFromDir(s.GetValue("InstallLocation") as string, name);
                            if (exe != null && !IsSelf(exe)) Add(name.Trim(), exe);
                        }
                        catch { }
                    }
                }
                catch { }
            }

            try
            {
                using var ap = hive.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths");
                if (ap == null) continue;
                foreach (var sub in ap.GetSubKeyNames())
                {
                    try
                    {
                        if (!sub.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
                        using var s = ap.OpenSubKey(sub);
                        var v = (s?.GetValue("") as string)?.Trim().Trim('"');
                        if (!string.IsNullOrEmpty(v)) v = Environment.ExpandEnvironmentVariables(v);
                        if (!string.IsNullOrEmpty(v) && File.Exists(v) && !IsSystem(v) && !IsSelf(v)) Add(Describe(v), v);
                    }
                    catch { }
                }
            }
            catch { }
        }
        return map.Values.OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    static string? ExeFromIcon(string? v)
    {
        if (string.IsNullOrWhiteSpace(v)) return null;
        v = v.Trim();
        if (v.StartsWith('"'))
        {
            var e = v.IndexOf('"', 1);
            if (e > 0) v = v[1..e];
        }
        else
        {
            var c = v.LastIndexOf(',');
            if (c > 0) v = v[..c];
        }
        v = Environment.ExpandEnvironmentVariables(v);
        return v.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(v) && !IsSystem(v) ? v : null;
    }

    static string? ExeFromDir(string? dir, string displayName)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) return null;
            var exes = Directory.EnumerateFiles(dir, "*.exe", SearchOption.TopDirectoryOnly)
                .Where(f =>
                {
                    var n = System.IO.Path.GetFileName(f);
                    return !n.StartsWith("unins", StringComparison.OrdinalIgnoreCase)
                        && !n.StartsWith("uninstall", StringComparison.OrdinalIgnoreCase);
                }).ToList();
            if (exes.Count == 0) return null;
            if (exes.Count == 1) return exes[0];
            var flat = displayName.Replace(" ", "");
            var byName = exes.FirstOrDefault(f =>
            {
                var n = System.IO.Path.GetFileNameWithoutExtension(f);
                return flat.Contains(n, StringComparison.OrdinalIgnoreCase) || n.Contains(flat, StringComparison.OrdinalIgnoreCase);
            });
            return byName ?? exes.OrderByDescending(f => new FileInfo(f).Length).First();
        }
        catch { return null; }
    }

    // ---------- C. programs related to Otzaria ----------
    public static List<ProgramInfo> Related()
    {
        var res = new List<ProgramInfo>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cfg = Store.Load();
        var tname = cfg.TargetProcess.Trim();
        if (tname.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) tname = tname[..^4];

        bool IsTarget(string path) =>
            System.IO.Path.GetFileNameWithoutExtension(path).Equals(tname, StringComparison.OrdinalIgnoreCase);

        void Add(string path, string reason)
        {
            if (IsSystem(path) || IsSelf(path) || IsTarget(path) || Helpers.IsHelper(path)) return;
            if (!seen.Add(path)) return;
            res.Add(new ProgramInfo { Path = path, Name = Describe(path), Reason = reason });
        }

        var snap = Native.Snapshot();
        var targets = snap.Where(e => System.IO.Path.GetFileNameWithoutExtension(e.Name).Equals(tname, StringComparison.OrdinalIgnoreCase)).ToList();
        var dirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var t in targets)
        {
            var p = Native.GetPath(t.Pid);
            var d = p == null ? null : System.IO.Path.GetDirectoryName(p);
            if (!string.IsNullOrEmpty(d)) dirs.Add(d);
        }
        try
        {
            foreach (var i in Installed())
            {
                if (!MatchesOtzaria(i.Name) && !MatchesOtzaria(i.Path)) continue;
                var d = System.IO.Path.GetDirectoryName(i.Path);
                if (!string.IsNullOrEmpty(d)) dirs.Add(d);
            }
        }
        catch { }

        // 1. child processes of Otzaria (all descendants)
        var pids = targets.Select(t => t.Pid).ToHashSet();
        bool grew;
        do
        {
            grew = false;
            foreach (var e in snap)
                if (!pids.Contains(e.Pid) && pids.Contains(e.Ppid)) { pids.Add(e.Pid); grew = true; }
        } while (grew);
        foreach (var pid in pids)
        {
            if (targets.Any(t => t.Pid == pid)) continue;
            var path = Native.GetPath(pid);
            if (path != null) Add(path, "תהליך בן של אוצריא");
        }

        // 2. running processes living in Otzaria's folders or with a matching name
        foreach (var e in snap)
        {
            var path = Native.GetPath(e.Pid);
            if (path == null) continue;
            if (dirs.Any(d => path.StartsWith(d + "\\", StringComparison.OrdinalIgnoreCase))) Add(path, "רצה מתוך תיקיית אוצריא");
            else if (MatchesOtzaria(path)) Add(path, "שם התוכנה קשור לאוצריא");
        }

        // 3. executables inside Otzaria's folders (not running)
        foreach (var d in dirs)
        {
            try
            {
                var opts = new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 3, IgnoreInaccessible = true };
                foreach (var f in Directory.EnumerateFiles(d, "*.exe", opts).Take(60))
                {
                    var n = System.IO.Path.GetFileName(f);
                    if (n.StartsWith("unins", StringComparison.OrdinalIgnoreCase)) continue;
                    Add(f, "קובץ הפעלה בתיקיית אוצריא");
                }
            }
            catch { }
        }

        // 4. learned from previous Otzaria launches
        foreach (var l in Store.LoadLearned().Where(x => x.Count >= 2).OrderByDescending(x => x.Count))
            if (File.Exists(l.Path)) Add(l.Path, $"עלתה יחד עם אוצריא ({l.Count} פעמים)");

        return res;
    }
}
