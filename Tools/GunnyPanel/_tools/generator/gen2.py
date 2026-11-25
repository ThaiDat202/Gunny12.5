# -*- coding: utf-8 -*-
"""Generator part 2/3: Razor Pages (login/dashboard/jobs/db/audit) + wwwroot/css."""
import os

ROOT = r"C:\Gunny\ToolGunny\GunnyPanel"
FILES = {}

FILES[r"src\GunnyPanel\Pages\_ViewImports.cshtml"] = r'''@using GunnyPanel
@using GunnyPanel.Auth
@using GunnyPanel.Jobs
@using GunnyPanel.Pages
@using GunnySuite.Core
'''

FILES[r"src\GunnyPanel\Pages\_ViewStart.cshtml"] = r'''@{
    Layout = "_Layout";
}
'''

FILES[r"src\GunnyPanel\Pages\Shared\_Layout.cshtml"] = r'''@{
    var user = User;
    var display = user?.FindFirst(System.Security.Claims.ClaimTypes.GivenName)?.Value ?? user?.Identity?.Name ?? "";
    var role = user?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "";
    var title = ViewData["Title"] as string ?? "Dashboard";
}
<!DOCTYPE html>
<html lang="vi">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>@title - Gunny Panel</title>
    <link rel="stylesheet" href="~/css/site.css" />
    @RenderSection("Head", required: false)
</head>
<body>
<div class="layout">
    <aside class="sidebar">
        <div class="brand">GUNNY<span>PANEL</span><small>v0.1 test</small></div>
        <nav>
            <a href="/">Tổng quan</a>
            <a href="/Jobs">Jobs &amp; Tools</a>
            <a href="/Db">DB Query</a>
            <a href="/Audit">Audit log</a>
            <a class="ext" href="@(Context.RequestServices.GetService(typeof(Microsoft.Extensions.Configuration.IConfiguration)) is Microsoft.Extensions.Configuration.IConfiguration c ? (c["Vng:AdmincpUrl"] ?? "http://127.0.0.1:90/Users") : "http://127.0.0.1:90/Users")" target="_blank" rel="noopener">Admincp cũ ↗</a>
        </nav>
        <div class="side-footer">
            <div class="who">@display</div>
            <div class="role">@role</div>
            <a href="/Logout" class="logout">Đăng xuất</a>
        </div>
    </aside>
    <main class="main">
        <header class="topbar">
            <h1>@title</h1>
            <span class="badge-ok">Internal tool · localhost</span>
        </header>
        <div class="content">
            @RenderBody()
        </div>
    </main>
</div>
@RenderSection("Scripts", required: false)
</body>
</html>
'''

# ---------------- Login ----------------
FILES[r"src\GunnyPanel\Pages\Login.cshtml"] = r'''@page
@model GunnyPanel.Pages.LoginModel
@{
    Layout = null;
}
<!DOCTYPE html>
<html lang="vi">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>Đăng nhập - Gunny Panel</title>
    <link rel="stylesheet" href="~/css/site.css" />
</head>
<body class="login-body">
    <div class="login-box">
        <h1>GUNNY <span>PANEL</span></h1>
        <p class="muted">Quản trị server &amp; tool Gunny</p>
        @if (!string.IsNullOrEmpty(Model.Error))
        {
            <div class="alert danger">@Model.Error</div>
        }
        <form method="post">
            @Html.AntiForgeryToken()
            <label>Tài khoản</label>
            <input name="UserName" value="@Model.UserName" autocomplete="username" autofocus />
            <label>Mật khẩu</label>
            <input name="Password" type="password" autocomplete="current-password" />
            <button type="submit" class="btn primary block">Đăng nhập</button>
        </form>
        <p class="muted small">Test: <b>admin/Admin@123</b> · <b>gm/Gm@123</b> · <b>viewer/View@123</b></p>
    </div>
</body>
</html>
'''

FILES[r"src\GunnyPanel\Pages\Login.cshtml.cs"] = r'''using GunnyPanel.Auth;
using GunnySuite.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GunnyPanel.Pages;

public sealed class LoginModel : PageModel
{
    private readonly PanelUserStore _users;
    private readonly AuditLog _audit;

    public LoginModel(PanelUserStore users, AuditLog audit)
    {
        _users = users;
        _audit = audit;
    }

    [BindProperty] public string UserName { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";
    public string? Error { get; set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToPage("/Index");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = _users.Find(UserName ?? "");
        if (user == null || !PanelUserStore.Verify(user, Password ?? ""))
        {
            Error = "Sai tài khoản hoặc mật khẩu.";
            _audit.Write(UserName ?? "", "login.failed", HttpContext.Connection.RemoteIpAddress?.ToString() ?? "");
            return Page();
        }

        await HttpContext.SignInPanelAsync(user);
        _audit.Write(user.UserName, "login.ok", HttpContext.Connection.RemoteIpAddress?.ToString() ?? "");
        return RedirectToPage("/Index");
    }
}
'''

FILES[r"src\GunnyPanel\Pages\Logout.cshtml"] = r'''@page
@model GunnyPanel.Pages.LogoutModel
'''

FILES[r"src\GunnyPanel\Pages\Logout.cshtml.cs"] = r'''using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GunnyPanel.Pages;

public sealed class LogoutModel : PageModel
{
    public async Task<IActionResult> OnGetAsync()
    {
        await HttpContext.SignOutAsync();
        return RedirectToPage("/Login");
    }
}
'''

# ---------------- Dashboard ----------------
FILES[r"src\GunnyPanel\Pages\Index.cshtml"] = r'''@page
@model GunnyPanel.Pages.IndexModel
@{
    ViewData["Title"] = "Tổng quan";
}

<div class="grid">
    <section class="card">
        <h2>Kết nối SQL</h2>
        <table class="mini">
            @foreach (var db in Model.Dbs)
            {
                <tr>
                    <td>@db.Name</td>
                    <td class="@(db.Ok ? "ok" : "bad")">@(db.Ok ? "OK" : "FAIL")</td>
                    <td class="muted small">@db.Text</td>
                </tr>
            }
        </table>
    </section>

    <section class="card">
        <h2>Quest server VNG</h2>
        <p class="@(Model.VngOk ? "ok" : "bad")">
            @(Model.VngOk ? "OK" : "FAIL") — @Model.VngText
        </p>
        <p class="muted small">@Model.VngUrl</p>
    </section>

    <section class="card">
        <h2>Nguồn XML cho Tool XML</h2>
        <p>Db_Gm.dbo.Url_Tank:</p>
        <p><b>@(Model.UrlTankName ?? "(chưa đọc được)")</b></p>
        <p class="muted small">Detail: @(Model.UrlTankDetail ?? "-")</p>
        <a class="btn small" href="/Jobs">Cập nhật / Sync XML →</a>
    </section>

    <section class="card">
        <h2>Đường dẫn</h2>
        <table class="mini">
            @foreach (var p in Model.Paths)
            {
                <tr>
                    <td>@p.Label</td>
                    <td class="@(p.Exists ? "ok" : "bad")">@(p.Exists ? "OK" : "THIẾU")</td>
                    <td class="muted small">@p.Path</td>
                </tr>
            }
        </table>
    </section>
</div>

<section class="card">
    <h2>Jobs gần đây</h2>
    @if (Model.Jobs.Count == 0)
    {
        <p class="muted">Chưa có job nào. Vào <a href="/Jobs">Jobs &amp; Tools</a> để chạy.</p>
    }
    else
    {
        <table class="list">
            <thead><tr><th>Thời gian</th><th>Loại</th><th>Người tạo</th><th>Trạng thái</th><th>Kết quả</th></tr></thead>
            <tbody>
            @foreach (var job in Model.Jobs)
            {
                <tr>
                    <td class="mono">@job.CreatedAt.ToString("dd/MM HH:mm:ss")</td>
                    <td>@job.Title</td>
                    <td>@job.CreatedBy</td>
                    <td class="@(job.Status == JobStatus.Success ? "ok" : job.Status == JobStatus.Failed ? "bad" : "")">@job.Status (@job.Progress%)</td>
                    <td class="muted small">@(job.Error ?? job.Result)</td>
                </tr>
            }
            </tbody>
        </table>
    }
</section>
'''

FILES[r"src\GunnyPanel\Pages\Index.cshtml.cs"] = r'''using GunnyPanel.Jobs;
using GunnySuite.Core;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GunnyPanel.Pages;

public sealed class DbStatusItem
{
    public string Name { get; set; } = "";
    public bool Ok { get; set; }
    public string Text { get; set; } = "";
}

public sealed class PathStatusItem
{
    public string Label { get; set; } = "";
    public string Path { get; set; } = "";
    public bool Exists { get; set; }
}

public sealed class IndexModel : PageModel
{
    private readonly SqlDatabase _db;
    private readonly HttpFetcher _http;
    private readonly JobStore _jobs;
    private readonly IConfiguration _cfg;

    public IndexModel(SqlDatabase db, HttpFetcher http, JobStore jobs, IConfiguration cfg)
    {
        _db = db;
        _http = http;
        _jobs = jobs;
        _cfg = cfg;
    }

    public List<DbStatusItem> Dbs { get; } = new();
    public List<PathStatusItem> Paths { get; } = new();
    public bool VngOk { get; private set; }
    public string VngText { get; private set; } = "";
    public string VngUrl { get; private set; } = "";
    public string? UrlTankName { get; private set; }
    public string? UrlTankDetail { get; private set; }
    public List<JobRecord> Jobs { get; private set; } = new();

    public async Task OnGetAsync()
    {
        foreach (var name in _db.Names)
        {
            var (ok, text) = await _db.TestAsync(name);
            Dbs.Add(new DbStatusItem { Name = name, Ok = ok, Text = text });
        }

        VngUrl = (_cfg["Vng:RequestSource"] ?? "https://quest.gn.zing.vn/").TrimEnd('/') + "/levellist.xml";
        var check = await _http.CheckAsync(VngUrl);
        VngOk = check.ok;
        VngText = check.message;

        var tank = await _db.QueryAsync("Db_Gm", "SELECT TOP 1 [Name],[Detail] FROM [dbo].[Url_Tank] ORDER BY [ID]", 5, 10);
        if (tank.Ok && tank.Rows.Count > 0)
        {
            UrlTankName = tank.Rows[0][0];
            UrlTankDetail = tank.Rows[0][1];
        }

        foreach (var (label, key) in new[]
                 {
                     ("Web Request", "Paths:WebRequestDir"),
                     ("XML mirror", "Paths:MirrorDir"),
                     ("Data dir", "Paths:DataDir")
                 })
        {
            var path = _cfg[key] ?? "";
            Paths.Add(new PathStatusItem { Label = label, Path = path, Exists = Directory.Exists(path) });
        }

        Jobs = _jobs.List(8).ToList();
    }
}
'''

# ---------------- Jobs ----------------
FILES[r"src\GunnyPanel\Pages\Jobs.cshtml"] = r'''@page
@model GunnyPanel.Pages.JobsModel
@{
    ViewData["Title"] = "Jobs & Tools";
    var running = Model.Jobs.Any(j => j.Status == JobStatus.Running || j.Status == JobStatus.Pending);
}

<div class="grid">
    <section class="card">
        <h2>1) Sync XML từ VNG → local (Tool XML)</h2>
        <p class="muted small">Tải bộ XML mới nhất từ quest server, nén lại zlib (nếu server trả plain), ghi vào thư mục IIS port 88.</p>
        <form method="post" asp-page-handler="Sync">
            <label>Source URL</label>
            <input name="SyncSource" value="@Model.SyncSource" />
            <label>Thư mục đích (mirror)</label>
            <input name="SyncDest" value="@Model.SyncDest" />
            <label class="row"><input type="checkbox" name="SyncOverwrite" value="true" checked="@Model.SyncOverwrite" /> Ghi đè file đã có</label>
            <button type="submit" class="btn primary">Chạy job Sync XML</button>
        </form>
    </section>

    <section class="card">
        <h2>2) Cập nhật Db_Gm.dbo.Url_Tank</h2>
        <p class="muted small">Đổi nguồn trong Tool XML. URL phải bắt đầu bằng http:// và kết thúc bằng /.</p>
        <form method="post" asp-page-handler="UpdateUrl">
            <label>Name (URL)</label>
            <input name="UrlName" value="@Model.UrlName" />
            <label>Detail</label>
            <input name="UrlDetail" value="@Model.UrlDetail" />
            <button type="submit" class="btn primary">Chạy job cập nhật Url_Tank</button>
        </form>
        <hr />
        <form method="post" asp-page-handler="Check">
            <button type="submit" class="btn">Kiểm tra kết nối SQL + VNG</button>
        </form>
    </section>
</div>

<section class="card">
    <h2>Jobs</h2>
    @if (Model.Jobs.Count == 0)
    {
        <p class="muted">Chưa có job.</p>
    }
    else
    {
        <table class="list">
            <thead>
            <tr>
                <th>Thời gian</th><th>Job</th><th>Người tạo</th><th>Trạng thái</th><th>Tiến độ</th><th></th>
            </tr>
            </thead>
            <tbody>
            @foreach (var job in Model.Jobs)
            {
                <tr>
                    <td class="mono">@job.CreatedAt.ToString("dd/MM HH:mm:ss")</td>
                    <td>
                        <b>@job.Title</b>
                        @if (!string.IsNullOrEmpty(job.Error) || !string.IsNullOrEmpty(job.Result))
                        {
                            <details>
                                <summary class="muted small">kết quả / log</summary>
                                <pre class="log">@(job.Error ?? job.Result)
@string.Join("\n", job.Log.TakeLast(30))</pre>
                            </details>
                        }
                    </td>
                    <td>@job.CreatedBy</td>
                    <td class="@(job.Status == JobStatus.Success ? "ok" : job.Status == JobStatus.Failed ? "bad" : "")">@job.Status</td>
                    <td>
                        <div class="progress"><div style="width:@(job.Progress)%"></div></div>
                        <span class="muted small">@job.Progress%</span>
                    </td>
                    <td>
                        @if (job.Status == JobStatus.Running || job.Status == JobStatus.Pending)
                        {
                            <form method="post" asp-page-handler="Cancel" asp-route-id="@job.Id">
                                <button type="submit" class="btn small danger">Hủy</button>
                            </form>
                        }
                    </td>
                </tr>
            }
            </tbody>
        </table>
    }
</section>

@section Scripts {
    @if (running)
    {
        <script>setTimeout(function () { location.reload(); }, 4000);</script>
    }
}
'''

FILES[r"src\GunnyPanel\Pages\Jobs.cshtml.cs"] = r'''using GunnyPanel.Jobs;
using GunnySuite.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GunnyPanel.Pages;

public sealed class JobsModel : PageModel
{
    private readonly JobQueue _queue;
    private readonly JobStore _store;
    private readonly AuditLog _audit;
    private readonly IConfiguration _cfg;

    public JobsModel(JobQueue queue, JobStore store, AuditLog audit, IConfiguration cfg)
    {
        _queue = queue;
        _store = store;
        _audit = audit;
        _cfg = cfg;
    }

    public List<JobRecord> Jobs { get; private set; } = new();

    [BindProperty] public string SyncSource { get; set; } = "";
    [BindProperty] public string SyncDest { get; set; } = "";
    [BindProperty] public bool SyncOverwrite { get; set; } = true;
    [BindProperty] public string UrlName { get; set; } = "http://127.0.0.1:88/vng-quest/";
    [BindProperty] public string UrlDetail { get; set; } = "Official Br";

    public void OnGet()
    {
        SyncSource = _cfg["Vng:RequestSource"] ?? "https://quest.gn.zing.vn/";
        SyncDest = _cfg["Paths:MirrorDir"] ?? "";
        Jobs = _store.List(50).ToList();
    }

    public IActionResult OnPostSync()
    {
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
        var user = User.Identity?.Name ?? "unknown";
        var job = _queue.Enqueue("check-connections", "Kiểm tra kết nối", new Dictionary<string, string>(), user);
        _audit.Write(user, "job.enqueue", $"check-connections {job.Id}");
        return RedirectToPage();
    }

    public IActionResult OnPostCancel(Guid id)
    {
        var user = User.Identity?.Name ?? "unknown";
        _queue.RequestCancel(id);
        _audit.Write(user, "job.cancel", id.ToString());
        return RedirectToPage();
    }
}
'''

# ---------------- DB Query ----------------
FILES[r"src\GunnyPanel\Pages\Db.cshtml"] = r'''@page
@model GunnyPanel.Pages.DbModel
@{
    ViewData["Title"] = "DB Query (SELECT only)";
}

<section class="card">
    <form method="post">
        <div class="grid two">
            <div>
                <label>Connection</label>
                <select name="Connection">
                    @foreach (var name in Model.Connections)
                    {
                        <option value="@name" selected="@(name == Model.Connection)">@name</option>
                    }
                </select>
            </div>
            <div>
                <label>Bảng (chèn nhanh)</label>
                <select id="tables">
                    <option value="">-- chọn bảng --</option>
                    @foreach (var table in Model.Tables)
                    {
                        <option value="@table">@table</option>
                    }
                </select>
            </div>
        </div>
        <label>SQL (chỉ SELECT / WITH, tối đa 300 dòng hiển thị)</label>
        <textarea name="Sql" rows="5">@Model.Sql</textarea>
        <div class="row gap">
            <button type="submit" class="btn primary">Chạy</button>
            @if (Model.Result != null && Model.Result.Ok)
            {
                <a class="btn" href="?handler=Export&connection=@Model.Connection&sql=@Uri.EscapeDataString(Model.Sql ?? "")">Xuất CSV</a>
            }
        </div>
    </form>
</section>

@if (!string.IsNullOrEmpty(Model.Message))
{
    <div class="alert @(Model.Result != null && !Model.Result.Ok ? "danger" : "info")">@Model.Message</div>
}

@if (Model.Result != null && Model.Result.Ok)
{
    <section class="card">
        <h2>Kết quả: @Model.Result.Rows.Count dòng @(Model.Result.Truncated ? "(đã cắt bớt)" : "")</h2>
        <div class="table-wrap">
            <table class="list compact">
                <thead>
                <tr>
                    @foreach (var col in Model.Result.Columns)
                    {
                        <th>@col</th>
                    }
                </tr>
                </thead>
                <tbody>
                @foreach (var row in Model.Result.Rows)
                {
                    <tr>
                        @foreach (var cell in row)
                        {
                            <td>@cell</td>
                        }
                    </tr>
                }
                </tbody>
            </table>
        </div>
    </section>
}

@section Scripts {
    <script>
        document.getElementById('tables').addEventListener('change', function () {
            if (!this.value) return;
            var box = document.querySelector('textarea[name="Sql"]');
            box.value = 'SELECT TOP 100 * FROM [dbo].[' + this.value + ']';
        });
    </script>
}
'''

FILES[r"src\GunnyPanel\Pages\Db.cshtml.cs"] = r'''using System.Text;
using GunnySuite.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GunnyPanel.Pages;

public sealed class DbModel : PageModel
{
    private readonly SqlDatabase _db;

    public DbModel(SqlDatabase db) => _db = db;

    public IReadOnlyCollection<string> Connections { get; private set; } = Array.Empty<string>();
    public List<string> Tables { get; } = new();

    [BindProperty] public string Connection { get; set; } = "Db_Tank_All";
    [BindProperty] public string Sql { get; set; } = "SELECT TOP 20 * FROM [dbo].[Sys_Users_Detail]";

    public SqlResult? Result { get; private set; }
    public string? Message { get; set; }

    public async Task OnGetAsync()
    {
        Connections = _db.Names;
        await LoadTablesAsync();
    }

    public async Task OnPostAsync()
    {
        Connections = _db.Names;
        await LoadTablesAsync();

        var sqlText = (Sql ?? "").Trim();
        if (!IsSelect(sqlText))
        {
            Message = "Giai đoạn test chỉ cho phép câu lệnh SELECT/WITH.";
            return;
        }

        Result = await _db.QueryAsync(Connection, sqlText, 300, 30);
        Message = Result.Ok
            ? $"OK: đọc {Result.TotalRead} dòng."
            : "LỖI: " + Result.Error;
    }

    public async Task<IActionResult> OnGetExportAsync(string connection, string sql)
    {
        if (!IsSelect((sql ?? "").Trim())) return Content("Chi cho phep SELECT/WITH.");
        if (!_db.Has(connection)) return Content("Connection khong ton tai.");

        var result = await _db.QueryAsync(connection, sql, 5000, 60);
        if (!result.Ok) return Content("LOI: " + result.Error);

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", result.Columns.Select(Csv)));
        foreach (var row in result.Rows)
            sb.AppendLine(string.Join(",", row.Select(v => Csv(v ?? ""))));
        return File(Encoding.UTF8.GetBytes("\uFEFF" + sb), "text/csv", $"export_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
    }

    private async Task LoadTablesAsync()
    {
        var tables = await _db.QueryAsync("Db_Tank_All", "SELECT name FROM sys.tables ORDER BY name", 1000, 10);
        if (!tables.Ok) return;
        foreach (var row in tables.Rows)
            if (row[0] != null) Tables.Add(row[0]);
    }

    private static bool IsSelect(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql)) return false;
        return sql.StartsWith("select", StringComparison.OrdinalIgnoreCase)
            || sql.StartsWith("with", StringComparison.OrdinalIgnoreCase);
    }

    private static string Csv(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }
}
'''

# ---------------- Audit ----------------
FILES[r"src\GunnyPanel\Pages\Audit.cshtml"] = r'''@page
@model GunnyPanel.Pages.AuditModel
@{
    ViewData["Title"] = "Audit log";
}
<section class="card">
    <p class="muted small">Hiển thị 300 dòng gần nhất từ <code>data/logs/audit-*.log</code></p>
    <div class="table-wrap">
        <table class="list compact">
            <thead><tr><th>Thời gian</th><th>User</th><th>Hành động</th><th>Chi tiết</th></tr></thead>
            <tbody>
            @foreach (var entry in Model.Entries)
            {
                <tr>
                    <td class="mono">@entry.Time.ToString("dd/MM HH:mm:ss")</td>
                    <td>@entry.User</td>
                    <td>@entry.Action</td>
                    <td class="muted small">@entry.Detail</td>
                </tr>
            }
            </tbody>
        </table>
    </div>
</section>
'''

FILES[r"src\GunnyPanel\Pages\Audit.cshtml.cs"] = r'''using GunnySuite.Core;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GunnyPanel.Pages;

public sealed class AuditModel : PageModel
{
    private readonly AuditLog _audit;

    public AuditModel(AuditLog audit) => _audit = audit;

    public IReadOnlyList<AuditEntry> Entries { get; private set; } = Array.Empty<AuditEntry>();

    public void OnGet()
    {
        Entries = _audit.Tail(300);
    }
}
'''

# ---------------- CSS ----------------
FILES[r"src\GunnyPanel\wwwroot\css\site.css"] = r''':root {
    --bg: #0f1420;
    --panel: #171e2e;
    --panel2: #1e2740;
    --line: #2a3552;
    --text: #e6ebf5;
    --muted: #8b97b3;
    --accent: #4f8cff;
    --ok: #3ddc97;
    --bad: #ff5d73;
    --warn: #ffc857;
}
* { box-sizing: border-box; }
body {
    margin: 0;
    font-family: "Segoe UI", Roboto, Arial, sans-serif;
    background: var(--bg);
    color: var(--text);
    font-size: 14px;
}
a { color: var(--accent); text-decoration: none; }
a:hover { text-decoration: underline; }
.layout { display: flex; min-height: 100vh; }
.sidebar {
    width: 230px; flex: 0 0 230px;
    background: linear-gradient(180deg, #121a2b, #0d1220);
    border-right: 1px solid var(--line);
    display: flex; flex-direction: column;
    padding: 18px 0;
}
.brand { padding: 0 20px 18px; font-weight: 800; letter-spacing: .5px; font-size: 18px; }
.brand span { color: var(--accent); }
.brand small { display: block; font-weight: 400; color: var(--muted); font-size: 11px; margin-top: 2px; }
.sidebar nav { display: flex; flex-direction: column; }
.sidebar nav a { color: var(--text); padding: 10px 20px; border-left: 3px solid transparent; }
.sidebar nav a:hover { background: var(--panel2); text-decoration: none; }
.sidebar nav a.ext { color: var(--muted); font-size: 12px; }
.side-footer { margin-top: auto; padding: 16px 20px 0; border-top: 1px solid var(--line); }
.side-footer .who { font-weight: 600; }
.side-footer .role { color: var(--muted); font-size: 12px; margin-bottom: 8px; }
.side-footer .logout { color: var(--warn); font-size: 12px; }
.main { flex: 1; min-width: 0; }
.topbar {
    display: flex; align-items: center; justify-content: space-between;
    padding: 16px 26px; border-bottom: 1px solid var(--line); background: var(--panel);
}
.topbar h1 { margin: 0; font-size: 20px; }
.badge-ok { color: var(--muted); font-size: 12px; }
.content { padding: 22px 26px 60px; }
.grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: 16px; margin-bottom: 18px; }
.grid.two { grid-template-columns: 1fr 1fr; }
.card {
    background: var(--panel); border: 1px solid var(--line); border-radius: 10px;
    padding: 16px 18px; margin-bottom: 16px;
}
.card h2 { margin: 0 0 12px; font-size: 15px; color: #cfd8ec; }
label { display: block; margin: 10px 0 5px; color: var(--muted); font-size: 12px; text-transform: uppercase; letter-spacing: .4px; }
input[type=text], input[type=password], input:not([type]), select, textarea {
    width: 100%; padding: 9px 11px; border-radius: 7px; border: 1px solid var(--line);
    background: var(--panel2); color: var(--text); font-family: inherit; font-size: 13px;
}
textarea { font-family: Consolas, "Courier New", monospace; line-height: 1.5; resize: vertical; }
input[type=checkbox] { width: auto; }
.row { display: flex; align-items: center; gap: 8px; }
.row.gap { gap: 10px; margin-top: 12px; }
.btn {
    display: inline-block; padding: 8px 14px; border-radius: 7px; border: 1px solid var(--line);
    background: var(--panel2); color: var(--text); font-size: 13px; cursor: pointer;
}
.btn:hover { border-color: var(--accent); text-decoration: none; }
.btn.primary { background: var(--accent); border-color: var(--accent); color: #08101f; font-weight: 700; }
.btn.small { padding: 5px 10px; font-size: 12px; }
.btn.danger { border-color: var(--bad); color: var(--bad); }
.btn.block { width: 100%; margin-top: 16px; }
table.mini { width: 100%; border-collapse: collapse; }
table.mini td { padding: 5px 6px 5px 0; vertical-align: top; }
table.list { width: 100%; border-collapse: collapse; }
table.list th, table.list td { text-align: left; padding: 8px 10px; border-bottom: 1px solid var(--line); vertical-align: top; }
table.list th { color: var(--muted); font-size: 12px; text-transform: uppercase; letter-spacing: .3px; }
table.list.compact th, table.list.compact td { padding: 5px 8px; }
.table-wrap { overflow: auto; max-height: 60vh; }
.mono { font-family: Consolas, monospace; font-size: 12px; }
.muted { color: var(--muted); }
.small { font-size: 12px; }
.ok { color: var(--ok); }
.bad { color: var(--bad); }
.progress { width: 110px; height: 7px; background: #0b101c; border-radius: 10px; overflow: hidden; }
.progress div { height: 100%; background: var(--accent); }
.alert { padding: 10px 14px; border-radius: 8px; margin: 12px 0; border: 1px solid var(--line); background: var(--panel); }
.alert.danger { border-color: var(--bad); color: #ffd6dc; }
.alert.info { border-color: var(--accent); }
pre.log {
    max-height: 220px; overflow: auto; background: #0b101c; border: 1px solid var(--line);
    padding: 10px; border-radius: 7px; font-size: 12px; white-space: pre-wrap;
}
details summary { cursor: pointer; }
hr { border: none; border-top: 1px solid var(--line); margin: 16px 0; }
.login-body {
    display: flex; align-items: center; justify-content: center; min-height: 100vh;
    background: radial-gradient(circle at 20% 20%, #1b2740, #0b0f19 60%);
}
.login-box {
    width: 360px; background: var(--panel); border: 1px solid var(--line);
    border-radius: 14px; padding: 28px; box-shadow: 0 20px 60px rgba(0,0,0,.45);
}
.login-box h1 { margin: 0 0 4px; font-size: 22px; }
.login-box h1 span { color: var(--accent); }
.login-box .muted { margin-top: 0; }
'''

# ---------------- README / bat / gitignore ----------------
FILES[r"README.md"] = r'''# GunnyPanel (v0.1 — bản test)

Panel quản trị Gunny chạy **hoàn toàn tách biệt** với Admincp cũ và Road.Service.
Mục tiêu giai đoạn này: test được luồng **job nền + web UI + DB + sync XML VNG** trước khi
đụng vào code cũ (Road.Service / Form1.cs / Admincp).

## Vị trí & phạm vi

- Thư mục: `C:\Gunny\ToolGunny\GunnyPanel` (không nằm trong `Admincp`).
- **Không sửa gì** trong `Admincp`, `Road.Service`, `Gunny12.6` — chỉ đọc DB và ghi vào
  thư mục mirror `Gunny12.6\WebSite\Request\vng-quest`.
- Port mặc định: **http://127.0.0.1:91** (bind localhost).

## Cấu trúc solution

```
GunnyPanel.sln
├─ src\GunnySuite.Core\        # class library dung chung (zlib, http, sql, audit)
└─ src\GunnyPanel\             # ASP.NET Core (net10): Razor Pages + job worker + scheduler
   ├─ Auth\                    # user store đọc từ appsettings (test)
   ├─ Jobs\                    # JobStore/JobQueue/JobWorker + handlers
   ├─ Pages\                   # Login, Dashboard, Jobs, DB Query, Audit
   └─ appsettings.json         # connection strings, paths, users, scheduler
```

## Chạy thử

```bat
cd C:\Gunny\ToolGunny\GunnyPanel
run-test.bat
```
Mở: http://127.0.0.1:91

| Tài khoản | Mật khẩu | Role |
|---|---|---|
| admin | Admin@123 | Develop |
| gm | Gm@123 | Admin |
| viewer | View@123 | Moderator |

> Lưu ý: đây là **auth test** (user nằm trong `appsettings.json`, hash SHA256 salt). Khi chuyển
> sang dùng thật sẽ thay bằng Identity/DB + HTTPS + audit DB.

## Chức năng hiện có (v0.1)

1. **Dashboard**: trạng thái kết nối 3 DB (`Db_Gm`, `Db_Tank`, `Db_Tank_All`), trạng thái quest
   server VNG, giá trị `Url_Tank` hiện tại, đường dẫn cấu hình, 8 job gần nhất.
2. **Jobs & Tools**
   - **Sync XML VNG → local**: tải bộ XML mới nhất từ `https://quest.gn.zing.vn/`, tự nén zlib
     các file server trả plain, ghi vào `C:\Gunny\Gunny12.6\WebSite\Request\vng-quest`
     (để Tool XML Admincp ở `http://127.0.0.1:90/XML` đọc được).
   - **Cập nhật `Db_Gm.dbo.Url_Tank`**: đổi nguồn cho Tool XML (validate `http://.../`).
   - **Kiểm tra kết nối** SQL + VNG.
   - Job chạy nền, có tiến độ/log/hủy, lịch sử job lưu `data/jobs.json`.
3. **DB Query**: chỉ SELECT/WITH, tối đa 300 dòng hiển thị, xuất CSV (tối đa 5000 dòng).
4. **Audit log**: file JSON-lines trong `data/logs/audit-YYYYMMDD.log`.
5. **Scheduler**: tự chạy Sync XML mỗi ngày lúc `Scheduler:SyncXmlDailyAt` (mặc định 03:00).

## API (cho desktop/automation sau này)

| Method | Path | Ghi chú |
|---|---|---|
| GET | `/api/health` | anonymous |
| GET | `/api/jobs` | cần đăng nhập (cookie) |
| GET | `/api/jobs/{id}` | cần đăng nhập |

Giai đoạn sau sẽ thêm API key + endpoint cho desktop/agent (`/api/v1/...`).

## Cấu hình (`src\GunnyPanel\appsettings.json`)

```jsonc
"ConnectionStrings": { "Db_Gm": "...", "Db_Tank": "...", "Db_Tank_All": "..." },
"Paths": {
  "GameRoot": "C:\\Gunny\\Gunny12.6",
  "WebRequestDir": "C:\\Gunny\\Gunny12.6\\WebSite\\Request",
  "MirrorDir": "C:\\Gunny\\Gunny12.6\\WebSite\\Request\\vng-quest",
  "DataDir": "C:\\Gunny\\ToolGunny\\GunnyPanel\\data",
  "LogDir": "C:\\Gunny\\ToolGunny\\GunnyPanel\\data\\logs"
},
"Vng": { "RequestSource": "https://quest.gn.zing.vn/", "ResourceSource": "https://gunny.vcdn.vn/flash/" },
"Scheduler": { "Enabled": true, "SyncXmlDailyAt": "03:00" }
```

## Build / publish

```bat
build.bat                                  :: dotnet build
dotnet publish src\GunnyPanel\GunnyPanel.csproj -c Release -o publish
```

Sau này cài như Windows Service (khi đã chốt):
```bat
sc create GunnyPanel binPath= "C:\Gunny\ToolGunny\GunnyPanel\publish\GunnyPanel.exe" start= auto
sc start GunnyPanel
```
Panel đã tự bind `http://127.0.0.1:91`; muốn mở LAN thì đổi `Panel:Urls` và bật firewall có kiểm soát.

## Việc tiếp theo (đúng lộ trình đã chốt)

- [ ] Sprint 2: module **Request XML → SQL** (dry-run, backup, transaction) + export CSV/Excel.
- [ ] Sprint 2: module **Resource sync** (VNG `gunny.vcdn.vn/flash`) + **Bomb convert**.
- [ ] Sprint 3: **Road GM API** nhỏ (localhost) cho kick/ban/announce/reload → Panel gọi sang; khi đó mới bỏ Form1 khỏi Road.Service.
- [ ] Sprint 3: audit DB, Identity thật, HTTPS, role chi tiết, desktop shell dùng chung API.
- [ ] Tuỳ chọn: PostgreSQL/SQLite cho `jobs`/`audit` thay vì file.

## Nguyên tắc an toàn (đang áp dụng)

- Chỉ SELECT trên trang DB Query; thao tác ghi phải qua job có kiểm tra tham số.
- Job update DB dùng parameterized query, không cộng chuỗi SQL.
- Audit mọi enqueue/cancel/login.
- Chỉ bind localhost; không mở public khi chưa có HTTPS + auth thật.
'''

FILES[r"run-test.bat"] = r'''@echo off
chcp 65001 >nul
cd /d "%~dp0"
echo === GunnyPanel (test) ===
echo Mo trinh duyet: http://127.0.0.1:91
echo.
dotnet run --project src\GunnyPanel\GunnyPanel.csproj --no-launch-profile
pause
'''

FILES[r"build.bat"] = r'''@echo off
chcp 65001 >nul
cd /d "%~dp0"
dotnet build GunnyPanel.sln -c Debug
pause
'''

FILES[r".gitignore"] = r'''bin/
obj/
publish/
data/
*.user
.vs/
'''


def write_all():
    for rel, content in FILES.items():
        path = os.path.join(ROOT, rel)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            f.write(content)
    print(f"Wrote {len(FILES)} files")


if __name__ == "__main__":
    write_all()
    print("PART 2 DONE")
