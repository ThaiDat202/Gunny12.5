using GunnyPanel.Jobs;
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
