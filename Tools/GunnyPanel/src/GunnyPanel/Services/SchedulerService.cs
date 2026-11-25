using GunnyPanel.Jobs;

namespace GunnyPanel.Services;

/// <summary>Scheduler don gian: moi ngay tai gio cau hinh se enqueue job dong bo XML.</summary>
public sealed class SchedulerService : BackgroundService
{
    private readonly JobQueue _queue;
    private readonly IConfiguration _cfg;
    private readonly GunnySuite.Core.AuditLog _audit;

    public SchedulerService(JobQueue queue, IConfiguration cfg, GunnySuite.Core.AuditLog audit)
    {
        _queue = queue;
        _cfg = cfg;
        _audit = audit;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!string.Equals(_cfg["Scheduler:Enabled"], "true", StringComparison.OrdinalIgnoreCase))
            return;

        var at = _cfg["Scheduler:SyncXmlDailyAt"] ?? "03:00";
        if (!TimeSpan.TryParse(at, out var time)) time = TimeSpan.FromHours(3);

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTime.Now;
            var next = now.Date + time;
            if (next <= now) next = next.AddDays(1);
            try { await Task.Delay(next - now, stoppingToken); }
            catch (TaskCanceledException) { break; }
            if (stoppingToken.IsCancellationRequested) break;

            var job = _queue.Enqueue("sync-xml", "Sync XML VNG (scheduler)", new Dictionary<string, string>
            {
                ["source"] = _cfg["Vng:RequestSource"] ?? "https://quest.gn.zing.vn/",
                ["dest"] = _cfg["Paths:MirrorDir"] ?? "",
                ["overwrite"] = "true"
            }, "scheduler");
            _audit.Write("scheduler", "job.enqueue", $"sync-xml {job.Id} (daily {at})");
        }
    }
}
