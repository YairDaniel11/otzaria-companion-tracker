using System.Diagnostics;

namespace OtzariaCompanionGuard;

internal sealed class Managed
{
    public AppEntry Entry = null!;
    public Process? Proc;
    public bool Ours;       // started by us (vs. already running when we attached)
    public int Restarts;
}

/// <summary>Watches the target process and starts/stops the companion programs. Single-threaded (called from the timer).</summary>
internal sealed class Supervisor
{
    Config cfg = Store.Load();
    DateTime cfgStamp = Store.Stamp();
    bool wasRunning;
    readonly Dictionary<string, Managed> managed = new(StringComparer.OrdinalIgnoreCase);
    HashSet<int>? baseline;
    DateTime? learnAt;

    public bool Paused;
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
            if (c != null) { cfg = c; ConfigChanged = true; }
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
            if (m.Ours && m.Proc != null && Exited(m.Proc))
            {
                if (m.Restarts < cfg.MaxRestarts) { m.Restarts++; m.Proc.Dispose(); Launch(m); }
            }
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
        else Launch(m);
    }

    void Launch(Managed m)
    {
        try
        {
            var path = m.Entry.Path;
            var psi = new ProcessStartInfo(path)
            {
                UseShellExecute = false,
                Arguments = m.Entry.Args ?? "",
                WorkingDirectory = string.IsNullOrWhiteSpace(m.Entry.WorkingDir)
                    ? (System.IO.Path.GetDirectoryName(path) ?? "")
                    : m.Entry.WorkingDir,
            };
            m.Proc = Process.Start(psi);
            m.Ours = m.Proc != null;
        }
        catch
        {
            m.Proc = null;
            m.Ours = false;
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

    void StopAsync(Process? p)
    {
        if (p == null) return;
        int timeout = Math.Clamp(cfg.GracefulTimeoutSeconds, 1, 60) * 1000;
        Task.Run(() =>
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
                if (known.Contains(path)) continue;
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
