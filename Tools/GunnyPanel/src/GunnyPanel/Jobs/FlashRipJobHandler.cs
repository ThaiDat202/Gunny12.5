using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO.Enumeration;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace GunnyPanel.Jobs;

// ---------------------------------------------------------------------------
// DTO: report do job flash-rip ghi ra <target>\_rip-report.json
// (trang /FlashRip doc lai de hien thi ket qua)
// ---------------------------------------------------------------------------

public sealed class FlashRipStats
{
    public int Total { get; set; }
    public int Excluded { get; set; }
    public int Ok { get; set; }
    public int Missing { get; set; }
    public int Failed { get; set; }
    public int Skipped { get; set; }
    public long Bytes { get; set; }
    public double Seconds { get; set; }
}

public sealed class FlashRipFileResult
{
    public string Path { get; set; } = "";
    public string Url { get; set; } = "";
    public string Status { get; set; } = ""; // ok | missing | failed | skipped
    public long Size { get; set; }
    public string? Sha256 { get; set; }
    public string? Error { get; set; }
}

public sealed class FlashRipReport
{
    public Guid JobId { get; set; }
    public string Source { get; set; } = "";
    public string Reference { get; set; } = "";
    public string Target { get; set; } = "";
    public string Mode { get; set; } = "rip"; // rip | scan
    public string Exclude { get; set; } = "";
    public DateTime GeneratedAt { get; set; } = DateTime.Now;
    public FlashRipStats Stats { get; set; } = new();
    public List<FlashRipFileResult> Files { get; set; } = new();
}

/// <summary>
/// Rip (mirror) toàn bộ flash UI của nhà phát hành VNG từ CDN về 1 thư mục.
///
/// Vì CDN gunny.vcdn.vn không cho directory listing, danh sách file được lấy từ
/// "reference" (mặc định WebSite\Flash - cây client local đã biết). Tool tự HEAD/GET
/// từng file, file nào CDN không có (404) thì ghi nhận "missing" và bỏ qua.
///
/// - exclude: mỗi dòng 1 pattern (hỗ trợ *), mặc định 2.png + 4.png (file core gắn code game).
/// - dry-run: chỉ HEAD kiểm tra CDN, không tải, không ghi file.
/// - tải thật: GET song song, ghi file + _rip-report.json + _rip-manifest.json.
/// </summary>
public sealed class FlashRipJobHandler : IJobHandler
{
    private const int MaxRetries = 2;
    private static readonly string[] DefaultExcludes = { "2.png", "4.png" };
    private static readonly string[] InternalNames = { "_rip-report.json", "_rip-manifest.json" };

    private readonly IConfiguration _cfg;

    public FlashRipJobHandler(IConfiguration cfg) => _cfg = cfg;

    public string Type => "flash-rip";

    public async Task<string> RunAsync(JobExecutionContext ctx, CancellationToken ct)
    {
        // ---------------- tham so ----------------
        var source = Param(ctx, "source", "https://gunny.vcdn.vn/flash/").Trim();
        if (!source.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !source.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Source phai la URL http(s), vd: https://gunny.vcdn.vn/flash/");
        source = source.TrimEnd('/') + "/";

        var reference = Path.GetFullPath(Param(ctx, "reference", ""));
        if (!Directory.Exists(reference))
            throw new InvalidOperationException("Khong tim thay thu muc tham chieu (reference): " + reference);

        var target = Path.GetFullPath(Param(ctx, "target", ""));
        if (string.IsNullOrWhiteSpace(target) || target.Length <= 3)
            throw new InvalidOperationException("Thu muc dich (target) khong hop le: " + target);

        var dryRun = IsTrue(Param(ctx, "dryRun", "false"));
        var overwrite = IsTrue(Param(ctx, "overwrite", "true"));
        var allowInPlace = IsTrue(Param(ctx, "allowInPlace", "false"));
        var maxFiles = ParseInt(Param(ctx, "maxFiles", "0"), 0, 0, int.MaxValue);
        var concurrency = ParseInt(Param(ctx, "concurrency", "8"), 8, 1, 16);
        var excludeRaw = ctx.Parameters.ContainsKey("exclude")
            ? (ctx.Parameters["exclude"] ?? "")
            : string.Join('\n', DefaultExcludes);
        var patterns = excludeRaw
            .Split(new[] { '\r', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => p.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var excludeLabel = patterns.Count == 0 ? "(khong loai tru)" : string.Join(", ", patterns);

        // ---------------- quet danh sach file tu reference ----------------
        ctx.Report(2, "Dang quet thu muc tham chieu: " + reference);
        var allCount = 0;
        var excluded = 0;
        var candidates = new List<string>();
        var targetInsideReference = IsInside(target, reference);
        foreach (var full in SafeEnumerate(reference))
        {
            ct.ThrowIfCancellationRequested();
            allCount++;
            var rel = Path.GetRelativePath(reference, full).Replace('\\', '/');
            if (targetInsideReference && IsInside(full, target)) continue; // khong quet lai chinh folder output
            var name = Path.GetFileName(rel);
            if (InternalNames.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase))) continue;
            if (IsExcluded(rel, patterns)) { excluded++; continue; }
            candidates.Add(rel);
        }
        candidates.Sort(StringComparer.OrdinalIgnoreCase);
        if (maxFiles > 0 && candidates.Count > maxFiles) candidates = candidates.Take(maxFiles).ToList();

        var localBytes = 0L;
        foreach (var rel in candidates)
        {
            try { localBytes += new FileInfo(Path.Combine(reference, rel.Replace('/', Path.DirectorySeparatorChar))).Length; } catch { }
        }

        if (candidates.Count == 0)
            throw new InvalidOperationException("Khong co file nao de rip sau khi ap dung bo loc. Kiem tra reference/exclude.");

        ctx.Report(5,
            $"Reference: {allCount} file | loc con {candidates.Count} file ({Mb(localBytes)}) | loai tru {excluded} ({excludeLabel})");
        if (maxFiles > 0) ctx.Report(6, $"Gioi han maxFiles={maxFiles}");

        // ---------------- che do quet (dry-run) ----------------
        if (dryRun)
            return await RunScanAsync(ctx, ct, source, reference, target, candidates, excludeLabel, concurrency);

        // ---------------- che do tai that ----------------
        var sameAsReference = string.Equals(Path.GetFullPath(target), Path.GetFullPath(reference), StringComparison.OrdinalIgnoreCase);
        if (sameAsReference && !allowInPlace)
            throw new InvalidOperationException(
                "Target trung voi Reference (ghi de truc tiep client dang chay). " +
                "Neu that su muon, bat 'Cho phep ghi de truc tiep' o trang Rip Flash.");
        if (IsInside(target, reference) && !sameAsReference)
            ctx.Report(7, "LUU Y: target nam trong cay Reference; tool se bo qua target khi quet reference.");

        // Pre-check dung luong trong (uoc tinh theo size reference, thuong lon hon CDN).
        try
        {
            var root = Path.GetPathRoot(target);
            if (!string.IsNullOrEmpty(root))
            {
                var drive = new DriveInfo(root);
                if (drive.IsReady && localBytes > 0 && drive.AvailableFreeSpace < localBytes + 200L * 1024 * 1024)
                    throw new InvalidOperationException(
                        $"Khong du dung luong trong o {root}: con {Mb(drive.AvailableFreeSpace)}, can ~{Mb(localBytes)} + 200 MB.");
            }
        }
        catch (InvalidOperationException) { throw; }
        catch { /* khong doc duoc drive -> bo qua pre-check */ }

        Directory.CreateDirectory(target);
        ctx.Report(8, $"TAI THAT -> {target} | overwrite={overwrite} | song song={concurrency}");
        if (sameAsReference)
            ctx.Report(9, "CANH BAO: target trung reference - file local se bi ghi de (tru cac file bi loai tru).");

        var sw = Stopwatch.StartNew();
        var results = new ConcurrentBag<FlashRipFileResult>();
        int done = 0, ok = 0, missing = 0, failed = 0, skipped = 0;
        long bytes = 0;
        var reportLock = new object();

        using var http = CreateHttp();
        using var sem = new SemaphoreSlim(concurrency);
        var total = candidates.Count;

        var tasks = candidates.Select(async rel =>
        {
            await sem.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                ct.ThrowIfCancellationRequested();
                var url = BuildUrl(source, rel);
                var dest = Path.Combine(target, rel.Replace('/', Path.DirectorySeparatorChar));
                var rec = new FlashRipFileResult { Path = rel, Url = url };

                // Ghi de = false: chi bo qua khi file da co VA size khop CDN (tranh bo qua file hong).
                var skipExisting = false;
                if (!overwrite && File.Exists(dest))
                {
                    long localLen = -1;
                    try { localLen = new FileInfo(dest).Length; } catch { }
                    if (localLen >= 0)
                    {
                        var (hstatus, hlen, _) = await HeadWithRetryAsync(http, url, ct).ConfigureAwait(false);
                        if (hstatus == HttpStatusCode.OK)
                        {
                            if (hlen <= 0 || hlen == localLen)
                            {
                                skipExisting = true;
                            }
                            else
                            {
                                rec.Error = $"local {localLen} B != CDN {hlen} B -> tai lai";
                            }
                        }
                        else
                        {
                            // CDN khong tra loi duoc (loi tam thoi) -> giu file local, coi nhu skipped.
                            skipExisting = true;
                        }
                    }
                }

                if (skipExisting)
                {
                    rec.Status = "skipped";
                    try { rec.Size = new FileInfo(dest).Length; } catch { }
                    Interlocked.Increment(ref skipped);
                }
                else
                {
                    var (status, data, error) = await GetWithRetryAsync(http, url, ct).ConfigureAwait(false);
                    if (status == HttpStatusCode.OK && data != null)
                    {
                        var tmp = dest + ".part";
                        try
                        {
                            var dir = Path.GetDirectoryName(dest);
                            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                            await File.WriteAllBytesAsync(tmp, data, ct).ConfigureAwait(false);
                            File.Move(tmp, dest, true);
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            rec.Status = "failed";
                            rec.Error = ex.Message;
                            Interlocked.Increment(ref failed);
                            results.Add(rec);
                            Interlocked.Increment(ref done);
                            return;
                        }
                        finally
                        {
                            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                        }

                        rec.Status = "ok";
                        rec.Size = data.Length;
                        rec.Sha256 = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
                        Interlocked.Increment(ref ok);
                        Interlocked.Add(ref bytes, data.Length);
                    }
                    else if (status is HttpStatusCode.NotFound or HttpStatusCode.Forbidden or HttpStatusCode.Gone)
                    {
                        rec.Status = "missing";
                        rec.Error = "HTTP " + (int)status;
                        Interlocked.Increment(ref missing);
                    }
                    else
                    {
                        rec.Status = "failed";
                        rec.Error = error ?? ("HTTP " + (int)status);
                        Interlocked.Increment(ref failed);
                    }
                }

                results.Add(rec);
                var d = Interlocked.Increment(ref done);
                if (d % 5 == 0 || d == total)
                {
                    var elapsed = Math.Max(0.001, sw.Elapsed.TotalSeconds);
                    var mbps = Interlocked.Read(ref bytes) / 1048576.0 / elapsed;
                    var eta = EstimateEta(localBytes, Interlocked.Read(ref bytes), mbps);
                    lock (reportLock)
                        ctx.Report(8 + (int)(90.0 * d / total),
                            $"TAI {d}/{total} | OK {ok} | 404 {missing} | loi {failed} | bo qua {skipped} | {mbps:F1} MB/s | ETA {eta}");
                }
            }
            finally
            {
                sem.Release();
            }
        }).ToList();

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            WriteReport(ctx.Job, target, source, reference, excludeLabel, "rip", new FlashRipStats
            {
                Total = total, Excluded = excluded, Ok = ok, Missing = missing, Failed = failed,
                Skipped = skipped, Bytes = bytes, Seconds = Math.Round(sw.Elapsed.TotalSeconds, 1)
            }, results);
            throw;
        }

        sw.Stop();
        var stats = new FlashRipStats
        {
            Total = total,
            Excluded = excluded,
            Ok = ok,
            Missing = missing,
            Failed = failed,
            Skipped = skipped,
            Bytes = bytes,
            Seconds = Math.Round(sw.Elapsed.TotalSeconds, 1)
        };
        var reportPath = WriteReport(ctx.Job, target, source, reference, excludeLabel, "rip", stats, results);
        var speed = stats.Seconds > 0 ? stats.Bytes / 1048576.0 / stats.Seconds : 0;
        ctx.Report(100, $"XONG: OK {ok}/{total} | 404 {missing} | loi {failed} | bo qua {skipped} | {Mb(bytes)} | {speed:F1} MB/s");

        return string.Join("\n", new[]
        {
            "FLASH RIP (tai that)",
            "Nguon:    " + source,
            "Dich:     " + target,
            "Reference:" + reference,
            $"Loai tru: {excludeLabel} ({excluded} file)",
            $"Ket qua:  OK={ok}, 404={missing}, loi={failed}, bo qua={skipped} / tong {total} file",
            $"Da tai:   {Mb(bytes)} trong {stats.Seconds:F1}s ({speed:F1} MB/s)",
            "Report:   " + reportPath
        });
    }

    // ------------------------------------------------------------------
    // Dry-run: HEAD tung file, khong tai
    // ------------------------------------------------------------------
    private static async Task<string> RunScanAsync(
        JobExecutionContext ctx, CancellationToken ct,
        string source, string reference, string target,
        List<string> candidates, string excludeLabel, int concurrency)
    {
        ctx.Report(8, "CHE DO QUET: chi HEAD kiem tra CDN, khong tai file nao.");
        var sw = Stopwatch.StartNew();
        int done = 0, available = 0, missing = 0, failed = 0;
        long availableBytes = 0;
        var samples = new ConcurrentBag<string>();
        var problems = new ConcurrentBag<string>();
        var reportLock = new object();
        var total = candidates.Count;

        using var http = CreateHttp();
        using var sem = new SemaphoreSlim(concurrency);
        var tasks = candidates.Select(async rel =>
        {
            await sem.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                ct.ThrowIfCancellationRequested();
                var url = BuildUrl(source, rel);
                var (status, length, error) = await HeadWithRetryAsync(http, url, ct).ConfigureAwait(false);
                var d = Interlocked.Increment(ref done);
                if (status == HttpStatusCode.OK)
                {
                    Interlocked.Increment(ref available);
                    if (length > 0) Interlocked.Add(ref availableBytes, length);
                    if (samples.Count < 8) samples.Add("  " + rel + "  (" + Mb(length) + ")");
                }
                else if (status is HttpStatusCode.NotFound or HttpStatusCode.Forbidden or HttpStatusCode.Gone)
                {
                    Interlocked.Increment(ref missing);
                    if (problems.Count < 40) problems.Add("404  " + rel);
                }
                else
                {
                    Interlocked.Increment(ref failed);
                    if (problems.Count < 40) problems.Add("ERR  " + rel + "  " + (error ?? ("HTTP " + (int)status)));
                }

                if (d % 25 == 0 || d == total)
                {
                    var el = Math.Max(0.001, sw.Elapsed.TotalSeconds);
                    lock (reportLock)
                        ctx.Report(8 + (int)(90.0 * d / total),
                            $"QUET {d}/{total} | CDN co {available} | 404 {missing} | loi {failed} | {d / el:F0} req/s");
                }
            }
            finally
            {
                sem.Release();
            }
        }).ToList();

        await Task.WhenAll(tasks);
        sw.Stop();
        ctx.Report(100, $"QUET XONG: CDN co {available}/{total} file ({Mb(availableBytes)}) | 404 {missing} | loi {failed}");

        var lines = new List<string>
        {
            "FLASH RIP (quet thu - chua tai file nao)",
            "Nguon:    " + source,
            "Reference:" + reference,
            "Dich (khi tai that): " + target,
            $"Loai tru: {excludeLabel}",
            $"Ket qua:  CDN co {available}/{total} file ({Mb(availableBytes)}), 404={missing}, loi={failed}",
            "File mau:",
        };
        lines.AddRange(samples.ToArray().OrderBy(s => s));
        if (problems.Count > 0)
        {
            lines.Add("File khong co tren CDN (toi da 40):");
            lines.AddRange(problems.ToArray().OrderBy(s => s));
        }
        return string.Join("\n", lines);
    }

    // ------------------------------------------------------------------
    // Report / manifest
    // ------------------------------------------------------------------
    private string WriteReport(
        JobRecord job, string target, string source, string reference, string excludeLabel, string mode,
        FlashRipStats stats, IEnumerable<FlashRipFileResult> results)
    {
        var ordered = results.OrderBy(r => r.Path, StringComparer.OrdinalIgnoreCase).ToList();
        var generatedAt = DateTime.Now;
        var report = new FlashRipReport
        {
            JobId = job.Id,
            Source = source,
            Reference = reference,
            Target = target,
            Mode = mode,
            Exclude = excludeLabel,
            GeneratedAt = generatedAt,
            Stats = stats,
            Files = ordered
        };
        var opts = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        var reportJson = JsonSerializer.Serialize(report, opts);

        var manifest = new
        {
            jobId = job.Id,
            source,
            generatedAt,
            mode,
            exclude = excludeLabel,
            note = "Manifest chi liet ke file tai OK trong lan chay nay (khong gom file bo qua).",
            stats,
            files = ordered
                .Where(r => r.Status == "ok")
                .Select(r => new { path = r.Path, url = r.Url, size = r.Size, sha256 = r.Sha256 })
                .ToList()
        };
        var manifestJson = JsonSerializer.Serialize(manifest, opts);

        Directory.CreateDirectory(target);
        var reportPath = Path.Combine(target, "_rip-report.json");
        WriteAllTextAtomic(reportPath, reportJson);
        WriteAllTextAtomic(Path.Combine(target, "_rip-manifest.json"), manifestJson);

        // Ban sao theo job (data/rip/jobs) de xem lai lich su khong bi lan chay sau cung target ghi de.
        try
        {
            var dataDir = _cfg["Paths:DataDir"] ?? Path.Combine(AppContext.BaseDirectory, "data");
            var jobsDir = Path.Combine(dataDir, "rip", "jobs");
            Directory.CreateDirectory(jobsDir);
            var key = job.Id.ToString("N");
            WriteAllTextAtomic(Path.Combine(jobsDir, key + "-report.json"), reportJson);
            WriteAllTextAtomic(Path.Combine(jobsDir, key + "-manifest.json"), manifestJson);
        }
        catch { }
        return reportPath;
    }

    private static void WriteAllTextAtomic(string path, string content)
    {
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, content);
        File.Move(tmp, path, true);
    }

    // ------------------------------------------------------------------
    // HTTP helpers
    // ------------------------------------------------------------------
    private static HttpClient CreateHttp()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(15),
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer = 16
        };
        var http = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(5) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("GunnyPanel-FlashRip/1.0 (+local)");
        return http;
    }

    private static async Task<(HttpStatusCode Status, byte[]? Data, string? Error)> GetWithRetryAsync(
        HttpClient http, string url, CancellationToken ct)
    {
        string? lastError = null;
        for (var attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                using var resp = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                if (resp.IsSuccessStatusCode)
                {
                    var data = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                    return (resp.StatusCode, data, null);
                }
                if (resp.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden or HttpStatusCode.Gone)
                    return (resp.StatusCode, null, null);
                lastError = "HTTP " + (int)resp.StatusCode;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex.GetType().Name + ": " + ex.Message;
            }
            if (attempt < MaxRetries) await Task.Delay(250 * (attempt + 1), ct).ConfigureAwait(false);
        }
        return (HttpStatusCode.InternalServerError, null, lastError);
    }

    private static async Task<(HttpStatusCode Status, long Length, string? Error)> HeadWithRetryAsync(
        HttpClient http, string url, CancellationToken ct)
    {
        string? lastError = null;
        for (var attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Head, url);
                using var resp = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
                if (resp.IsSuccessStatusCode)
                    return (resp.StatusCode, resp.Content.Headers.ContentLength ?? 0, null);
                if (resp.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden or HttpStatusCode.Gone)
                    return (resp.StatusCode, 0, null);
                lastError = "HTTP " + (int)resp.StatusCode;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastError = ex.GetType().Name + ": " + ex.Message;
            }
            if (attempt < MaxRetries) await Task.Delay(250 * (attempt + 1), ct).ConfigureAwait(false);
        }
        return (HttpStatusCode.InternalServerError, 0, lastError);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------
    private static IEnumerable<string> SafeEnumerate(string root)
    {
        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var dir = stack.Pop();
            string[] files;
            string[] dirs;
            try
            {
                files = Directory.GetFiles(dir);
                dirs = Directory.GetDirectories(dir);
            }
            catch
            {
                continue;
            }
            foreach (var f in files) yield return f;
            foreach (var d in dirs) stack.Push(d);
        }
    }

    private static bool IsInside(string path, string root)
    {
        var p = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var r = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.Equals(p, r, StringComparison.OrdinalIgnoreCase)) return true;
        return p.StartsWith(r + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsExcluded(string rel, List<string> patterns)
    {
        if (patterns.Count == 0) return false;
        var name = Path.GetFileName(rel);
        foreach (var pattern in patterns)
        {
            var p = pattern.Trim().Replace('\\', '/');
            if (p.Length == 0) continue;
            try
            {
                if (p.Contains('/'))
                {
                    if (FileSystemName.MatchesSimpleExpression(p, rel, true)) return true;
                }
                else if (FileSystemName.MatchesSimpleExpression(p, name, true))
                {
                    return true;
                }
            }
            catch
            {
                if (string.Equals(p, name, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p, rel, StringComparison.OrdinalIgnoreCase)) return true;
            }
        }
        return false;
    }

    private static string BuildUrl(string source, string rel)
    {
        var segs = rel.Split('/').Select(Uri.EscapeDataString);
        return source.TrimEnd('/') + "/" + string.Join('/', segs);
    }

    private static string Mb(long bytes) =>
        (bytes / 1048576.0).ToString("0.0", CultureInfo.InvariantCulture) + " MB";

    private static string EstimateEta(long totalBytes, long doneBytes, double mbps)
    {
        if (mbps <= 0 || totalBytes <= doneBytes) return "--";
        var remain = (totalBytes - doneBytes) / 1048576.0;
        var sec = (int)Math.Min(3599, remain / mbps);
        return sec >= 60 ? $"{sec / 60}m{sec % 60:00}s" : sec + "s";
    }

    private static bool IsTrue(string value) =>
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "1", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);

    private static int ParseInt(string value, int fallback, int min, int max)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)) return fallback;
        return Math.Clamp(n, min, max);
    }

    private static string Param(JobExecutionContext ctx, string key, string fallback) =>
        ctx.Parameters.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : fallback;
}
