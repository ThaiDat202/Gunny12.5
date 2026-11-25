using System.Net.Sockets;
using System.Xml.Linq;
using GunnyPanel.Jobs;
using GunnySuite.Core;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GunnyPanel.Pages;

public sealed class ServerStatusModel : PageModel
{
    private readonly IConfiguration _cfg;
    private readonly SqlDatabase _db;
    private readonly HttpFetcher _http;
    private readonly JobStore _store;

    public ServerStatusModel(IConfiguration cfg, SqlDatabase db, HttpFetcher http, JobStore store)
    {
        _cfg = cfg;
        _db = db;
        _http = http;
        _store = store;
    }

    public string? Error { get; private set; }
    public string SqlVersion { get; private set; } = "";
    public List<(string Db, string SizeMb)> DbSizes { get; } = new();
    public int UsersCount { get; private set; }
    public int GoodsCount { get; private set; }
    public int OnlineCount { get; private set; }
    public int GuildCount { get; private set; }
    public List<(string Name, int Port, bool Ok, string Info)> Ports { get; } = new();
    public List<(string Name, string Url, bool Ok, string Info)> Vng { get; } = new();
    public List<(string Key, string Value)> RoadConfig { get; } = new();
    public int XmlCount { get; private set; }
    public string XmlSize { get; private set; } = "";
    public int FlashCount { get; private set; }
    public string FlashSize { get; private set; } = "";
    public int SqlFiles { get; private set; }
    public int BombPng { get; private set; }
    public int BombFiles { get; private set; }
    public int JobsTotal { get; private set; }
    public int JobsRunning { get; private set; }
    public int JobsFailed { get; private set; }
    public string DataPath { get; private set; } = "";

    public async Task OnGetAsync()
    {
        var db = "Db_Tank";
        var ver = await _db.QueryAsync(db, "SELECT @@VERSION", 1, 10);
        if (!ver.Ok) Error = ver.Error;
        else SqlVersion = (ver.FirstCell() ?? "").Split('\n')[0].Trim();

        var sizes = await _db.QueryAsync(db,
            "SELECT DB_NAME(database_id) AS DbName, CAST(SUM(size) * 8.0 / 1024.0 AS decimal(10,1)) AS SizeMb " +
            "FROM sys.master_files " +
            "WHERE database_id IN (DB_ID('Db_Gm'), DB_ID('Db_Tank'), DB_ID('Db_Tank_All')) " +
            "GROUP BY database_id ORDER BY DB_NAME(database_id)", 10, 20);
        if (sizes.Ok)
            foreach (var row in sizes.Rows) DbSizes.Add((row.ElementAtOrDefault(0) ?? "", row.ElementAtOrDefault(1) ?? ""));

        var counts = await _db.QueryAsync(db,
            "SELECT (SELECT COUNT(*) FROM dbo.Sys_Users_Detail) AS Users, " +
            "(SELECT COUNT(*) FROM dbo.Sys_Users_Goods) AS Goods, " +
            "(SELECT COUNT(*) FROM dbo.Sys_Users_Detail WHERE State > 0) AS OnlineUsers, " +
            "(SELECT COUNT(*) FROM dbo.Consortia WHERE IsExist = 1) AS Guilds", 1, 20);
        if (counts.Ok && counts.Rows.Count > 0)
        {
            var r = counts.Rows[0];
            UsersCount = UserRow.ParseInt(r.ElementAtOrDefault(0));
            GoodsCount = UserRow.ParseInt(r.ElementAtOrDefault(1));
            OnlineCount = UserRow.ParseInt(r.ElementAtOrDefault(2));
            GuildCount = UserRow.ParseInt(r.ElementAtOrDefault(3));
        }

        await CheckPortsAsync();
        await CheckVngAsync();
        LoadRoadConfig();
        LoadLocalAssets();
        LoadJobs();
        DataPath = _cfg["Paths:DataDir"] ?? "";
    }

    private async Task CheckPortsAsync()
    {
        var targets = new (string Name, int Port)[]
        {
            ("Panel", 90),
            ("WebSite Request (IIS)", 88),
            ("SQL Server", 1433),
            ("Road.Service (game)", 8527),
            ("Login server", 1593),
            ("Fight server", 7539)
        };
        foreach (var (name, port) in targets)
        {
            var (ok, info) = await CheckTcpAsync("127.0.0.1", port);
            Ports.Add((name, port, ok, info));
        }
    }

    private static async Task<(bool Ok, string Info)> CheckTcpAsync(string host, int port, int timeoutMs = 700)
    {
        try
        {
            using var client = new TcpClient();
            var connect = client.ConnectAsync(host, port);
            var done = await Task.WhenAny(connect, Task.Delay(timeoutMs));
            if (done != connect) return (false, "timeout");
            await connect;
            return (true, "đang mở");
        }
        catch (Exception ex)
        {
            return (false, ex.GetType().Name);
        }
    }

    private async Task CheckVngAsync()
    {
        var quest = (_cfg["Vng:RequestSource"] ?? "https://quest.gn.zing.vn/").TrimEnd('/') + "/levellist.xml";
        var resource = (_cfg["Vng:ResourceSource"] ?? "https://gunny.vcdn.vn/flash/").TrimEnd('/') + "/1.png";
        Vng.Add(("Quest XML", quest, false, ""));
        Vng.Add(("Resource flash", resource, false, ""));

        var checks = new List<(string Name, string Url, bool Ok, string Info)>();
        foreach (var (name, url) in new[] { ("Quest XML", quest), ("Resource flash", resource) })
        {
            var (ok, info) = await _http.CheckAsync(url);
            checks.Add((name, url, ok, info));
        }
        Vng.Clear();
        Vng.AddRange(checks);
    }

    private void LoadRoadConfig()
    {
        try
        {
            var path = _cfg["Paths:RoadServiceConfig"];
            if (string.IsNullOrWhiteSpace(path))
                path = Path.Combine(_cfg["Paths:GameRoot"] ?? "", "Service", "Service", "Road", "Road.Service.exe.config");
            if (!System.IO.File.Exists(path)) return;

            var doc = XDocument.Load(path);
            var keys = new[]
            {
                "ServerID", "ServerName", "ServerNameShort", "AreaID", "AreaName", "Port", "IP",
                "RequestUrl", "Resource", "Request", "Edition", "MaxClientCount",
                "LoginServerPort", "LoginServerIp", "FightServerPort", "FightServerIp"
            };
            foreach (var add in doc.Descendants("add"))
            {
                var key = add.Attribute("key")?.Value;
                var value = add.Attribute("value")?.Value;
                if (key == null || value == null) continue;
                if (keys.Contains(key, StringComparer.OrdinalIgnoreCase))
                    RoadConfig.Add((key, value));
            }
        }
        catch { }
    }

    private void LoadLocalAssets()
    {
        try
        {
            var mirror = _cfg["Paths:MirrorDir"] ?? "";
            if (Directory.Exists(mirror))
            {
                var files = new DirectoryInfo(mirror).GetFiles("*.xml");
                XmlCount = files.Length;
                XmlSize = FormatMb(files.Sum(f => f.Length));
            }

            var flash = Path.Combine(_cfg["Paths:GameRoot"] ?? "", "WebSite", "Flash");
            if (Directory.Exists(flash))
            {
                var files = new DirectoryInfo(flash).GetFiles("*", SearchOption.AllDirectories);
                FlashCount = files.Length;
                FlashSize = FormatMb(files.Sum(f => f.Length));
            }

            var sqlDir = Path.Combine(_cfg["Paths:DataDir"] ?? "", "sql");
            if (Directory.Exists(sqlDir)) SqlFiles = new DirectoryInfo(sqlDir).GetFiles("*.sql").Length;

            var outDir = Path.Combine(_cfg["Paths:DataDir"] ?? "", "out");
            if (Directory.Exists(Path.Combine(outDir, "bomb-png")))
                BombPng = new DirectoryInfo(Path.Combine(outDir, "bomb-png")).GetFiles("*.png").Length;
            if (Directory.Exists(Path.Combine(outDir, "bomb")))
                BombFiles = new DirectoryInfo(Path.Combine(outDir, "bomb")).GetFiles("*.bomb").Length;
        }
        catch { }
    }

    private void LoadJobs()
    {
        var jobs = _store.List(500);
        JobsTotal = jobs.Count;
        JobsRunning = jobs.Count(j => j.Status is JobStatus.Pending or JobStatus.Running);
        JobsFailed = jobs.Count(j => j.Status == JobStatus.Failed);
    }

    private static string FormatMb(long bytes) => (bytes / 1024.0 / 1024.0).ToString("0.0") + " MB";
}
