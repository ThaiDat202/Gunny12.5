using System.Globalization;
using System.Text.Json;
using GunnyPanel.Jobs;
using GunnySuite.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GunnyPanel.Pages;

/// <summary>
/// Trang Rip Flash: mirror toàn bộ flash UI của NPH VNG (gunny.vcdn.vn/flash/) về 1 thư mục.
/// Giao diện riêng (không dùng form chung ở /Jobs) để dễ thao tác + xem tiến độ realtime.
/// </summary>
public sealed class FlashRipModel : PageModel
{
    public const string JobType = "flash-rip";
    public const string DefaultExclude = "2.png\n4.png";
    public const int RecentPageSize = 8;

    private readonly JobQueue _queue;
    private readonly JobStore _store;
    private readonly AuditLog _audit;
    private readonly IConfiguration _cfg;
    private readonly HttpFetcher _http;

    public FlashRipModel(JobQueue queue, JobStore store, AuditLog audit, IConfiguration cfg, HttpFetcher http)
    {
        _queue = queue;
        _store = store;
        _audit = audit;
        _cfg = cfg;
        _http = http;
    }

    [BindProperty] public string Source { get; set; } = "";
    [BindProperty] public string Reference { get; set; } = "";
    [BindProperty] public string Target { get; set; } = "";
    [BindProperty] public string Exclude { get; set; } = DefaultExclude;
    [BindProperty] public string StartMode { get; set; } = "scan";
    [BindProperty] public bool Overwrite { get; set; } = true;
    [BindProperty] public bool AllowInPlace { get; set; }
    [BindProperty] public int MaxFiles { get; set; }
    [BindProperty] public int Concurrency { get; set; } = 8;

    /// <summary>Chỉ Develop/Admin mới được chạy job (viewer/Moderator chỉ xem).</summary>
    public bool CanRun => User.IsInRole("Develop") || User.IsInRole("Admin");

    public JobRecord? ActiveJob { get; private set; }
    public FlashRipReport? LastReport { get; private set; }
    /// <summary>Report của lần RIP THẬT gần nhất (dùng cho mini stats ở hero, kể cả khi đang xem job khác).</summary>
    public FlashRipReport? LastRipReport { get; private set; }
    public List<JobRecord> RecentJobs { get; private set; } = new();
    public string? ReportError { get; private set; }
    public int ReferenceFileCount { get; private set; }
    public long ReferenceBytes { get; private set; }
    public string CdnState { get; private set; } = "unknown"; // ok | bad | unknown
    public string CdnMessage { get; private set; } = "";
    public bool IsRunning => ActiveJob is { Status: JobStatus.Pending or JobStatus.Running };

    /// <summary>Thống kê đọc từ dòng log tiến độ cuối cùng (để panel job đã xong vẫn hiện số đúng).</summary>
    public (string Done, string Total, string Ok, string Miss, string Fail, string Speed) LiveStats { get; private set; }
        = ("0", "0", "0", "0", "0", "—");

    public bool IsScanJob(JobRecord job) =>
        job.Parameters.TryGetValue("dryRun", out var v) && string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);

    public async Task OnGetAsync(Guid? job)
    {
        Source = _cfg["Vng:ResourceSource"] ?? "https://gunny.vcdn.vn/flash/";
        var gameRoot = _cfg["Paths:GameRoot"] ?? "";
        Reference = Path.Combine(gameRoot, "WebSite", "Flash");
        Target = Path.Combine(gameRoot, "WebSite", "FlashVNG");
        Exclude = DefaultExclude;
        MaxFiles = 0;
        Concurrency = 8;

        LoadReferenceStats();
        LoadJobs(job);

        CdnState = "unknown";
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var (ok, message) = await _http.CheckAsync(Source.TrimEnd('/') + "/config.xml", cts.Token);
            CdnState = ok ? "ok" : "bad";
            CdnMessage = message;
        }
        catch (Exception ex)
        {
            CdnState = "bad";
            CdnMessage = ex.GetType().Name + ": " + ex.Message;
        }
    }

    private void LoadReferenceStats()
    {
        try
        {
            if (!Directory.Exists(Reference)) return;
            foreach (var file in Directory.EnumerateFiles(Reference, "*", SearchOption.AllDirectories))
            {
                ReferenceFileCount++;
                try { ReferenceBytes += new FileInfo(file).Length; } catch { }
            }
        }
        catch { }
    }

    private void LoadJobs(Guid? jobId)
    {
        var jobs = _store.List(300).Where(j => j.Type == JobType).ToList();
        RecentJobs = jobs.Take(RecentPageSize).ToList();
        if (jobId.HasValue)
        {
            var explicitJob = _store.Get(jobId.Value);
            if (explicitJob is { Type: JobType }) ActiveJob = explicitJob;
        }
        ActiveJob ??= jobs.FirstOrDefault();
        if (ActiveJob != null)
        {
            LiveStats = ParseLiveStats(ActiveJob.Log);
            if (ActiveJob.Status is JobStatus.Success or JobStatus.Failed or JobStatus.Cancelled)
                LastReport = TryReadReport(ActiveJob);
        }
        // Mini stats ở hero: ưu tiên report của job đang xem; nếu job đó là quét thử (không có report)
        // thì lấy lần RIP THẬT gần nhất đúng thư mục đích mặc định (bỏ qua các lần rip thử vào folder _verify).
        LastRipReport = LastReport;
        if (LastRipReport == null || !SamePath(LastRipReport.Target, Target))
        {
            FlashRipReport? fallback = null;
            foreach (var finished in jobs.Where(j => j.Status == JobStatus.Success && !IsScanJob(j)))
            {
                var rep = TryReadReport(finished);
                if (rep == null) continue;
                fallback ??= rep;
                if (SamePath(rep.Target, Target)) { LastRipReport = rep; break; }
            }
            LastRipReport ??= fallback;
        }
    }

    private static bool SamePath(string? a, string? b)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b)) return false;
            return string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static (string Done, string Total, string Ok, string Miss, string Fail, string Speed) ParseLiveStats(List<string> log)
    {
        for (var i = log.Count - 1; i >= 0; i--)
        {
            var line = log[i];
            var m = System.Text.RegularExpressions.Regex.Match(line,
                @"TAI (\d+)/(\d+) \| OK (\d+) \| 404 (\d+) \| loi (\d+) \| bo qua (\d+) \| ([\d.]+) MB/s");
            var speed = "—";
            if (m.Success)
            {
                if (m.Groups.Count >= 8) speed = m.Groups[7].Value + " MB/s";
            }
            else
            {
                m = System.Text.RegularExpressions.Regex.Match(line,
                    @"QUET (\d+)/(\d+) \| CDN co (\d+) \| 404 (\d+) \| loi (\d+) \| (\d+) req/s");
                if (!m.Success) continue;
                if (m.Groups.Count >= 7) speed = m.Groups[6].Value + " req/s";
            }
            return (m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value, m.Groups[5].Value, speed);
        }
        return ("0", "0", "0", "0", "0", "—");
    }

    private FlashRipReport? TryReadReport(JobRecord job)
    {
        try
        {
            // 1) Ban sao report theo job (data/rip/jobs) — chinh xac nhat, khong bi lan chay sau ghi de.
            var report = TryReadReportFile(JobReportDir, job, "report");
            if (report != null) return report;

            // 2) Fallback report "latest" trong thu muc dich — chi chap nhan neu dung job + dung khoang thoi gian.
            if (!job.Parameters.TryGetValue("target", out var target) || string.IsNullOrWhiteSpace(target)) return null;
            var path = Path.Combine(target, "_rip-report.json");
            if (!System.IO.File.Exists(path)) return null;
            var fromTarget = JsonSerializer.Deserialize<FlashRipReport>(System.IO.File.ReadAllText(path));
            return IsReportOfJob(fromTarget, job) ? fromTarget : null;
        }
        catch (Exception ex)
        {
            ReportError = ex.Message;
            return null;
        }
    }

    private string JobReportDir
    {
        get
        {
            var dataDir = _cfg["Paths:DataDir"] ?? Path.Combine(AppContext.BaseDirectory, "data");
            return Path.Combine(dataDir, "rip", "jobs");
        }
    }

    private static FlashRipReport? TryReadReportFile(string dir, JobRecord job, string kind)
    {
        try
        {
            var path = Path.Combine(dir, job.Id.ToString("N") + "-" + kind + ".json");
            if (!System.IO.File.Exists(path)) return null;
            var report = JsonSerializer.Deserialize<FlashRipReport>(System.IO.File.ReadAllText(path));
            return IsReportOfJob(report, job) ? report : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Report co thuoc dung job nay khong (chan attribution sai khi dung chung target).</summary>
    private static bool IsReportOfJob(FlashRipReport? report, JobRecord job)
    {
        if (report == null) return false;
        if (report.JobId != Guid.Empty) return report.JobId == job.Id;
        // Report cu chua co JobId: chi nhan neu GeneratedAt nam trong khoang job chay (+/- 10s).
        if (!job.StartedAt.HasValue) return false;
        var start = job.StartedAt.Value.AddSeconds(-10);
        var end = (job.FinishedAt ?? job.StartedAt.Value).AddSeconds(10);
        return report.GeneratedAt >= start && report.GeneratedAt <= end;
    }

    public IActionResult OnPostStart()
    {
        var user = User.Identity?.Name ?? "unknown";
        if (!CanRun)
        {
            TempData["FlashError"] = "Chỉ tài khoản role Develop/Admin mới được chạy job Rip Flash (viewer chỉ xem).";
            return RedirectToPage();
        }

        var source = (Source ?? "").Trim();
        var reference = (Reference ?? "").Trim();
        var target = (Target ?? "").Trim();
        var mode = string.Equals(StartMode, "rip", StringComparison.OrdinalIgnoreCase) ? "rip" : "scan";
        var exclude = Exclude ?? "";

        if (!source.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !source.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            TempData["FlashError"] = "Nguồn (Source) phải là URL http(s), ví dụ: https://gunny.vcdn.vn/flash/";
            return RedirectToPage();
        }
        if (!Directory.Exists(reference))
        {
            TempData["FlashError"] = "Không tìm thấy thư mục danh sách (Reference): " + reference;
            return RedirectToPage();
        }
        if (!Path.IsPathRooted(target) || target.TrimEnd('\\', '/').Length <= 2)
        {
            TempData["FlashError"] = "Thư mục đích (Target) phải là đường dẫn tuyệt đối hợp lệ: " + target;
            return RedirectToPage();
        }
        if (mode == "rip" &&
            string.Equals(Path.GetFullPath(target), Path.GetFullPath(reference), StringComparison.OrdinalIgnoreCase) &&
            !AllowInPlace)
        {
            TempData["FlashError"] = "Target trùng Reference (sẽ ghi đè trực tiếp client đang chạy). " +
                                     "Nếu thật sự muốn, bật 'Cho phép ghi đè trực tiếp' rồi chạy lại.";
            return RedirectToPage();
        }
        var pending = _store.List(300).FirstOrDefault(j =>
            j.Type == JobType && j.Status is JobStatus.Pending or JobStatus.Running &&
            j.Parameters.TryGetValue("target", out var t) && string.Equals(t, target, StringComparison.OrdinalIgnoreCase));
        if (pending != null)
        {
            TempData["FlashError"] = $"Đã có job Rip Flash đang chờ/chạy cho đúng thư mục này ({pending.Status}). " +
                                     "Đợi job đó xong rồi chạy lại để tránh tải trùng.";
            return RedirectToPage(new { job = pending.Id });
        }

        var job = _queue.Enqueue(JobType, mode == "rip" ? "Rip Flash VNG (tải thật)" : "Rip Flash VNG (quét thử)", new Dictionary<string, string>
        {
            ["source"] = source,
            ["reference"] = reference,
            ["target"] = target,
            ["exclude"] = exclude,
            ["dryRun"] = mode == "scan" ? "true" : "false",
            ["overwrite"] = Overwrite ? "true" : "false",
            ["allowInPlace"] = AllowInPlace ? "true" : "false",
            ["maxFiles"] = Math.Max(0, MaxFiles).ToString(CultureInfo.InvariantCulture),
            ["concurrency"] = Math.Clamp(Concurrency, 1, 16).ToString(CultureInfo.InvariantCulture)
        }, user);
        _audit.Write(user, "job.enqueue", $"flash-rip {job.Id} mode={mode} target={target}");
        TempData["FlashMessage"] = mode == "rip"
            ? $"Đã tạo job TẢI THẬT vào {target}. Xem tiến độ bên dưới."
            : "Đã tạo job QUÉT THỬ (chỉ kiểm tra CDN, không tải file).";
        return RedirectToPage(new { job = job.Id });
    }

    public IActionResult OnPostCancel(Guid id)
    {
        var user = User.Identity?.Name ?? "unknown";
        if (!CanRun)
        {
            TempData["FlashError"] = "Chỉ role Develop/Admin mới được huỷ job.";
            return RedirectToPage();
        }
        var job = _store.Get(id);
        if (job is not { Type: JobType })
        {
            TempData["FlashError"] = "Không tìm thấy job Rip Flash cần huỷ.";
            return RedirectToPage();
        }
        _queue.RequestCancel(id);
        _audit.Write(user, "job.cancel", "flash-rip " + id);
        TempData["FlashMessage"] = "Đã gửi yêu cầu huỷ job.";
        return RedirectToPage(new { job = id });
    }

    public IActionResult OnGetDownload(Guid job, string kind)
    {
        var record = _store.Get(job);
        if (record is not { Type: JobType }) return NotFound();
        var fileName = string.Equals(kind, "manifest", StringComparison.OrdinalIgnoreCase)
            ? "_rip-manifest.json"
            : "_rip-report.json";
        var fileKind = string.Equals(kind, "manifest", StringComparison.OrdinalIgnoreCase) ? "manifest" : "report";

        // 1) Ban sao theo job.
        var jobScoped = Path.Combine(JobReportDir, job.ToString("N") + "-" + fileKind + ".json");
        if (System.IO.File.Exists(jobScoped))
            return PhysicalFile(jobScoped, "application/json; charset=utf-8", fileName);

        // 2) Report "latest" trong thu muc dich — chi khi dung job (tranh tra nham report cua job khac).
        if (!record.Parameters.TryGetValue("target", out var target) || string.IsNullOrWhiteSpace(target)) return NotFound();
        var path = Path.Combine(target, fileName);
        if (!System.IO.File.Exists(path)) return NotFound();
        if (fileKind == "report")
        {
            try
            {
                var report = JsonSerializer.Deserialize<FlashRipReport>(System.IO.File.ReadAllText(path));
                if (!IsReportOfJob(report, record)) return NotFound();
            }
            catch { return NotFound(); }
        }
        return PhysicalFile(path, "application/json; charset=utf-8", fileName);
    }
}
