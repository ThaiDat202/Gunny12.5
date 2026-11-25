using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using GunnyRequestStudio.Core;

namespace BombCli
{
    /// <summary>
    /// Harness dev cho BombService (net48 console).
    /// Lenh: check | scan | rip | mask | site | all
    ///   check : kiem tra dinh dang .bomb + roundtrip PNG &lt;-&gt; mask + so sanh crater.png voi mask that.
    ///   scan  : quet toan bo bomb id duoc request tham chieu, ghi reports\bomb-report.json.
    ///   rip   : mirror asset thieu tu CDN ve WebSite\image\bomb.
    ///   mask  : sinh .bomb tu crater.png va deploy vao cac folder Bomb cua server.
    ///   site  : doc SITE trong config.xml va kiem tra host do thieu asset nao.
    ///   all   : check -> scan -> rip -> mask -> scan lai (final state).
    /// </summary>
    internal static class Program
    {
        private static string _repoRoot;
        private static Logger _log;
        private static AppSettings _settings;
        private static BombService _svc;
        private static readonly CancellationToken Cancel = CancellationToken.None;
        private static readonly List<string> NoteLines = new List<string>();

        private static int Main(string[] args)
        {
            try
            {
                try { Console.OutputEncoding = Encoding.UTF8; } catch { }
                _repoRoot = FindRepoRoot();
                _log = new Logger(Path.Combine(_repoRoot, "_dev", "bombcli", "logs", "bombcli-" + DateTime.Now.ToString("yyyyMMdd") + ".log"));
                _log.OnLog += entry => Console.WriteLine(entry.ToString());
                _settings = BuildSettings();
                Directory.CreateDirectory(_settings.ReportsDir);
                BombService.ProbeCachePath = Path.Combine(_repoRoot, "_dev", "bombcli", "cache", "probe-cache.json");
                _svc = new BombService(_log);
                foreach (var dir in DefaultExtraCraterDirs())
                {
                    if (Directory.Exists(dir)) _svc.ExtraCraterDirs.Add(dir);
                }

                if (args == null || args.Length == 0)
                {
                    PrintHelp();
                    return 0;
                }
                string cmd = (args[0] ?? string.Empty).Trim().ToLowerInvariant();
                bool overwrite = args.Any(a => a.Equals("--overwrite", StringComparison.OrdinalIgnoreCase) || a.Equals("-o", StringComparison.OrdinalIgnoreCase));

                switch (cmd)
                {
                    case "check": return CmdCheck();
                    case "scan": return CmdScan(overwrite);
                    case "rip": return CmdRip(overwrite);
                    case "mask": return CmdMask(overwrite);
                    case "site": return CmdSite();
                    case "all": return CmdAll(overwrite);
                    case "help":
                    case "-h":
                    case "--help":
                        PrintHelp();
                        return 0;
                    default:
                        Console.Error.WriteLine("Lenh khong hop le: " + cmd);
                        PrintHelp();
                        return 2;
                }
            }
            catch (OperationCanceledException)
            {
                Console.Error.WriteLine("Da huy.");
                return 130;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("LOI: " + ex);
                return 1;
            }
        }

        // ============================================================ setup

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\'));
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "src", "Core")) &&
                    File.Exists(Path.Combine(dir.FullName, "build.ps1")))
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }
            // fallback: bin\Release\net48 => len 4
            var guess = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            for (int i = 0; i < 6 && guess != null; i++) guess = guess.Parent;
            return guess == null ? AppDomain.CurrentDomain.BaseDirectory : guess.FullName;
        }

        private static AppSettings BuildSettings()
        {
            var s = AppSettings.CreateDefault();
            s.RequestDir = @"C:\Gunny\Gunny12.6\WebSite\Request";
            s.ServiceRoot = @"C:\Gunny\Gunny12.6\Service\Service";
            s.FullSourceRoot = @"C:\Gunny\Gunny12.6\Full Source";
            s.WebSiteRoot = @"C:\Gunny\Gunny12.6\WebSite";
            s.BombAssetDir = @"C:\Gunny\Gunny12.6\WebSite\image\bomb";
            s.ClientConfigPath = @"C:\Gunny\Gunny12.6\WebSite\config.xml";
            s.ResBases = new List<string> { "https://res.gn.zing.vn/", "https://gunny.vcdn.vn/" };
            s.Concurrency = 8;
            s.ReportsDir = Path.Combine(_repoRoot, "reports");
            return s;
        }

        private static IEnumerable<string> DefaultExtraCraterDirs()
        {
            // Cac bo asset publisher da rip san tren may (chi la nguon du phong khi offline).
            yield return @"C:\Gunny\ToolRip\tool rip bomb\Crater";
            yield return @"C:\Gunny\ToolGunny\tool rip bomb\Crater";
        }

        private static string ReportPath()
        {
            return Path.Combine(_settings.ReportsDir, "bomb-report.json");
        }

        private static void PrintHelp()
        {
            Console.WriteLine("bombcli - harness dong bo bomb/crater asset cho GunnyRequestStudio");
            Console.WriteLine("  check | scan | rip | mask | site | all [--overwrite]");
            Console.WriteLine("  vi du: dotnet run -c Release -- check");
            Console.WriteLine("         dotnet run -c Release -- all");
        }

        // ============================================================ docs

        private static string FindRequestFile(string fileName)
        {
            var candidates = new List<string>
            {
                Path.Combine(_settings.RequestDir, "vng-quest", fileName),
                Path.Combine(_settings.RequestDir, fileName),
                Path.Combine(_settings.MirrorDir ?? string.Empty, fileName)
            };
            foreach (var c in candidates) if (File.Exists(c)) return c;
            try
            {
                if (Directory.Exists(_settings.RequestDir))
                {
                    var files = Directory.GetFiles(_settings.RequestDir, fileName, SearchOption.AllDirectories);
                    if (files.Length > 0) return files[0];
                }
            }
            catch { }
            return null;
        }

        private static List<XmlDocInfo> LoadRequestDocs()
        {
            var docs = new List<XmlDocInfo>();
            foreach (var name in new[] { "BallList.xml", "bombconfig.xml" })
            {
                string path = FindRequestFile(name);
                if (path == null) { _log.Warn("Khong tim thay request XML: " + name); continue; }
                var doc = XmlCodec.Inspect(path);
                if (!string.IsNullOrEmpty(doc.Error))
                {
                    _log.Warn("Doc " + path + " loi: " + doc.Error);
                    continue;
                }
                _log.Info("Doc " + Path.GetFileName(path) + ": " + doc.ItemCount + " item, " + doc.Rows.Count + " row");
                docs.Add(doc);
            }
            return docs;
        }

        private static XmlDocInfo FindBallDoc(List<XmlDocInfo> docs)
        {
            foreach (var d in docs)
            {
                string n = XmlCodec.NormalizeName(d.Name);
                if (n.Contains("balllist")) return d;
            }
            return docs.FirstOrDefault();
        }

        private static XmlDocInfo FindBombConfigDoc(List<XmlDocInfo> docs)
        {
            foreach (var d in docs)
            {
                if (XmlCodec.NormalizeName(d.Name).Contains("bombconfig")) return d;
            }
            return null;
        }

        // ============================================================ check

        private sealed class CheckResult
        {
            public string Note;
            public bool Passed;
            public int StructuralTotal;
            public int StructuralBad;
            public int RoundtripTotal;
            public int RoundtripEqual;
            public int CompareIds;
            public double AggregateMatch;
            public double MinMatch;
            public int CompareDimsMismatch;
        }

        private static int CmdCheck()
        {
            var sw = Stopwatch.StartNew();
            var result = RunCheck();
            _log.Okay("CHECK " + (result.Passed ? "PASS" : "FAIL") + " (" + sw.Elapsed.TotalSeconds.ToString("0.0") + "s)");

            var report = LoadReport();
            if (report == null)
            {
                report = NewEmptyReport();
                report.Note = result.Note;
                SaveReport(report);
            }
            Console.WriteLine(result.Note);
            Console.WriteLine("Report: " + ReportPath());
            return result.Passed ? 0 : 3;
        }

        private static CheckResult RunCheck()
        {
            var result = new CheckResult();
            var sb = new StringBuilder();
            string bombDir = BombService.ServerBombDir(_settings);
            var files = Directory.Exists(bombDir) ? Directory.GetFiles(bombDir, "*.bomb") : new string[0];
            int bad = 0, mismatchLen = 0;
            var maskIds = new List<int>();
            foreach (var f in files)
            {
                try
                {
                    var data = File.ReadAllBytes(f);
                    int w, h;
                    if (!BombService.TryReadBombHeader(data, out w, out h)) { bad++; _log.Warn("Header .bomb loi: " + f); continue; }
                    long expected = BombService.ExpectedBombLength(w, h);
                    if (data.Length < expected) { bad++; _log.Warn("File .bomb thieu du lieu: " + f + " (" + data.Length + " < " + expected + ")"); continue; }
                    if (data.Length != expected) mismatchLen++;
                    int id;
                    if (int.TryParse(Path.GetFileNameWithoutExtension(f), out id)) maskIds.Add(id);
                }
                catch (Exception ex)
                {
                    bad++;
                    _log.Warn("Doc .bomb loi " + f + ": " + ex.Message);
                }
            }
            result.StructuralTotal = files.Length;
            result.StructuralBad = bad;
            sb.AppendLine("[check] .bomb hop le: " + (files.Length - bad) + "/" + files.Length + " file (loi=" + bad +
                          "; file dung du dung cong thuc stride=" + ((files.Length - bad) - mismatchLen) + ", lech chieu dai=" + mismatchLen + ")");

            // round-trip .bomb -> PNG -> .bomb (kiem tra codec PNG khong mat du lieu)
            maskIds.Sort();
            int rtTotal = 0, rtEqual = 0;
            foreach (var id in maskIds.Take(120))
            {
                try
                {
                    var bomb = File.ReadAllBytes(Path.Combine(bombDir, id + ".bomb"));
                    var png = BombService.BombMaskToPng(bomb);
                    var back = BombService.PngToBombMask(png);
                    rtTotal++;
                    if (BytesEqual(bomb, back)) rtEqual++;
                    else _log.Warn("Roundtrip lech byte: id " + id);
                }
                catch (Exception ex) { _log.Warn("Roundtrip loi id " + id + ": " + ex.Message); }
            }
            result.RoundtripTotal = rtTotal;
            result.RoundtripEqual = rtEqual;
            sb.AppendLine("[check] roundtrip .bomb->PNG->.bomb: " + rtEqual + "/" + rtTotal + " byte-identical");

            // so sanh crater.png (publisher) -> mask voi file .bomb co san.
            // Luu y: server nap mask theo BALL ID; noi dung mask cua ball = crater.png cua crater
            // duoc tro boi thuoc tinh Crater cua ball do.
            var maskSet = new HashSet<int>(maskIds);
            var docs = LoadRequestDocs();
            var ballDoc = FindBallDoc(docs);
            _svc.BuildIdMaps(ballDoc);
            var candidates = new List<KeyValuePair<int, int>>(); // (mask id, source crater id)
            foreach (var kv in _svc.BallCraterMap.OrderBy(k => k.Key))
            {
                if (kv.Value <= 0) continue;
                if (!maskSet.Contains(kv.Key)) continue;
                candidates.Add(new KeyValuePair<int, int>(kv.Key, kv.Value));
                if (candidates.Count >= 80) break;
            }
            var ballIds = new HashSet<int>(_svc.BallCraterMap.Keys);
            foreach (var cid in _svc.CraterIds.OrderBy(x => x))
            {
                if (ballIds.Contains(cid) || !maskSet.Contains(cid)) continue;
                candidates.Add(new KeyValuePair<int, int>(cid, cid));
                if (candidates.Count >= 100) break;
            }
            long totalPix = 0, equalPix = 0;
            int compared = 0, dimsMismatch = 0;
            double minMatch = 100.0;
            foreach (var pair in candidates)
            {
                int id = pair.Key, src = pair.Value;
                try
                {
                    var png = _svc.LoadCraterSource(_settings, src);
                    if (png == null) continue;
                    string bombPath = Path.Combine(bombDir, id + ".bomb");
                    if (!File.Exists(bombPath)) continue;
                    var expectedMask = File.ReadAllBytes(bombPath);
                    int mw, mh;
                    if (!BombService.TryReadBombHeader(expectedMask, out mw, out mh)) continue;
                    var generated = BombService.PngToBombMask(png);
                    int gw, gh;
                    BombService.TryReadBombHeader(generated, out gw, out gh);
                    if (gw != mw || gh != mh)
                    {
                        // mask publisher cu dung canvas khac -> khong tinh vao do khop
                        dimsMismatch++;
                        continue;
                    }
                    long eq = CountEqualBits(expectedMask, generated, mw, mh, out long total);
                    totalPix += total;
                    equalPix += eq;
                    double pct = total == 0 ? 0 : eq * 100.0 / total;
                    if (pct < minMatch) minMatch = pct;
                    compared++;
                }
                catch (Exception ex) { _log.Warn("So sanh mask loi id " + id + ": " + ex.Message); }
            }
            result.CompareIds = compared;
            result.CompareDimsMismatch = dimsMismatch;
            result.AggregateMatch = totalPix == 0 ? 0 : equalPix * 100.0 / totalPix;
            result.MinMatch = compared == 0 ? 0 : minMatch;
            sb.AppendLine("[check] so sanh crater.png -> .bomb voi mask that: " + compared + " id trung kich thuoc" +
                          " (bo qua " + dimsMismatch + " id mask publisher cu dung canvas khac), " +
                          "khop pixel trung binh=" + result.AggregateMatch.ToString("0.00") + "%, thap nhat=" + result.MinMatch.ToString("0.00") + "%");

            result.Note = sb.ToString().TrimEnd();
            result.Passed = bad == 0 && (rtTotal == 0 || rtEqual == rtTotal) && compared > 0 && result.AggregateMatch > 98.0;
            if (compared == 0) _log.Warn("Khong co cap crater.png nao de so sanh (kiem tra mirror/ToolRip).");
            return result;
        }

        private static long CountEqualBits(byte[] a, byte[] b, int w, int h, out long total)
        {
            int stride = (w >> 3) + 1;
            long equal = 0;
            total = (long)w * h;
            for (int y = 0; y < h; y++)
            {
                int baseOffset = 8 + y * stride;
                for (int x = 0; x < w; x++)
                {
                    int idx = baseOffset + (x >> 3);
                    int bitA = (a[idx] >> (7 - (x & 7))) & 1;
                    int bitB = (b[idx] >> (7 - (x & 7))) & 1;
                    if (bitA == bitB) equal++;
                }
            }
            return equal;
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a == null || b == null) return a == b;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private static SortedSet<int> CollectCraterIds(XmlDocInfo ballDoc)
        {
            var set = new SortedSet<int>();
            if (ballDoc == null || ballDoc.Rows == null) return set;
            foreach (var row in ballDoc.Rows)
            {
                string v;
                if (row != null && row.TryGetValue("Crater", out v))
                {
                    int id;
                    if (int.TryParse((v ?? string.Empty).Trim(), out id) && id >= 0) set.Add(id);
                }
            }
            return set;
        }

        private static SortedSet<int> CollectBallIds(XmlDocInfo ballDoc)
        {
            var set = new SortedSet<int>();
            if (ballDoc == null || ballDoc.Rows == null) return set;
            foreach (var row in ballDoc.Rows)
            {
                string v;
                if (row != null && row.TryGetValue("ID", out v))
                {
                    int id;
                    if (int.TryParse((v ?? string.Empty).Trim(), out id) && id >= 0) set.Add(id);
                }
            }
            return set;
        }

        private static SortedSet<int> CollectBombParticalIds(XmlDocInfo ballDoc)
        {
            var set = new SortedSet<int>();
            if (ballDoc == null || ballDoc.Rows == null) return set;
            foreach (var row in ballDoc.Rows)
            {
                string v;
                if (row != null && row.TryGetValue("BombPartical", out v))
                {
                    int id;
                    if (int.TryParse((v ?? string.Empty).Trim(), out id) && id > 0) set.Add(id);
                }
            }
            return set;
        }

        // ============================================================ scan

        private static int CmdScan(bool overwrite)
        {
            var sw = Stopwatch.StartNew();
            var docs = LoadRequestDocs();
            var infos = _svc.Scan(_settings, docs, info =>
            {
                if (info.Id % 200 == 0) Console.Write(".");
            }, Cancel);
            Console.WriteLine();
            var report = BuildScanReport(infos, null, null);
            SaveReport(report);
            PrintScanSummary(report, "scan");
            Console.WriteLine("Scan xong sau " + sw.Elapsed.TotalSeconds.ToString("0.0") + "s. Report: " + ReportPath());
            return 0;
        }

        private static BombReport BuildScanReport(List<BombIdInfo> infos, BombRipStats rip, BombMaskStats mask)
        {
            var report = NewEmptyReport();
            report.ReferencedIds = infos.Count;
            report.WithMask = infos.Count(x => x.ServerMask);
            report.MissingMask = infos.Count(x => !x.ServerMask && _svc.IsMaskNeeded(x.Id));
            report.Ids = infos;
            report.UnresolvedIds = infos.Where(x => !x.ServerMask && _svc.IsMaskNeeded(x.Id) &&
                                                   string.Equals(x.Resolution, "no-source", StringComparison.OrdinalIgnoreCase))
                                        .Select(x => x.Id).ToList();
            report.AssetsUnavailable = infos.Sum(x => CountRemoteMissing(x));
            report.GeneratedMasks = mask == null ? 0 : mask.GeneratedIds;
            report.Deployed = mask == null ? 0 : mask.Deployed;
            report.AssetsDownloaded = rip == null ? 0 : rip.Downloaded;
            report.Note = null;
            return report;
        }

        private static int CountRemoteMissing(BombIdInfo info)
        {
            int n = 0;
            if (!info.CraterPng && !info.LocalCrater) n++;
            if (!info.CraterBrink && !info.LocalBrink) n++;
            if (!info.Bullet && !info.LocalBullet) n++;
            if (!info.BlastOut && !info.LocalBlastOut) n++;
            return n;
        }

        private static void PrintScanSummary(BombReport report, string label)
        {
            _log.Step("[" + label + "] tham chieu=" + report.ReferencedIds + ", co mask=" + report.WithMask +
                      ", thieu mask=" + report.MissingMask + ", no-source=" + report.UnresolvedIds.Count +
                      ", asset thieu (chua co local/CDN)=" + report.AssetsUnavailable);
            if (report.MissingMask > 0)
            {
                var miss = report.Ids.Where(x => !x.ServerMask).Select(x => x.Id).Take(30).ToList();
                _log.Info("  id thieu mask (30 dau): " + string.Join(", ", miss.Select(x => x.ToString()).ToArray()));
            }
        }

        // ============================================================ rip

        private static int CmdRip(bool overwrite)
        {
            var sw = Stopwatch.StartNew();
            var report = LoadReport();
            if (report == null || report.Ids == null || report.Ids.Count == 0)
            {
                Console.Error.WriteLine("Chua co " + ReportPath() + " - hay chay 'scan' truoc.");
                return 2;
            }
            _svc.RipAssets(_settings, report.Ids, overwrite, null, Cancel);
            var stats = _svc.LastRipStats;
            RefreshLocalFlags(report.Ids);
            report.AssetsDownloaded = (report.AssetsDownloaded == 0 ? 0 : report.AssetsDownloaded) + stats.Downloaded;
            report.AssetsUnavailable = report.Ids.Sum(x => CountRemoteMissing(x));
            report.Note = JoinNotes(RipNote(stats), report.Note);
            SaveReport(report);
            Console.WriteLine(RipNote(stats));
            Console.WriteLine("Rip xong sau " + sw.Elapsed.TotalSeconds.ToString("0.0") + "s. Report: " + ReportPath());
            return 0;
        }

        private static string RipNote(BombRipStats stats)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[rip] tai=" + stats.Downloaded + ", bo qua=" + stats.Skipped +
                          ", khong co tren CDN=" + stats.Unavailable + ", loi=" + stats.Failed);
            foreach (var type in new[] { BombService.TypeCrater, BombService.TypeBrink, BombService.TypeBullet, BombService.TypeBlastOut })
            {
                int d, sk, un, fa;
                stats.TypeCounts(type, out d, out sk, out un, out fa);
                sb.AppendLine("  " + type + ": tai=" + d + " bo qua=" + sk + " 404=" + un + " loi=" + fa);
            }
            return sb.ToString().TrimEnd();
        }

        private static void RefreshLocalFlags(List<BombIdInfo> infos)
        {
            if (infos == null) return;
            foreach (var info in infos)
            {
                if (info == null) continue;
                info.LocalCrater = File.Exists(BombService.CraterLocalPath(_settings, info.Id));
                info.LocalBrink = File.Exists(BombService.CraterBrinkLocalPath(_settings, info.Id));
                info.LocalBullet = File.Exists(BombService.BulletLocalPath(_settings, info.Id));
                info.LocalBlastOut = File.Exists(BombService.BlastOutLocalPath(_settings, info.Id));
            }
        }

        // ============================================================ mask

        private static int CmdMask(bool overwrite)
        {
            var sw = Stopwatch.StartNew();
            var report = LoadReport();
            if (report == null || report.Ids == null || report.Ids.Count == 0)
            {
                Console.Error.WriteLine("Chua co " + ReportPath() + " - hay chay 'scan' truoc.");
                return 2;
            }
            RefreshLocalFlags(report.Ids);
            _svc.GenerateAndDeployMasks(_settings, report.Ids, overwrite, null, Cancel);
            var stats = _svc.LastMaskStats;
            report.GeneratedMasks = stats.GeneratedIds;
            report.Deployed = stats.Deployed;
            report.WithMask = report.Ids.Count(x => x.ServerMask);
            report.MissingMask = report.Ids.Count(x => !x.ServerMask && _svc.IsMaskNeeded(x.Id));
            report.UnresolvedIds = report.Ids.Where(x => !x.ServerMask && _svc.IsMaskNeeded(x.Id) &&
                                                         string.Equals(x.Resolution, "no-source", StringComparison.OrdinalIgnoreCase))
                                             .Select(x => x.Id).ToList();
            report.Note = JoinNotes(MaskNote(stats), report.Note);
            SaveReport(report);
            Console.WriteLine(MaskNote(stats));
            Console.WriteLine("Mask xong sau " + sw.Elapsed.TotalSeconds.ToString("0.0") + "s. Report: " + ReportPath());
            return 0;
        }

        private static string MaskNote(BombMaskStats stats)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[mask] tao=" + stats.GeneratedIds + " id (moi=" + stats.NewIds + ", bo sung folder khac=" + stats.FilledIds +
                          "), ghi=" + stats.Deployed + " file, bo qua=" + stats.SkippedIds +
                          ", khong co nguon=" + stats.NoSource + ", loi=" + stats.Failed);
            foreach (var kv in stats.DeployedByFolder.OrderBy(k => k.Key))
            {
                sb.AppendLine("  " + kv.Key + " = " + kv.Value + " file");
            }
            return sb.ToString().TrimEnd();
        }

        // ============================================================ site

        private static int CmdSite()
        {
            var report = LoadReport();
            if (report == null) report = NewEmptyReport();
            var docs = LoadRequestDocs();
            var ballDoc = FindBallDoc(docs);
            _svc.BuildIdMaps(ballDoc);
            List<int> newIds = null;
            try
            {
                string cachePath = Path.Combine(_repoRoot, "_dev", "bombcli", "cache", "last-new-ids.json");
                if (File.Exists(cachePath)) newIds = Json.LoadFile<List<int>>(cachePath);
            }
            catch (Exception ex) { _log.Warn("Khong doc duoc last-new-ids.json: " + ex.Message); }
            if (newIds == null || newIds.Count == 0)
            {
                var infos = _svc.Scan(_settings, docs, null, Cancel);
                newIds = infos.Where(x => !x.ServerMask && _svc.IsMaskNeeded(x.Id)).Select(x => x.Id).ToList();
                if (report.Ids == null || report.Ids.Count == 0) report.Ids = infos;
            }
            _log.Info("Client site check cho " + newIds.Count + " id moi");
            string advice = _svc.ClientSiteCheck(_settings, newIds);
            report.Note = ReplaceSection(report.Note, "[client-site]", advice);
            SaveReport(report);
            Console.WriteLine(advice);
            Console.WriteLine("Report: " + ReportPath());
            return 0;
        }

        private static string ReplaceSection(string note, string marker, string content)
        {
            if (string.IsNullOrEmpty(note)) return marker + Environment.NewLine + content;
            int idx = note.IndexOf(marker, StringComparison.Ordinal);
            if (idx >= 0) note = note.Substring(0, idx).TrimEnd();
            return JoinNotes(note, marker + Environment.NewLine + content);
        }

        // ============================================================ all

        private static int CmdAll(bool overwrite)
        {
            var total = Stopwatch.StartNew();
            var report = NewEmptyReport();

            // 1) check
            _log.Step("=== BUOC 1/5: CHECK dinh dang + roundtrip ===");
            var check = RunCheck();
            _log.Okay("Check: " + (check.Passed ? "PASS" : "FAIL") + " | " + check.Note.Replace("\r", "").Replace("\n", " | "));

            // 2) scan
            _log.Step("=== BUOC 2/5: SCAN ===");
            var docs = LoadRequestDocs();
            var infos = _svc.Scan(_settings, docs, null, Cancel);
            var ballDoc = FindBallDoc(docs);
            report = BuildScanReport(infos, null, null);
            report.Note = check.Note;
            SaveReport(report);
            PrintScanSummary(report, "scan");

            // 3) rip asset
            _log.Step("=== BUOC 3/5: RIP ASSET ===");
            _svc.RipAssets(_settings, infos, overwrite, null, Cancel);
            var ripMain = _svc.LastRipStats;
            RefreshLocalFlags(infos);
            _log.Okay("Rip (id tham chieu): tai=" + ripMain.Downloaded + ", bo qua=" + ripMain.Skipped +
                      ", 404=" + ripMain.Unavailable + ", loi=" + ripMain.Failed);

            // danh sach "id moi" = id thieu mask (truoc khi deploy) - dung cho ClientSiteCheck
            var newIds = infos.Where(x => !x.ServerMask && _svc.IsMaskNeeded(x.Id)).Select(x => x.Id).ToList();

            // 3b) bo sung asset that su client dung:
            //     bullet<BombPartical>.swf  va blastOut<ballId>.swf  cho cac id ngoai tap tham chieu
            var extraIds = new SortedSet<int>();
            foreach (var id in CollectBombParticalIds(ballDoc)) extraIds.Add(id);
            foreach (var id in CollectBallIds(ballDoc)) extraIds.Add(id);
            foreach (var info in infos) extraIds.Remove(info.Id);
            var extraInfos = extraIds.Select(id => new BombIdInfo { Id = id }).ToList();
            BombRipStats ripExtra = null;
            if (extraInfos.Count > 0)
            {
                _log.Step("Rip bo sung: bullet/blastOut cho " + extraInfos.Count + " id (BombPartical + ball id ngoai tap tham chieu)");
                _svc.RipAssets(_settings, extraInfos, overwrite, null, Cancel);
                ripExtra = _svc.LastRipStats;
                _log.Okay("Rip bo sung: tai=" + ripExtra.Downloaded + ", 404=" + ripExtra.Unavailable + ", loi=" + ripExtra.Failed);
            }
            int maskOkBefore = infos.Count(x => x.ServerMask);
            int maskMissingBefore = infos.Count(x => !x.ServerMask && _svc.IsMaskNeeded(x.Id));
            int maskNotNeeded = infos.Count(x => !_svc.IsMaskNeeded(x.Id));

            // 4) mask
            _log.Step("=== BUOC 4/5: GENERATE + DEPLOY MASK ===");
            _svc.GenerateAndDeployMasks(_settings, infos, overwrite, null, Cancel);
            var maskStats = _svc.LastMaskStats;
            _log.Okay("Mask: tao=" + maskStats.GeneratedIds + " id (moi=" + maskStats.NewIds + ", bo sung folder khac=" +
                      maskStats.FilledIds + "), ghi=" + maskStats.Deployed + " file, khong co nguon=" + maskStats.NoSource);
            try
            {
                Json.SaveFile(Path.Combine(_repoRoot, "_dev", "bombcli", "cache", "last-new-ids.json"), newIds);
            }
            catch (Exception ex) { _log.Warn("Khong luu duoc last-new-ids.json: " + ex.Message); }

            // 5) scan lai (final)
            _log.Step("=== BUOC 5/5: SCAN LAI (FINAL) ===");
            var finalInfos = _svc.Scan(_settings, docs, null, Cancel);
            report = BuildScanReport(finalInfos, ripMain, maskStats);
            report.WithMask = finalInfos.Count(x => x.ServerMask);
            report.MissingMask = finalInfos.Count(x => !x.ServerMask && _svc.IsMaskNeeded(x.Id));
            report.UnresolvedIds = finalInfos.Where(x => !x.ServerMask && _svc.IsMaskNeeded(x.Id) &&
                                                         string.Equals(x.Resolution, "no-source", StringComparison.OrdinalIgnoreCase))
                                             .Select(x => x.Id).ToList();

            // note tong hop
            var note = new StringBuilder();
            note.AppendLine(check.Note).AppendLine();
            int notNeededMissing = infos.Count(x => !x.ServerMask && !_svc.IsMaskNeeded(x.Id));
            note.AppendLine("[scan] tham chieu=" + finalInfos.Count + ", co mask truoc khi deploy=" + maskOkBefore +
                            ", thieu mask truoc=" + maskMissingBefore +
                            " (ball khong co Crater => khong can mask: " + maskNotNeeded + ", trong do dang thieu .bomb: " + notNeededMissing + ")");
            note.AppendLine(RipNote(ripMain));
            if (ripExtra != null)
            {
                note.AppendLine("[rip-bo-sung] tai=" + ripExtra.Downloaded + ", 404=" + ripExtra.Unavailable +
                                ", loi=" + ripExtra.Failed + " (bullet<BombPartical> + blastOut<ballId>)");
                foreach (var type in new[] { BombService.TypeBullet, BombService.TypeBlastOut, BombService.TypeCrater, BombService.TypeBrink })
                {
                    int d, sk, un, fa;
                    ripExtra.TypeCounts(type, out d, out sk, out un, out fa);
                    note.AppendLine("  " + type + ": tai=" + d + " 404=" + un + " loi=" + fa);
                }
            }
            note.AppendLine(MaskNote(maskStats));
            note.AppendLine("[final] co mask=" + report.WithMask + "/" + report.ReferencedIds +
                            ", thieu mask=" + report.MissingMask + ", no-source=" + report.UnresolvedIds.Count);
            note.AppendLine("[id-moi] " + newIds.Count + " id thieu mask truoc khi deploy (dung cho client-site): " +
                            string.Join(", ", newIds.Take(30).Select(x => x.ToString()).ToArray()) +
                            (newIds.Count > 30 ? ", ..." : ""));
            if (report.UnresolvedIds.Count > 0)
            {
                note.AppendLine("  unresolved (no-source, 30 dau): " +
                                string.Join(", ", report.UnresolvedIds.Take(30).Select(x => x.ToString()).ToArray()) +
                                (report.UnresolvedIds.Count > 30 ? ", ..." : ""));
            }
            // clientsite check
            _log.Step("=== CLIENT SITE CHECK ===");
            string site = _svc.ClientSiteCheck(_settings, newIds);
            note.AppendLine("[client-site]").AppendLine(site);
            report.Note = note.ToString().TrimEnd();
            SaveReport(report);

            Console.WriteLine();
            Console.WriteLine("================ TONG KET ================");
            Console.WriteLine("Check: " + (check.Passed ? "PASS" : "FAIL") + " | " + check.Note.Replace("\r", "").Replace("\n", " | "));
            Console.WriteLine("Referenced ids     : " + report.ReferencedIds);
            Console.WriteLine("Masks before deploy: ok=" + maskOkBefore + ", missing=" + maskMissingBefore + ", not-needed=" + maskNotNeeded);
            Console.WriteLine("Masks generated    : " + maskStats.GeneratedIds + " id (moi=" + maskStats.NewIds +
                              ", bo sung folder khac=" + maskStats.FilledIds + ") -> " + maskStats.Deployed + " file");
            Console.WriteLine("Assets downloaded  : " + (ripMain.Downloaded + (ripExtra == null ? 0 : ripExtra.Downloaded)));
            Console.WriteLine("Assets unavailable : " + (ripMain.Unavailable + (ripExtra == null ? 0 : ripExtra.Unavailable)));
            Console.WriteLine("Final masks        : ok=" + report.WithMask + ", missing=" + report.MissingMask + ", no-source=" + report.UnresolvedIds.Count);
            Console.WriteLine("Client site        : " + site.Replace("\r", "").Replace("\n", " | "));
            Console.WriteLine("Report             : " + ReportPath());
            Console.WriteLine("Thoi gian          : " + total.Elapsed.TotalSeconds.ToString("0.0") + "s");
            return check.Passed ? 0 : 3;
        }

        // ============================================================ report helpers

        private static BombReport NewEmptyReport()
        {
            return new BombReport
            {
                GeneratedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Ids = new List<BombIdInfo>(),
                UnresolvedIds = new List<int>()
            };
        }

        private static BombReport LoadReport()
        {
            try
            {
                string path = ReportPath();
                if (!File.Exists(path)) return null;
                return Json.LoadFile<BombReport>(path);
            }
            catch (Exception ex)
            {
                _log.Warn("Khong doc duoc report: " + ex.Message);
                return null;
            }
        }

        private static void SaveReport(BombReport report)
        {
            report.GeneratedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            Json.SaveFile(ReportPath(), report);
        }

        private static string JoinNotes(string first, string second)
        {
            if (string.IsNullOrEmpty(first)) return second;
            if (string.IsNullOrEmpty(second)) return first;
            return first.TrimEnd() + Environment.NewLine + Environment.NewLine + second.Trim();
        }
    }
}
