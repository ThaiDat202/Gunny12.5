# -*- coding: utf-8 -*-
"""Generator part 1/3: tao solution GunnyPanel + project Core + backend Panel.
Chay: python gen1.py
"""
import os, sys, subprocess, secrets, hashlib

ROOT = r"C:\Gunny\ToolGunny\GunnyPanel"

FILES = {}

# ---------------- solution / csproj ----------------
FILES[r"src\GunnySuite.Core\GunnySuite.Core.csproj"] = r'''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <RootNamespace>GunnySuite.Core</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Data.SqlClient" Version="5.2.2" />
  </ItemGroup>
</Project>
'''

FILES[r"src\GunnyPanel\GunnyPanel.csproj"] = r'''<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <RootNamespace>GunnyPanel</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\GunnySuite.Core\GunnySuite.Core.csproj" />
  </ItemGroup>
</Project>
'''

# ---------------- Core: Zlib ----------------
FILES[r"src\GunnySuite.Core\Zlib.cs"] = r'''using System.IO.Compression;

namespace GunnySuite.Core;

/// <summary>zlib (RFC 1950) - dung cho cac file XML cua Gunny quest server.</summary>
public static class Zlib
{
    public static byte[] Compress(byte[] data)
    {
        using var ms = new MemoryStream();
        using (var z = new ZLibStream(ms, CompressionLevel.SmallestSize, leaveOpen: true))
            z.Write(data, 0, data.Length);
        return ms.ToArray();
    }

    public static byte[] Decompress(byte[] data)
    {
        using var input = new MemoryStream(data);
        using var z = new ZLibStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        z.CopyTo(output);
        return output.ToArray();
    }

    public static bool LooksZlib(byte[] data) => data.Length >= 2 && data[0] == 0x78;
    public static bool LooksXml(byte[] data) => data.Length > 0 && data[0] == (byte)'<';
}
'''

# ---------------- Core: HttpFetcher ----------------
FILES[r"src\GunnySuite.Core\HttpFetcher.cs"] = r'''namespace GunnySuite.Core;

/// <summary>HTTP client dung chung (tai XML/asset tu quest/resource server).</summary>
public sealed class HttpFetcher
{
    private readonly HttpClient _http;

    public HttpFetcher()
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = true };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) GunnyPanel/1.0");
    }

    /// <summary>Tra ve null neu loi/404.</summary>
    public async Task<byte[]?> TryGetAsync(string url, CancellationToken ct = default)
    {
        try
        {
            using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadAsByteArrayAsync(ct);
        }
        catch
        {
            return null;
        }
    }

    public async Task<(bool ok, string message)> CheckAsync(string url, CancellationToken ct = default)
    {
        try
        {
            using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            return (resp.IsSuccessStatusCode, $"{(int)resp.StatusCode} {resp.ReasonPhrase}");
        }
        catch (Exception ex)
        {
            return (false, ex.GetType().Name + ": " + ex.Message);
        }
    }
}
'''

# ---------------- Core: SqlDatabase ----------------
FILES[r"src\GunnySuite.Core\SqlDatabase.cs"] = r'''using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;

namespace GunnySuite.Core;

public sealed class SqlResult
{
    public List<string> Columns { get; } = new();
    public List<string?[]> Rows { get; } = new();
    public int TotalRead { get; set; }
    public bool Truncated { get; set; }
    public string? Error { get; set; }
    public bool Ok => Error == null;
}

public sealed class SqlDatabase
{
    private readonly Dictionary<string, string> _connections;

    public SqlDatabase(IReadOnlyDictionary<string, string> connections)
    {
        _connections = new Dictionary<string, string>(connections, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyCollection<string> Names => _connections.Keys;
    public bool Has(string name) => _connections.ContainsKey(name);
    public string? GetConnectionString(string name) => _connections.TryGetValue(name, out var cs) ? cs : null;

    public async Task<SqlResult> QueryAsync(string connectionName, string sql, int maxRows = 500, int timeoutSeconds = 30, CancellationToken ct = default)
    {
        var result = new SqlResult();
        if (!_connections.TryGetValue(connectionName, out var cs))
        {
            result.Error = $"Khong tim thay connection '{connectionName}'";
            return result;
        }

        try
        {
            await using var conn = new SqlConnection(cs);
            await conn.OpenAsync(ct);
            await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = timeoutSeconds };
            await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, ct);
            for (var i = 0; i < reader.FieldCount; i++) result.Columns.Add(reader.GetName(i));
            while (await reader.ReadAsync(ct))
            {
                result.TotalRead++;
                if (result.Rows.Count >= maxRows)
                {
                    result.Truncated = true;
                    continue;
                }
                var row = new string?[reader.FieldCount];
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    row[i] = reader.IsDBNull(i)
                        ? null
                        : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture);
                }
                result.Rows.Add(row);
            }
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }
        return result;
    }

    public async Task<(int rows, string? error)> ExecuteAsync(string connectionName, string sql, IDictionary<string, object?>? parameters = null, int timeoutSeconds = 60, CancellationToken ct = default)
    {
        if (!_connections.TryGetValue(connectionName, out var cs))
            return (0, $"Khong tim thay connection '{connectionName}'");
        try
        {
            await using var conn = new SqlConnection(cs);
            await conn.OpenAsync(ct);
            await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = timeoutSeconds };
            if (parameters != null)
                foreach (var kv in parameters)
                    cmd.Parameters.AddWithValue("@" + kv.Key, kv.Value ?? DBNull.Value);
            return (await cmd.ExecuteNonQueryAsync(ct), null);
        }
        catch (Exception ex)
        {
            return (0, ex.Message);
        }
    }

    public async Task<(bool ok, string text)> TestAsync(string connectionName, CancellationToken ct = default)
    {
        var r = await QueryAsync(connectionName, "SELECT @@VERSION AS V", 1, 8, ct);
        if (!r.Ok) return (false, r.Error ?? "unknown error");
        var text = r.Rows.Count > 0 ? (r.Rows[0][0] ?? "") : "OK";
        var firstLine = text.Split('\n')[0].Trim();
        return (true, firstLine);
    }
}
'''

# ---------------- Core: AuditLog ----------------
FILES[r"src\GunnySuite.Core\AuditLog.cs"] = r'''using System.Text.Json;

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
'''

# ---------------- Panel: Program ----------------
FILES[r"src\GunnyPanel\Program.cs"] = r'''using GunnyPanel.Auth;
using GunnyPanel.Jobs;
using GunnyPanel.Services;
using GunnySuite.Core;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

builder.WebHost.UseUrls(cfg["Panel:Urls"] ?? "http://127.0.0.1:91");

builder.Services.AddRazorPages();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Login";
        o.AccessDeniedPath = "/Login";
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
        o.Cookie.Name = "GunnyPanel.Auth";
    });
builder.Services.AddAuthorization();

var dataDir = cfg["Paths:DataDir"] ?? Path.Combine(AppContext.BaseDirectory, "data");
var logDir = cfg["Paths:LogDir"] ?? Path.Combine(dataDir, "logs");
Directory.CreateDirectory(dataDir);

builder.Services.AddSingleton(new AuditLog(logDir));
builder.Services.AddSingleton<HttpFetcher>();
builder.Services.AddSingleton(new SqlDatabase(BuildConnections(cfg)));
builder.Services.AddSingleton<PanelUserStore>();
builder.Services.AddSingleton<JobStore>();
builder.Services.AddSingleton<JobQueue>();
builder.Services.AddHostedService<JobWorker>();
builder.Services.AddHostedService<SchedulerService>();
builder.Services.AddScoped<SyncXmlJobHandler>();
builder.Services.AddScoped<UpdateUrlTankJobHandler>();
builder.Services.AddScoped<CheckConnectionsJobHandler>();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.MapGet("/api/health", () => Results.Json(new { ok = true, time = DateTime.Now, app = "GunnyPanel" }));

app.MapGet("/api/jobs", (JobStore store) => Results.Json(store.List(100)))
   .RequireAuthorization();

app.MapGet("/api/jobs/{id:guid}", (Guid id, JobStore store) =>
{
    var job = store.Get(id);
    return job == null ? Results.NotFound() : Results.Json(job);
}).RequireAuthorization();

app.Run();

static Dictionary<string, string> BuildConnections(IConfiguration cfg)
{
    var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var child in cfg.GetSection("ConnectionStrings").GetChildren())
        if (!string.IsNullOrWhiteSpace(child.Value)) dict[child.Key] = child.Value!;
    return dict;
}
'''

# ---------------- Panel: Auth ----------------
FILES[r"src\GunnyPanel\Auth\PanelAuth.cs"] = r'''using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace GunnyPanel.Auth;

public sealed class PanelUser
{
    public string UserName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Role { get; set; } = "Moderator";
    public string Salt { get; set; } = "";
    public string PasswordHash { get; set; } = "";
}

/// <summary>User store don gian doc tu appsettings.json (giai doan test).</summary>
public sealed class PanelUserStore
{
    private readonly List<PanelUser> _users = new();

    public PanelUserStore(IConfiguration cfg)
    {
        foreach (var c in cfg.GetSection("Panel:Users").GetChildren())
        {
            _users.Add(new PanelUser
            {
                UserName = c["UserName"] ?? "",
                DisplayName = c["DisplayName"] ?? c["UserName"] ?? "",
                Role = c["Role"] ?? "Moderator",
                Salt = c["Salt"] ?? "",
                PasswordHash = (c["PasswordHash"] ?? "").ToLowerInvariant()
            });
        }
    }

    public PanelUser? Find(string userName) =>
        _users.FirstOrDefault(u => string.Equals(u.UserName, userName, StringComparison.OrdinalIgnoreCase));

    public static bool Verify(PanelUser user, string password)
    {
        if (string.IsNullOrEmpty(user.Salt)) return false;
        var hash = Hash(user.Salt, password);
        var a = Encoding.UTF8.GetBytes(hash);
        var b = Encoding.UTF8.GetBytes(user.PasswordHash);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }

    public static string Hash(string salt, string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(salt + password));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

public static class PanelAuthExtensions
{
    public static async Task SignInPanelAsync(this HttpContext ctx, PanelUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.GivenName, user.DisplayName),
            new(ClaimTypes.Role, user.Role)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await ctx.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8) });
    }
}
'''

# ---------------- Panel: Jobs core ----------------
FILES[r"src\GunnyPanel\Jobs\Jobs.cs"] = r'''using System.Collections.Concurrent;
using System.Text.Json;
using GunnySuite.Core;

namespace GunnyPanel.Jobs;

public enum JobStatus { Pending, Running, Success, Failed, Cancelled }

public sealed class JobRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public JobStatus Status { get; set; } = JobStatus.Pending;
    public int Progress { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string? Error { get; set; }
    public string? Result { get; set; }
    public List<string> Log { get; set; } = new();
    public Dictionary<string, string> Parameters { get; set; } = new();
    public bool CancelRequested { get; set; }
}

/// <summary>Luu job trong memory + snapshot ra data/jobs.json.</summary>
public sealed class JobStore
{
    private readonly ConcurrentDictionary<Guid, JobRecord> _jobs = new();
    private readonly object _lock = new();
    private readonly string _file;

    public JobStore(IConfiguration cfg)
    {
        var dataDir = cfg["Paths:DataDir"] ?? Path.Combine(AppContext.BaseDirectory, "data");
        Directory.CreateDirectory(dataDir);
        _file = Path.Combine(dataDir, "jobs.json");
        Load();
    }

    public IReadOnlyList<JobRecord> List(int max = 100)
    {
        lock (_lock)
            return _jobs.Values.OrderByDescending(j => j.CreatedAt).Take(max).ToList();
    }

    public JobRecord? Get(Guid id)
    {
        _jobs.TryGetValue(id, out var job);
        return job;
    }

    public void Add(JobRecord job)
    {
        _jobs[job.Id] = job;
        Save();
    }

    public void Save()
    {
        try
        {
            lock (_lock)
            {
                var list = _jobs.Values.OrderByDescending(j => j.CreatedAt).Take(300).ToList();
                File.WriteAllText(_file, JsonSerializer.Serialize(list));
            }
        }
        catch { }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_file)) return;
            var list = JsonSerializer.Deserialize<List<JobRecord>>(File.ReadAllText(_file)) ?? new();
            foreach (var job in list)
            {
                if (job.Status is JobStatus.Pending or JobStatus.Running)
                {
                    job.Status = JobStatus.Failed;
                    job.Error = "Bi ngat do panel restart";
                    job.FinishedAt = DateTime.Now;
                }
                _jobs[job.Id] = job;
            }
        }
        catch { }
    }
}

public sealed class JobExecutionContext
{
    public required JobRecord Job { get; init; }
    public required Dictionary<string, string> Parameters { get; init; }
    public required Action<int, string> Report { get; init; }
    public required AuditLog Audit { get; init; }
}

public interface IJobHandler
{
    string Type { get; }
    Task<string> RunAsync(JobExecutionContext ctx, CancellationToken ct);
}

public sealed class JobQueue
{
    private readonly BlockingCollection<Guid> _queue = new();
    private readonly JobStore _store;

    public JobQueue(JobStore store) => _store = store;

    public JobRecord Enqueue(string type, string title, Dictionary<string, string> parameters, string createdBy)
    {
        var job = new JobRecord { Type = type, Title = title, CreatedBy = createdBy, Parameters = parameters };
        _store.Add(job);
        _queue.Add(job.Id);
        return job;
    }

    public void RequestCancel(Guid id)
    {
        var job = _store.Get(id);
        if (job == null) return;
        job.CancelRequested = true;
        _store.Save();
    }

    public IEnumerable<Guid> Consume(CancellationToken ct) => _queue.GetConsumingEnumerable(ct);
}

/// <summary>Worker chay tuan tu cac job (1 job 1 luc).</summary>
public sealed class JobWorker : BackgroundService
{
    private readonly JobQueue _queue;
    private readonly JobStore _store;
    private readonly IServiceScopeFactory _scopes;
    private readonly AuditLog _audit;

    public JobWorker(JobQueue queue, JobStore store, IServiceScopeFactory scopes, AuditLog audit)
    {
        _queue = queue;
        _store = store;
        _scopes = scopes;
        _audit = audit;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        foreach (var id in _queue.Consume(stoppingToken))
        {
            await RunOneAsync(id, stoppingToken);
        }
    }

    private async Task RunOneAsync(Guid id, CancellationToken stoppingToken)
    {
        var job = _store.Get(id);
        if (job == null) return;
        if (job.CancelRequested)
        {
            job.Status = JobStatus.Cancelled;
            job.FinishedAt = DateTime.Now;
            _store.Save();
            return;
        }

        job.Status = JobStatus.Running;
        job.StartedAt = DateTime.Now;
        job.Log.Add($"[{DateTime.Now:HH:mm:ss}] bat dau");
        _store.Save();

        try
        {
            using var scope = _scopes.CreateScope();
            var handler = scope.ServiceProvider.GetServices<IJobHandler>()
                .FirstOrDefault(h => h.Type == job.Type)
                ?? throw new InvalidOperationException($"Khong co handler cho job '{job.Type}'");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            using var poll = new Timer(_ =>
            {
                if (!job.CancelRequested) return;
                try { cts.Cancel(); } catch { }
            }, null, 1000, 1000);

            var ctx = new JobExecutionContext
            {
                Job = job,
                Parameters = job.Parameters,
                Audit = _audit,
                Report = (percent, message) =>
                {
                    job.Progress = Math.Clamp(percent, 0, 100);
                    job.Log.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
                    if (job.Log.Count > 400) job.Log.RemoveRange(1, job.Log.Count - 400);
                    _store.Save();
                }
            };

            var result = await handler.RunAsync(ctx, cts.Token);
            job.Result = result;
            job.Progress = 100;
            job.Status = job.CancelRequested ? JobStatus.Cancelled : JobStatus.Success;
        }
        catch (OperationCanceledException)
        {
            job.Status = JobStatus.Cancelled;
            job.Error = "Da huy";
        }
        catch (Exception ex)
        {
            job.Status = JobStatus.Failed;
            job.Error = ex.Message;
            job.Log.Add($"[ERR] {ex}");
        }
        finally
        {
            job.FinishedAt = DateTime.Now;
            job.Log.Add($"[{DateTime.Now:HH:mm:ss}] ket thuc: {job.Status}");
            _store.Save();
            _audit.Write(job.CreatedBy, "job." + job.Type, $"{job.Status} | {job.Title} | {job.Error ?? job.Result ?? ""}");
        }
    }
}
'''

# ---------------- Panel: Job handlers ----------------
FILES[r"src\GunnyPanel\Jobs\Handlers.cs"] = r'''using GunnySuite.Core;

namespace GunnyPanel.Jobs;

/// <summary>Tai/mirror bo XML moi nhat tu quest server VNG ve thu muc local (IIS port 88).</summary>
public sealed class SyncXmlJobHandler : IJobHandler
{
    private static readonly string[] Presets =
    {
        "levellist","serverconfig","BallList","ActiveList","bombconfig","shopbox",
        "cardachievement","cardinfolist","cardtemplateinfo","loadboxtemp","LoadMapsItems",
        "loadpetformdata","LoadPVEItems","mounttalismansinfolist","onlinearmdropitem",
        "onlinearmlevelinfo","petexpitemprice","petlevelinfo","petskillelementinfo",
        "petskilltemplateinfo","templatemanorlist","activitysystemitems",
        "TS_Butterfly","TS_ButterflyTask","TS_CardBooklet","TS_ElfSkillBook","TS_ElfTemplate",
        "TS_Emblem","TS_JadeTemp","TS_UpgradeTemplate"
    };

    private readonly IConfiguration _cfg;
    private readonly HttpFetcher _http;

    public SyncXmlJobHandler(IConfiguration cfg, HttpFetcher http)
    {
        _cfg = cfg;
        _http = http;
    }

    public string Type => "sync-xml";

    public async Task<string> RunAsync(JobExecutionContext ctx, CancellationToken ct)
    {
        var source = Param(ctx, "source", _cfg["Vng:RequestSource"] ?? "https://quest.gn.zing.vn/");
        var dest = Param(ctx, "dest", _cfg["Paths:MirrorDir"] ?? "");
        var webRequestDir = _cfg["Paths:WebRequestDir"] ?? "";
        var overwrite = !string.Equals(Param(ctx, "overwrite", "true"), "false", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(dest)) throw new InvalidOperationException("Chua cau hinh Paths:MirrorDir");
        Directory.CreateDirectory(dest);

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(webRequestDir))
        {
            foreach (var file in Directory.EnumerateFiles(webRequestDir, "*.xml"))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (Skip(name)) continue;
                names.Add(name);
            }
        }
        foreach (var preset in Presets) names.Add(preset);

        var ordered = names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        var ok = 0; var packed = 0; var missing = 0; var failed = 0;
        var log = new List<string> { $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {source} -> {dest}" };

        for (var i = 0; i < ordered.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var name = ordered[i];
            var url = source.TrimEnd('/') + "/" + name + ".xml";
            var target = Path.Combine(dest, name + ".xml");

            if (!overwrite && File.Exists(target))
            {
                ok++;
            }
            else
            {
                var data = await _http.TryGetAsync(url, ct);
                if (data == null)
                {
                    missing++;
                    log.Add($"MISS {name}");
                }
                else if (Zlib.LooksZlib(data))
                {
                    await File.WriteAllBytesAsync(target, data, ct);
                    ok++;
                    log.Add($"OK   {name}");
                }
                else if (Zlib.LooksXml(data))
                {
                    await File.WriteAllBytesAsync(target, Zlib.Compress(data), ct);
                    ok++;
                    packed++;
                    log.Add($"OK+PACK {name}");
                }
                else
                {
                    failed++;
                    log.Add($"BAD  {name}");
                }
            }

            if (i % 5 == 0 || i == ordered.Count - 1)
                ctx.Report((i + 1) * 100 / Math.Max(1, ordered.Count), $"Da xu ly {i + 1}/{ordered.Count}: OK={ok}, nen={packed}, thieu={missing}, loi={failed}");
        }

        try { await File.AppendAllLinesAsync(Path.Combine(dest, "mirror.log"), log, ct); } catch { }

        ctx.Report(100, $"Xong: OK={ok}, nen={packed}, thieu={missing}, loi={failed}");
        return $"OK={ok}, packed={packed}, missing={missing}, failed={failed} | {dest}";
    }

    private static bool Skip(string name) =>
        name.Contains('(') || name.Contains(' ') ||
        name.EndsWith("_out", StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith("out", StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith("_bk", StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith("bkap", StringComparison.OrdinalIgnoreCase);

    private static string Param(JobExecutionContext ctx, string key, string fallback) =>
        ctx.Parameters.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : fallback;
}

/// <summary>Cap nhat Db_Gm.dbo.Url_Tank.Name (nguon cho Tool XML cua Admincp).</summary>
public sealed class UpdateUrlTankJobHandler : IJobHandler
{
    private readonly SqlDatabase _db;

    public UpdateUrlTankJobHandler(SqlDatabase db) => _db = db;

    public string Type => "update-url-tank";

    public async Task<string> RunAsync(JobExecutionContext ctx, CancellationToken ct)
    {
        var name = ctx.Parameters.GetValueOrDefault("name", "").Trim();
        var detail = ctx.Parameters.GetValueOrDefault("detail", "Official Br").Trim();

        if (string.IsNullOrWhiteSpace(name) || !name.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || !name.EndsWith("/"))
            throw new InvalidOperationException("Name phai bat dau bang http:// va ket thuc bang '/' (yeu cau cua Tool XML)");

        var (rows, error) = await _db.ExecuteAsync("Db_Gm",
            "UPDATE [dbo].[Url_Tank] SET [Name] = @name WHERE [Detail] = @detail",
            new Dictionary<string, object?> { ["name"] = name, ["detail"] = detail }, 30, ct);
        if (error != null) throw new InvalidOperationException(error);

        ctx.Report(70, $"Da UPDATE {rows} dong");
        var check = await _db.QueryAsync("Db_Gm",
            "SELECT TOP 5 [ID],[Name],[Detail] FROM [dbo].[Url_Tank] ORDER BY [ID]", 10, 15, ct);
        if (!check.Ok) return $"UPDATE {rows} dong. (khong doc lai duoc: {check.Error})";

        var lines = check.Rows.Select(r => $"ID={r[0]} Name={r[1]} Detail={r[2]}");
        return $"UPDATE {rows} dong. Url_Tank hien tai: " + string.Join(" | ", lines);
    }
}

/// <summary>Test ket noi SQL + quest server.</summary>
public sealed class CheckConnectionsJobHandler : IJobHandler
{
    private readonly SqlDatabase _db;
    private readonly HttpFetcher _http;
    private readonly IConfiguration _cfg;

    public CheckConnectionsJobHandler(SqlDatabase db, HttpFetcher http, IConfiguration cfg)
    {
        _db = db;
        _http = http;
        _cfg = cfg;
    }

    public string Type => "check-connections";

    public async Task<string> RunAsync(JobExecutionContext ctx, CancellationToken ct)
    {
        var lines = new List<string>();
        var i = 0;
        foreach (var name in _db.Names)
        {
            ct.ThrowIfCancellationRequested();
            var (ok, text) = await _db.TestAsync(name, ct);
            lines.Add($"{name}: {(ok ? "OK" : "FAIL")} - {Truncate(text, 80)}");
            ctx.Report(10 + (++i) * 60 / Math.Max(1, _db.Names.Count), $"{name}: {(ok ? "OK" : "FAIL")}");
        }

        var vng = (_cfg["Vng:RequestSource"] ?? "https://quest.gn.zing.vn/").TrimEnd('/') + "/levellist.xml";
        var (vok, vmsg) = await _http.CheckAsync(vng, ct);
        lines.Add($"VNG quest: {(vok ? "OK" : "FAIL")} - {vmsg}");
        ctx.Report(100, "Kiem tra xong");
        return string.Join("\n", lines);
    }

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max] + "...";
}
'''

# ---------------- Panel: Scheduler ----------------
FILES[r"src\GunnyPanel\Services\SchedulerService.cs"] = r'''using GunnyPanel.Jobs;

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
'''

# ---------------- appsettings.json ----------------
def mk_user(user, pw, display, role):
    salt = secrets.token_hex(8)
    h = hashlib.sha256((salt + pw).encode("utf-8")).hexdigest()
    return (user, display, role, salt, h)

USERS = [
    mk_user("admin", "Admin@123", "Administrator", "Develop"),
    mk_user("gm", "Gm@123", "GM Operator", "Admin"),
    mk_user("viewer", "View@123", "Viewer", "Moderator"),
]

users_json = ",\n".join(
    '        {{ "UserName": "{0}", "DisplayName": "{1}", "Role": "{2}", "Salt": "{3}", "PasswordHash": "{4}" }}'.format(*u)
    for u in USERS
)

FILES[r"src\GunnyPanel\appsettings.json"] = r'''{{
  "Logging": {{ "LogLevel": {{ "Default": "Information", "Microsoft.AspNetCore": "Warning" }} }},
  "AllowedHosts": "*",
  "Panel": {{
    "Urls": "http://127.0.0.1:91",
    "Title": "Gunny Panel",
    "Users": [
{USERS}
    ]
  }},
  "ConnectionStrings": {{
    "Db_Gm": "Data Source=DESKTOP-SMLR4VU;Initial Catalog=Db_Gm;User ID=sa;Password=123456;Encrypt=True;TrustServerCertificate=True;Connect Timeout=10",
    "Db_Tank": "Data Source=DESKTOP-SMLR4VU;Initial Catalog=Db_Tank;User ID=sa;Password=123456;Encrypt=True;TrustServerCertificate=True;Connect Timeout=10",
    "Db_Tank_All": "Data Source=DESKTOP-SMLR4VU;Initial Catalog=Db_Tank_All;User ID=sa;Password=123456;Encrypt=True;TrustServerCertificate=True;Connect Timeout=10"
  }},
  "Paths": {{
    "GameRoot": "C:\\Gunny\\Gunny12.6",
    "WebRequestDir": "C:\\Gunny\\Gunny12.6\\WebSite\\Request",
    "MirrorDir": "C:\\Gunny\\Gunny12.6\\WebSite\\Request\\vng-quest",
    "ApkDir": "C:\\Gunny\\ToolGunny\\Admincp\\apk",
    "DataDir": "C:\\Gunny\\ToolGunny\\GunnyPanel\\data",
    "LogDir": "C:\\Gunny\\ToolGunny\\GunnyPanel\\data\\logs"
  }},
  "Vng": {{
    "RequestSource": "https://quest.gn.zing.vn/",
    "ResourceSource": "https://gunny.vcdn.vn/flash/",
    "AdmincpUrl": "http://127.0.0.1:90/Users"
  }},
  "Scheduler": {{ "Enabled": true, "SyncXmlDailyAt": "03:00" }}
}}
'''.replace("{USERS}", users_json)


def write_all():
    os.makedirs(ROOT, exist_ok=True)
    for rel, content in FILES.items():
        path = os.path.join(ROOT, rel)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            f.write(content)
    print(f"Wrote {len(FILES)} files to {ROOT}")


def run(cmd, cwd):
    print("RUN:", " ".join(cmd))
    p = subprocess.run(cmd, cwd=cwd, capture_output=True, text=True)
    if p.stdout.strip(): print(p.stdout.strip())
    if p.returncode != 0:
        print("STDERR:", p.stderr.strip())
        raise SystemExit(f"Command failed ({p.returncode}): {cmd}")


if __name__ == "__main__":
    write_all()
    sln = os.path.join(ROOT, "GunnyPanel.sln")
    if not os.path.exists(sln):
        run(["dotnet", "new", "sln", "-n", "GunnyPanel", "-o", ROOT], ROOT)
    run(["dotnet", "sln", sln, "add",
         r"src\GunnySuite.Core\GunnySuite.Core.csproj",
         r"src\GunnyPanel\GunnyPanel.csproj"], ROOT)
    print("PART 1 DONE")
