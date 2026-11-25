using GunnySuite.Core;

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
