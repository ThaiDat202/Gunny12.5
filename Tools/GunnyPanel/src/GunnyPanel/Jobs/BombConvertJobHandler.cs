using System.Globalization;
using GunnySuite.Core;

namespace GunnyPanel.Jobs;

/// <summary>
/// Sprint 2: chuyen doi file .bomb (mask 1bpp cua Gunny) <-> PNG de sua/kiem tra.
/// - to-png: doc folder .bomb -> xuat PNG (mask trang tren nen den)
/// - to-bomb: doc folder PNG -> xuat .bomb (alpha > 0 va do sang >= threshold)
/// Chi DOC tu Service\...\Bomb; chi GHI vao data\out cua GunnyPanel.
/// </summary>
public sealed class BombConvertJobHandler : IJobHandler
{
    private readonly IConfiguration _cfg;

    public BombConvertJobHandler(IConfiguration cfg) => _cfg = cfg;

    public string Type => "bomb-convert";

    public async Task<string> RunAsync(JobExecutionContext ctx, CancellationToken ct)
    {
        var mode = Param(ctx, "mode", "to-png").ToLowerInvariant();
        if (mode != "to-png" && mode != "to-bomb")
            throw new InvalidOperationException("mode phai la 'to-png' hoac 'to-bomb'");

        var source = Param(ctx, "source",
            _cfg["Paths:BombDir"] ?? Path.Combine(_cfg["Paths:GameRoot"] ?? "", "Service", "Service", "Road", "Bomb"));
        var dataDir = _cfg["Paths:DataDir"] ?? Path.Combine(AppContext.BaseDirectory, "data");
        var defaultOut = mode == "to-png"
            ? Path.Combine(dataDir, "out", "bomb-png")
            : Path.Combine(dataDir, "out", "bomb");
        var output = Param(ctx, "output", defaultOut);

        var maxFiles = 0;
        int.TryParse(Param(ctx, "maxFiles", "0"), NumberStyles.Integer, CultureInfo.InvariantCulture, out maxFiles);
        var threshold = 128;
        int.TryParse(Param(ctx, "threshold", "128"), NumberStyles.Integer, CultureInfo.InvariantCulture, out threshold);
        threshold = Math.Clamp(threshold, 1, 254);
        var overwrite = !string.Equals(Param(ctx, "overwrite", "true"), "false", StringComparison.OrdinalIgnoreCase);

        if (!Directory.Exists(source))
            throw new InvalidOperationException("Khong tim thay thu muc nguon: " + source);

        var pattern = mode == "to-png" ? "*.bomb" : "*.png";
        var files = Directory.GetFiles(source, pattern, SearchOption.TopDirectoryOnly)
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (maxFiles > 0 && files.Count > maxFiles) files = files.Take(maxFiles).ToList();

        Directory.CreateDirectory(output);
        ctx.Report(4, $"{mode}: {files.Count} file tu {source}");
        ctx.Report(8, $"Xuat vao: {output}");

        var ok = 0;
        var fail = 0;
        var errors = new List<string>();
        var done = 0;

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            done++;
            var name = Path.GetFileNameWithoutExtension(file);
            var outPath = Path.Combine(output, name + (mode == "to-png" ? ".png" : ".bomb"));

            if (!overwrite && File.Exists(outPath))
            {
                Report(ctx, done, files.Count, ok, fail, "bo qua " + name);
                continue;
            }

            try
            {
                if (mode == "to-png")
                {
                    var mask = Bomb.Read(file);
                    Bomb.ToPng(mask, outPath);
                }
                else
                {
                    var mask = Bomb.FromPng(file, threshold);
                    Bomb.Write(outPath, mask);
                }
                ok++;
            }
            catch (Exception ex)
            {
                fail++;
                if (errors.Count < 30) errors.Add(Path.GetFileName(file) + ": " + ex.Message);
            }

            Report(ctx, done, files.Count, ok, fail, "xu ly " + name);
            await Task.CompletedTask;
        }

        ctx.Report(100, $"Xong: OK={ok}, loi={fail}");
        var summary = $"{mode}: {files.Count} file\nNguon: {source}\nXuat: {output}\nOK={ok}, loi={fail}\n";
        if (errors.Count > 0) summary += "Loi (toi da 30):\n" + string.Join("\n", errors);
        return summary;
    }

    private static void Report(JobExecutionContext ctx, int done, int total, int ok, int fail, string message)
    {
        if (done % 5 != 0 && done != total) return;
        var percent = 8 + (int)(90.0 * done / Math.Max(1, total));
        ctx.Report(percent, $"{done}/{total} — OK={ok}, loi={fail} — {message}");
    }

    private static string Param(JobExecutionContext ctx, string key, string fallback) =>
        ctx.Parameters.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : fallback;
}
