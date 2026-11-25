using System.Globalization;
using GunnySuite.Core;

namespace GunnyPanel.Jobs;

/// <summary>
/// Sprint 2: dong bo resource (flash) tu resource server VNG ve thu muc WebSite\Flash local.
/// - Mac dinh DRY-RUN: chi quet file + thong ke, KHONG tai.
/// - Bo tick dry-run moi tai that; co the gioi han maxFiles de thu truoc.
/// </summary>
public sealed class ResourceSyncJobHandler : IJobHandler
{
    private readonly IConfiguration _cfg;
    private readonly HttpFetcher _http;

    public ResourceSyncJobHandler(IConfiguration cfg, HttpFetcher http)
    {
        _cfg = cfg;
        _http = http;
    }

    public string Type => "resource-sync";

    public async Task<string> RunAsync(JobExecutionContext ctx, CancellationToken ct)
    {
        var source = Param(ctx, "source", _cfg["Vng:ResourceSource"] ?? "https://gunny.vcdn.vn/flash/");
        var gameRoot = _cfg["Paths:GameRoot"] ?? "";
        var target = Param(ctx, "target", Path.Combine(gameRoot, "WebSite", "Flash"));
        var dryRun = !string.Equals(Param(ctx, "dryRun", "true"), "false", StringComparison.OrdinalIgnoreCase);
        var overwrite = !string.Equals(Param(ctx, "overwrite", "true"), "false", StringComparison.OrdinalIgnoreCase);
        var maxFiles = 0;
        int.TryParse(Param(ctx, "maxFiles", "0"), NumberStyles.Integer, CultureInfo.InvariantCulture, out maxFiles);

        if (string.IsNullOrWhiteSpace(target) || !Directory.Exists(target))
            throw new InvalidOperationException("Khong tim thay thu muc dich: " + target);

        ctx.Report(5, "Dang quet file local...");
        var files = Directory.EnumerateFiles(target, "*", SearchOption.AllDirectories).ToList();
        var totalBytes = 0L;
        foreach (var f in files)
        {
            try { totalBytes += new FileInfo(f).Length; } catch { }
        }
        var totalMb = (totalBytes / 1024.0 / 1024.0).ToString("0.0", CultureInfo.InvariantCulture);

        ctx.Report(12, $"Tim thay {files.Count} file ({totalMb} MB) trong {target}");
        ctx.Report(15, $"Nguon: {source} | dryRun={dryRun} | overwrite={overwrite} | maxFiles={maxFiles}");

        var work = files;
        if (maxFiles > 0 && work.Count > maxFiles) work = work.Take(maxFiles).ToList();

        if (dryRun)
        {
            var sample = new List<string>();
            foreach (var f in work.Take(15))
            {
                var rel = Path.GetRelativePath(target, f).Replace('\\', '/');
                sample.Add($"{rel}  ->  {Url(source, rel)}");
            }
            ctx.Report(100, "Dry-run xong (chua tai file nao)");
            return "DRY-RUN: khong tai file nao.\n" +
                   $"Local: {files.Count} file / {totalMb} MB\n" +
                   $"Se tai (neu bo dry-run): {work.Count} file, toi da {maxFiles} (0 = tat ca)\n" +
                   $"Source: {source}\n" +
                   "Vi du URL:\n" + string.Join("\n", sample);
        }

        var ok = 0;
        var fail = 0;
        var skipped = 0;
        var errors = new List<string>();
        var done = 0;

        foreach (var localFile in work)
        {
            ct.ThrowIfCancellationRequested();
            var rel = Path.GetRelativePath(target, localFile).Replace('\\', '/');
            var url = Url(source, rel);
            done++;

            if (!overwrite)
            {
                var fi = new FileInfo(localFile);
                if (fi.Exists) { skipped++; Progress(ctx, done, work.Count, ok, fail, skipped); continue; }
            }

            var data = await _http.TryGetAsync(url, ct);
            if (data == null)
            {
                fail++;
                if (errors.Count < 40) errors.Add(rel);
            }
            else
            {
                try
                {
                    var dir = Path.GetDirectoryName(localFile);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    await File.WriteAllBytesAsync(localFile, data, ct);
                    ok++;
                }
                catch (Exception ex)
                {
                    fail++;
                    if (errors.Count < 40) errors.Add(rel + " (" + ex.Message + ")");
                }
            }

            Progress(ctx, done, work.Count, ok, fail, skipped);
        }

        ctx.Report(100, $"Xong: OK={ok}, loi={fail}, bo qua={skipped}");
        var summary = $"Resource sync: {target}\n" +
                      $"Nguon: {source}\n" +
                      $"Tong local: {files.Count} file / {totalMb} MB\n" +
                      $"Da xu ly: {work.Count} file | OK={ok}, loi={fail}, bo qua={skipped}\n";
        if (errors.Count > 0)
            summary += "File loi (toi da 40):\n" + string.Join("\n", errors);
        return summary;
    }

    private static void Progress(JobExecutionContext ctx, int done, int total, int ok, int fail, int skipped)
    {
        if (done % 10 != 0 && done != total) return;
        var percent = 15 + (int)(82.0 * done / Math.Max(1, total));
        ctx.Report(percent, $"Tai resource {done}/{total}: OK={ok}, loi={fail}, bo qua={skipped}");
    }

    private static string Url(string source, string relative) =>
        source.TrimEnd('/') + "/" + relative;

    private static string Param(JobExecutionContext ctx, string key, string fallback) =>
        ctx.Parameters.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : fallback;
}
