using System.Diagnostics;

namespace OtzariaCompanionGuard;

internal sealed class Managed
{
    public AppEntry Entry = null!;
    public Process? Proc;
    public bool Ours;       // started by us (vs. already running when we attached)
    public int Restarts;
    public DateTime StartedAt;
    public DateTime NextTry;   // earliest time for a restart attempt
    public bool GaveUp;        // one-shot / failing program: never launch again until Otzaria restarts
}

/// <summary>Watches the target process and starts/stops the companion programs. Single-threaded (called from the timer).</summary>
internal sealed class Supervisor
{
    Config cfg = Store.Load();
    DateTime cfgStamp = Store.Stamp();
    bool wasRunning;
    readonly Dictionary<string, Managed> managed = new(StringComparer.OrdinalIgnoreCase);
    static readonly TimeSpan QuickExit = TimeSpan.FromSeconds(20);
    HashSet<int>? baseline;
    DateTime? learnAt;

    public bool Paused;

    public Supervisor() { Paused = cfg.Paused; }

    /// <summary>Pause/resume and remember the choice in config.json (so it survives a restart).</summary>
    public void SetPaused(bool value)
    {
        Paused = value;
        try
        {
            var c = Store.TryLoad();          // re-read: never overwrite a newer edit made by the settings UI
            if (c == null) return;            // unreadable file: don't replace it with defaults
            c.Paused = value;
            Store.Save(c);
            cfg = c;
            cfgStamp = Store.Stamp();
            ConfigChanged = true;
        }
        catch { }
    }

    /// <summary>Called when the guard exits: close, and wait for, the companions this guard started itself.</summary>
    public void Shutdown()
    {
        var tasks = managed.Values.Where(m => m.Ours && m.Proc != null).Select(m => StopAsync(m.Proc)).ToArray();
        managed.Clear();
        try { Task.WaitAll(tasks, TimeSpan.FromSeconds(Math.Clamp(cfg.GracefulTimeoutSeconds, 1, 60) + 3)); } catch { }
    }
    public bool ConfigChanged;
    public Config Cfg => cfg;
    public bool TargetRunning => wasRunning;
    public int ActiveCount => managed.Values.Count(m => { try { return m.Proc != null && !m.Proc.HasExited; } catch { return false; } });

    string TargetName
    {
        get
        {
            var n = cfg.TargetProcess.Trim();
            return n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? n[..^4] : n;
        }
    }

    public void Tick()
    {
        var st = Store.Stamp();
        if (st != cfgStamp)
        {
            cfgStamp = st;
            var c = Store.TryLoad();
            if (c != null) { cfg = c; Paused = c.Paused; ConfigChanged = true; }
        }
        if (Paused) return;

        bool running = IsTargetRunning();
        if (running && !wasRunning)
        {
            wasRunning = true;
            managed.Clear();
            if (cfg.Learning && baseline != null) learnAt = DateTime.UtcNow.AddSeconds(20);
            Reconcile();
        }
        else if (!running && wasRunning)
        {
            wasRunning = false;
            learnAt = null;
            StopAll();
        }
        else if (running)
        {
            Reconcile();
            if (learnAt != null && DateTime.UtcNow >= learnAt) { learnAt = null; Learn(); }
        }
        else if (cfg.Learning)
        {
            baseline = Native.Snapshot().Select(e => e.Pid).ToHashSet();
        }
    }

    bool IsTargetRunning()
    {
        var ps = Process.GetProcessesByName(TargetName);
        var r = ps.Length > 0;
        foreach (var p in ps) p.Dispose();
        return r;
    }

    static bool Exited(Process p) { try { return p.HasExited; } catch { return false; } }

    void Reconcile()
    {
        var desired = cfg.Apps
            .Where(a => a.Enabled && !string.IsNullOrWhiteSpace(a.Path))
            .GroupBy(a => a.Path, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        foreach (var a in desired)
        {
            if (!managed.TryGetValue(a.Path, out var m))
            {
                m = new Managed { Entry = a };
                managed[a.Path] = m;
                Attach(m);
                continue;
            }
            m.Entry = a;
            if (m.GaveUp || !m.Ours) continue;

            var now = DateTime.UtcNow;
            if (m.Proc != null && Exited(m.Proc))
            {
                var ranFor = now - m.StartedAt;
                m.Proc.Dispose();
                m.Proc = null;

                // A stub / single-instance program may have handed over to a process that is still alive: attach to it.
                var alive = FindByPath(a.Path);
                if (alive != null) { m.Proc = alive; m.Ours = false; continue; }

                // Quit almost immediately = one-shot tool, helper or failing program. Restarting it would only
                // flash a window every couple of seconds, so stop trying.
                if (ranFor < QuickExit || m.Restarts >= cfg.MaxRestarts) { m.GaveUp = true; continue; }

                m.Restarts++;
                m.NextTry = now.AddSeconds(5 * m.Restarts);   // back-off: 5s, 10s, 15s...
            }
            if (m.Proc == null && now >= m.NextTry) Launch(m);
        }

        var removed = managed.Keys.Where(k => !desired.Any(d => d.Path.Equals(k, StringComparison.OrdinalIgnoreCase))).ToList();
        foreach (var k in removed)
        {
            var m = managed[k];
            managed.Remove(k);
            Release(m);
        }
    }

    void Attach(Managed m)
    {
        var existing = FindByPath(m.Entry.Path);
        if (existing != null) { m.Proc = existing; m.Ours = false; }
        else if (Helpers.IsHelper(m.Entry.Path)) { m.Ours = false; m.GaveUp = true; }   // Otzaria starts these itself
        else Launch(m);
    }

    void Launch(Managed m)
    {
        try
        {
            var path = m.Entry.Path;
            if (!File.Exists(path)) { m.Proc = null; m.Ours = false; m.GaveUp = true; return; }
            var psi = new ProcessStartInfo(path)
            {
                UseShellExecute = false,
                CreateNoWindow = true,      // console programs must not pop up a black window
                Arguments = m.Entry.Args ?? "",
                WorkingDirectory = string.IsNullOrWhiteSpace(m.Entry.WorkingDir)
                    ? (System.IO.Path.GetDirectoryName(path) ?? "")
                    : m.Entry.WorkingDir,
            };
            m.Proc = Process.Start(psi);
            m.Ours = m.Proc != null;
            m.StartedAt = DateTime.UtcNow;
        }
        catch
        {
            m.Proc = null;
            m.Ours = false;
            m.GaveUp = true;
        }
    }

    static Process? FindByPath(string path)
    {
        try
        {
            var full = System.IO.Path.GetFullPath(path);
            Process? found = null;
            foreach (var p in Process.GetProcessesByName(System.IO.Path.GetFileNameWithoutExtension(full)))
            {
                var pp = Native.GetPath(p.Id);
                if (found == null && pp != null && string.Equals(System.IO.Path.GetFullPath(pp), full, StringComparison.OrdinalIgnoreCase)) found = p;
                else p.Dispose();
            }
            return found;
        }
        catch { return null; }
    }

    void Release(Managed m)
    {
        if (m.Ours || !cfg.LeaveRunning) StopAsync(m.Proc);
        else m.Proc?.Dispose();
    }

    void StopAll()
    {
        foreach (var m in managed.Values) Release(m);
        managed.Clear();
    }

    Task StopAsync(Process? p)
    {
        if (p == null) return Task.CompletedTask;
        int timeout = Math.Clamp(cfg.GracefulTimeoutSeconds, 1, 60) * 1000;
        return Task.Run(() =>
        {
            try
            {
                if (p.HasExited) return;
                // Graceful close first; kill (with child processes) if it doesn't exit in time.
                if (p.CloseMainWindow() && p.WaitForExit(timeout)) return;
                if (!p.HasExited) p.Kill(true);
            }
            catch { }
            finally { try { p.Dispose(); } catch { } }
        });
    }

    /// <summary>Learning mode: remember which new processes show up shortly after Otzaria starts.</summary>
    void Learn()
    {
        try
        {
            if (baseline == null) return;
            var win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var known = cfg.Apps.Select(a => a.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var self = AppContext.BaseDirectory;
            var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var e in Native.Snapshot())
            {
                if (baseline.Contains(e.Pid)) continue;
                var path = Native.GetPath(e.Pid);
                if (path == null) continue;
                if (path.StartsWith(win, StringComparison.OrdinalIgnoreCase)) continue;
                if (path.StartsWith(self, StringComparison.OrdinalIgnoreCase)) continue;
                if (known.Contains(path) || Helpers.IsHelper(path)) continue;
                if (System.IO.Path.GetFileNameWithoutExtension(path).Equals(TargetName, StringComparison.OrdinalIgnoreCase)) continue;
                found.Add(path);
            }
            if (found.Count == 0) return;

            var list = Store.LoadLearned();
            foreach (var p in found)
            {
                var l = list.FirstOrDefault(x => x.Path.Equals(p, StringComparison.OrdinalIgnoreCase));
                if (l == null) list.Add(l = new LearnedEntry { Path = p });
                l.Count++;
                l.LastSeen = DateTime.UtcNow.ToString("o");
            }
            Store.SaveLearned(list);
        }
        catch { }
    }
}

/// <summary>Executables that belong to Otzaria / the runtime and are started by Otzaria itself when needed
/// (crash reporters, updaters, the portable-disk launcher...). They are not companions: launching them from here
/// opens windows ("Otzaria not found") or flashes a console.</summary>
internal static class Helpers
{
    static readonly string[] Names =
    {
        "crashpad_handler", "createdump", "otzaria_launcher", "otzaria_updater", "zstd",
        "msedgewebview2", "msedge", "msedgewebview2_installer", "vc_redist", "dotnet", "conhost", "cmd", "powershell",
    };

    public static bool IsHelper(string path)
    {
        var n = System.IO.Path.GetFileNameWithoutExtension(path);
        if (n.StartsWith("unins", StringComparison.OrdinalIgnoreCase)) return true;
        return Names.Any(x => n.Equals(x, StringComparison.OrdinalIgnoreCase));
    }
}
