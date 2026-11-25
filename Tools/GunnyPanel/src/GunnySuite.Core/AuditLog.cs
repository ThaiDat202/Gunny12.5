using System.Text.Json;

namespace GunnySuite.Core;

public sealed class AuditEntry
{
    public DateTime Time { get; set; }
    public string User { get; set; } = "";
    public string Action { get; set; } = "";
    public string Detail { get; set; } = "";
}

/// <summary>Audit log dang JSON-lines (moi ngay 1 file).</summary>
public sealed class AuditLog
{
    private readonly string _dir;
    private readonly object _lock = new();

    public AuditLog(string dir)
    {
        _dir = dir;
        try { Directory.CreateDirectory(dir); } catch { }
    }

    public void Write(string user, string action, string detail = "")
    {
        try
        {
            var line = JsonSerializer.Serialize(new AuditEntry { Time = DateTime.Now, User = user, Action = action, Detail = detail });
            lock (_lock)
                File.AppendAllText(Path.Combine(_dir, $"audit-{DateTime.Now:yyyyMMdd}.log"), line + Environment.NewLine);
        }
        catch
        {
            // audit khong duoc phep lam hong nghiep vu
        }
    }

    public IReadOnlyList<AuditEntry> Tail(int max = 300)
    {
        var list = new List<AuditEntry>();
        try
        {
            var files = Directory.GetFiles(_dir, "audit-*.log")
                .OrderByDescending(f => f, StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .ToList();
            foreach (var file in files)
            {
                foreach (var line in File.ReadLines(file).Reverse())
                {
                    try
                    {
                        var entry = JsonSerializer.Deserialize<AuditEntry>(line);
                        if (entry != null) list.Add(entry);
                    }
                    catch { }
                    if (list.Count >= max) return list;
                }
            }
        }
        catch { }
        return list;
    }
}
