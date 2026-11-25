using GunnyPanel.Jobs;
using GunnyPanel.Pages.Shared;
using GunnySuite.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GunnyPanel.Pages;

public sealed class XmlNameItem
{
    public string Xml { get; set; } = "";
    public string Table { get; set; } = "";
}

public sealed class SqlFileItem
{
    public string Name { get; set; } = "";
    public long Size { get; set; }
    public DateTime Time { get; set; }
}

public sealed class JobsModel : PageModel
{
    public const int JobPageSize = 6;
    public const int SqlPageSize = 6;

    private readonly JobQueue _queue;
    private readonly JobStore _store;
    private readonly AuditLog _audit;
    private readonly IConfiguration _cfg;
    private readonly SqlDatabase _db;

    public JobsModel(JobQueue queue, JobStore store, AuditLog audit, IConfiguration cfg, SqlDatabase db)
    {
        _queue = queue;
        _store = store;
        _audit = audit;
        _cfg = cfg;
        _db = db;
    }

    public List<JobRecord> Jobs { get; private set; } = new();
    public List<XmlNameItem> XmlNames { get; private set; } = new();
    public List<SqlFileItem> SqlFiles { get; private set; } = new();
    public IReadOnlyCollection<string> Connections => _db.Names;

    /// <summary>Chỉ Develop/Admin mới được chạy/huỷ job (viewer/Moderator chỉ xem).</summary>
    public bool CanRun => User.IsInRole("Develop") || User.IsInRole("Admin");

    /// <summary>Chặn mọi thao tác ghi khi tài khoản không đủ quyền.</summary>
    private IActionResult? DenyIfCannotRun()
    {
        if (CanRun) return null;
        return new JsonResult(new
        {
            ok = false,
            message = "Chỉ tài khoản role Develop/Admin mới được chạy job (tài khoản hiện tại chỉ có quyền xem)."
        })
        { StatusCode = StatusCodes.Status403Forbidden };
    }

    public int JobTotal { get; private set; }
    public int SqlTotal { get; private set; }

    [BindProperty(SupportsGet = true, Name = "jpage")] public int JobPage { get; set; } = 1;
    [BindProperty(SupportsGet = true, Name = "spage")] public int SqlPage { get; set; } = 1;

    [BindProperty] public string SyncSource { get; set; } = "";
    [BindProperty] public string SyncDest { get; set; } = "";
    [BindProperty] public bool SyncOverwrite { get; set; } = true;
    [BindProperty] public string UrlName { get; set; } = "http://127.0.0.1:88/vng-quest/";
    [BindProperty] public string UrlDetail { get; set; } = "Official Br";

    [BindProperty(SupportsGet = true)] public string ReqXml { get; set; } = "levellist";
    [BindProperty] public string ReqSource { get; set; } = "local";
    [BindProperty] public string ReqConnection { get; set; } = "Db_Tank_All";
    [BindProperty] public bool ReqExecute { get; set; }
    [BindProperty] public string ReqTable { get; set; } = "";
    [BindProperty] public string ReqPath { get; set; } = "";
    [BindProperty] public bool ReqAutoId { get; set; }
    [BindProperty] public string ReqStyle { get; set; } = "compact";
    public IReadOnlyList<string> ToolRipXmls { get; } = new[]
    {
        "item", "balllist", "bombconfig", "LoadMapsItems", "NPCInfoList",
        "petskillelementinfo", "petskillinfo", "petskilltemplateinfo", "pettemplateinfo", "runetemplatelist"
    };

    [BindProperty] public string ResSource { get; set; } = "https://gunny.vcdn.vn/flash/";
    [BindProperty] public string ResTarget { get; set; } = "";
    [BindProperty] public bool ResDryRun { get; set; } = true;
    [BindProperty] public bool ResOverwrite { get; set; } = true;
    [BindProperty] public int ResMaxFiles { get; set; } = 200;

    [BindProperty] public string BombMode { get; set; } = "to-png";
    [BindProperty] public string BombSource { get; set; } = "";
    [BindProperty] public string BombOutput { get; set; } = "";
    [BindProperty] public int BombMaxFiles { get; set; } = 50;
    [BindProperty] public int BombThreshold { get; set; } = 128;
    [BindProperty] public bool BombOverwrite { get; set; } = true;

    public async Task OnGetAsync()
    {
        SyncSource = _cfg["Vng:RequestSource"] ?? "https://quest.gn.zing.vn/";
        SyncDest = _cfg["Paths:MirrorDir"] ?? "";
        ResSource = _cfg["Vng:ResourceSource"] ?? "https://gunny.vcdn.vn/flash/";
        ResTarget = Path.Combine(_cfg["Paths:GameRoot"] ?? "", "WebSite", "Flash");
        BombSource = _cfg["Paths:BombDir"] ?? Path.Combine(_cfg["Paths:GameRoot"] ?? "", "Service", "Service", "Road", "Bomb");

        var allJobs = _store.List(500).ToList();
        JobTotal = allJobs.Count;
        JobPage = Pager.Clamp(JobPage, JobTotal, JobPageSize);
        Jobs = allJobs.Skip((JobPage - 1) * JobPageSize).Take(JobPageSize).ToList();

        await LoadXmlNamesAsync();
        LoadSqlFiles();
    }

    private async Task LoadXmlNamesAsync()
    {
        var map = await _db.QueryAsync("Db_Gm",
            "SELECT DISTINCT [XmlName],[Name] FROM [dbo].[Table_Games] " +
            "WHERE [XmlName] IS NOT NULL AND LTRIM(RTRIM([XmlName])) <> '' ORDER BY [XmlName]",
            null, 500, 10);
        if (!map.Ok) return;
        XmlNames = map.Rows
            .Select(r => new XmlNameItem { Xml = r[0] ?? "", Table = r[1] ?? "" })
            .Where(x => x.Xml.Length > 0)
            .ToList();
        if (XmlNames.All(x => !string.Equals(x.Xml, ReqXml, StringComparison.OrdinalIgnoreCase)))
        {
            var pick = XmlNames.FirstOrDefault(x => x.Xml.Equals("levellist", StringComparison.OrdinalIgnoreCase))
                       ?? XmlNames.FirstOrDefault();
            if (pick != null) ReqXml = pick.Xml;
        }
    }

    private string SqlDir => Path.Combine(_cfg["Paths:DataDir"] ?? Path.Combine(AppContext.BaseDirectory, "data"), "sql");

    private void LoadSqlFiles()
    {
        try
        {
            if (!Directory.Exists(SqlDir)) return;
            var all = new DirectoryInfo(SqlDir).GetFiles("*.sql")
                .OrderByDescending(f => f.LastWriteTime)
                .Take(500)
                .Select(f => new SqlFileItem { Name = f.Name, Size = f.Length, Time = f.LastWriteTime })
                .ToList();
            SqlTotal = all.Count;
            SqlPage = Pager.Clamp(SqlPage, SqlTotal, SqlPageSize);
            SqlFiles = all.Skip((SqlPage - 1) * SqlPageSize).Take(SqlPageSize).ToList();
        }
        catch { }
    }

    public IActionResult OnGetDownload(string name)
    {
        var safeName = Path.GetFileName(name ?? "");
        if (string.IsNullOrWhiteSpace(safeName)) return NotFound();
        var path = Path.Combine(SqlDir, safeName);
        if (!System.IO.File.Exists(path)) return NotFound();
        return PhysicalFile(path, "text/plain; charset=utf-8", safeName);
    }

    public IActionResult OnPostSync()
    {
        if (DenyIfCannotRun() is { } deny) return deny;
        var user = User.Identity?.Name ?? "unknown";
        var job = _queue.Enqueue("sync-xml", "Sync XML VNG", new Dictionary<string, string>
        {
            ["source"] = SyncSource ?? "",
            ["dest"] = SyncDest ?? "",
            ["overwrite"] = SyncOverwrite ? "true" : "false"
        }, user);
        _audit.Write(user, "job.enqueue", $"sync-xml {job.Id}");
        return RedirectToPage();
    }

    public IActionResult OnPostUpdateUrl()
    {
        if (DenyIfCannotRun() is { } deny) return deny;
        var user = User.Identity?.Name ?? "unknown";
        var job = _queue.Enqueue("update-url-tank", "Cập nhật Url_Tank", new Dictionary<string, string>
        {
            ["name"] = UrlName ?? "",
            ["detail"] = UrlDetail ?? ""
        }, user);
        _audit.Write(user, "job.enqueue", $"update-url-tank {job.Id} -> {UrlName}");
        return RedirectToPage();
    }

    public IActionResult OnPostCheck()
    {
        if (DenyIfCannotRun() is { } deny) return deny;
        var user = User.Identity?.Name ?? "unknown";
        var job = _queue.Enqueue("check-connections", "Kiểm tra kết nối", new Dictionary<string, string>(), user);
        _audit.Write(user, "job.enqueue", $"check-connections {job.Id}");
        return RedirectToPage();
    }

    public IActionResult OnPostRequestSql()
    {
        if (DenyIfCannotRun() is { } deny) return deny;
        var user = User.Identity?.Name ?? "unknown";
        var job = _queue.Enqueue("request-sql", $"XML → SQL: {ReqXml}", new Dictionary<string, string>
        {
            ["xml"] = (ReqXml ?? "").Trim(),
            ["source"] = (ReqSource ?? "local").Trim(),
            ["path"] = (ReqPath ?? "").Trim(),
            ["connection"] = (ReqConnection ?? "Db_Tank_All").Trim(),
            ["execute"] = ReqExecute ? "true" : "false",
            ["table"] = (ReqTable ?? "").Trim(),
            ["autoId"] = ReqAutoId ? "true" : "false",
            ["style"] = (ReqStyle ?? "compact").Trim()
        }, user);
        _audit.Write(user, "job.enqueue", $"request-sql {job.Id} xml={ReqXml} execute={ReqExecute} autoId={ReqAutoId} style={ReqStyle}");
        return RedirectToPage();
    }

    public IActionResult OnPostResourceSync()
    {
        if (DenyIfCannotRun() is { } deny) return deny;
        var user = User.Identity?.Name ?? "unknown";
        var job = _queue.Enqueue("resource-sync", "Sync resource VNG", new Dictionary<string, string>
        {
            ["source"] = (ResSource ?? "").Trim(),
            ["target"] = (ResTarget ?? "").Trim(),
            ["dryRun"] = ResDryRun ? "true" : "false",
            ["overwrite"] = ResOverwrite ? "true" : "false",
            ["maxFiles"] = Math.Max(0, ResMaxFiles).ToString()
        }, user);
        _audit.Write(user, "job.enqueue", $"resource-sync {job.Id} dryRun={ResDryRun} max={ResMaxFiles}");
        return RedirectToPage();
    }

    public IActionResult OnPostBombConvert()
    {
        if (DenyIfCannotRun() is { } deny) return deny;
        var user = User.Identity?.Name ?? "unknown";
        var job = _queue.Enqueue("bomb-convert", $"Bomb: {BombMode}", new Dictionary<string, string>
        {
            ["mode"] = (BombMode ?? "to-png").Trim(),
            ["source"] = (BombSource ?? "").Trim(),
            ["output"] = (BombOutput ?? "").Trim(),
            ["maxFiles"] = Math.Max(0, BombMaxFiles).ToString(),
            ["threshold"] = Math.Clamp(BombThreshold, 1, 254).ToString(),
            ["overwrite"] = BombOverwrite ? "true" : "false"
        }, user);
        _audit.Write(user, "job.enqueue", $"bomb-convert {job.Id} mode={BombMode} max={BombMaxFiles}");
        return RedirectToPage();
    }

    public IActionResult OnPostCancel(Guid id)
    {
        if (DenyIfCannotRun() is { } deny) return deny;
        var user = User.Identity?.Name ?? "unknown";
        _queue.RequestCancel(id);
        _audit.Write(user, "job.cancel", id.ToString());
        return RedirectToPage();
    }
}
