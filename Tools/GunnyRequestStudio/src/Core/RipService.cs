using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace GunnyRequestStudio.Core
{
    public class RipOptions
    {
        public bool Overwrite = true;
        public bool PromoteTemplates = true;
        public bool PromoteAll = false;
        public bool DryRun = false;
        public int Concurrency = 8;
        public bool RefreshFromPublisher = true;
    }

    /// <summary>
    /// Rip (mirror) toan bo request XML tu server nha phat hanh (quest.gn.zing.vn)
    /// vao thu muc Request\vng-quest, sau do "promote" cac file template (co bang CSDL)
    /// vao thu muc Request goc de client su dung.
    /// </summary>
    public class RipService
    {
        private readonly Logger _log;
        private readonly TableMapper _mapper;

        public RipService(Logger log, TableMapper mapper = null)
        {
            _log = log ?? new Logger();
            _mapper = mapper;
        }

        // ------------------------------------------------------------ names

        /// <summary>Danh sach ten request: tu Request goc + mirror + names.txt (neu co).</summary>
        public List<string> DiscoverNames(AppSettings s)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var roots = new[] { s.RequestDir, s.MirrorDir };
            foreach (var root in roots)
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;
                foreach (var f in Directory.GetFiles(root, "*.xml", SearchOption.TopDirectoryOnly))
                {
                    var name = Path.GetFileName(f);
                    if (name.StartsWith("_")) continue;
                    var key = XmlCodec.StripXmlExtension(name);
                    if (!dict.ContainsKey(key)) dict[key] = key;
                }
            }
            // file names.txt (neu da luu lan truoc)
            try
            {
                var namesFile = Path.Combine(s.MirrorDir ?? string.Empty, "names.txt");
                if (File.Exists(namesFile))
                {
                    foreach (var line in File.ReadAllLines(namesFile))
                    {
                        var n = line.Trim();
                        if (n.Length > 0 && !dict.ContainsKey(n)) dict[n] = n;
                    }
                }
            }
            catch { }
            return dict.Values.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public void SaveNames(AppSettings s, IEnumerable<string> names)
        {
            try
            {
                Paths.EnsureDir(s.MirrorDir);
                File.WriteAllLines(Path.Combine(s.MirrorDir, "names.txt"), names.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(), Encoding.UTF8);
            }
            catch (Exception ex) { _log.Warn("Khong ghi duoc names.txt: " + ex.Message); }
        }

        /// <summary>Kiem tra nhanh host phat hanh nao con song.</summary>
        public List<string> WorkingBases(AppSettings s)
        {
            var ok = new List<string>();
            foreach (var b in s.PublisherBases ?? new List<string>())
            {
                if (string.IsNullOrWhiteSpace(b)) continue;
                var url = HttpUtil.Combine(b, "BallList.xml");
                var r = HttpUtil.Head(url, 12000, 1);
                if (r.Status == 200 || r.Status == 0)
                {
                    // status 0 = timeout -> van thu base khac; chi nhan 200
                }
                if (r.Status == 200) ok.Add(b.EndsWith("/") ? b : b + "/");
            }
            if (ok.Count == 0)
            {
                // fallback: giu tat ca base de van thu GET (co the HEAD bi chan)
                foreach (var b in s.PublisherBases ?? new List<string>())
                    if (!string.IsNullOrWhiteSpace(b)) ok.Add(b.EndsWith("/") ? b : b + "/");
            }
            return ok;
        }

        // ------------------------------------------------------------ rip

        public RipReport Rip(AppSettings s, IEnumerable<string> names, RipOptions opt, Action<RipEntry> progress, CancellationToken ct)
        {
            var report = new RipReport
            {
                GeneratedAt = DateTime.Now.ToString("s"),
                Sources = new List<string>(s.PublisherBases ?? new List<string>()),
                TargetDir = s.MirrorDir
            };
            Paths.EnsureDir(s.MirrorDir);
            var entries = new ConcurrentBag<RipEntry>();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var nameList = (names ?? Enumerable.Empty<string>()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var bases = opt.RefreshFromPublisher ? WorkingBases(s) : new List<string>();
            if (bases.Count == 0) bases = new List<string> { "" };   // chi doc mirror local
            report.Sources = bases.Where(b => !string.IsNullOrEmpty(b)).ToList();
            if (report.Sources.Count == 0) report.Sources = new List<string>(s.PublisherBases ?? new List<string>());

            _log.Step("Rip " + nameList.Count + " request tu " + string.Join(", ", report.Sources.ToArray()));

            var sem = new SemaphoreSlim(Math.Max(1, Math.Min(16, opt.Concurrency)));
            var tasks = new List<Task>();
            foreach (var name in nameList)
            {
                ct.ThrowIfCancellationRequested();
                sem.Wait(ct);
                var n = name;
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        var entry = RipOne(s, bases, n, opt);
                        entries.Add(entry);
                        if (progress != null) { try { progress(entry); } catch { } }
                    }
                    finally { sem.Release(); }
                }, ct));
            }
            try { Task.WaitAll(tasks.ToArray(), ct); } catch (OperationCanceledException) { _log.Warn("Da huy rip theo yeu cau."); }

            sw.Stop();
            report.Entries = entries.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
            report.Stats = new RipStats
            {
                Total = report.Entries.Count,
                Ok = report.Entries.Count(e => e.Status == "ok"),
                Unchanged = report.Entries.Count(e => e.Status == "unchanged"),
                Missing = report.Entries.Count(e => e.Status == "missing"),
                Failed = report.Entries.Count(e => e.Status == "failed"),
                Bytes = report.Entries.Where(e => e.Status == "ok").Sum(e => e.Size),
                Seconds = Math.Round(sw.Elapsed.TotalSeconds, 1)
            };
            _log.Okay(string.Format("Rip xong: OK={0} unchanged={1} missing={2} failed={3} ({4})",
                report.Stats.Ok, report.Stats.Unchanged, report.Stats.Missing, report.Stats.Failed, Paths.FormatSize(report.Stats.Bytes)));
            return report;
        }

        private RipEntry RipOne(AppSettings s, List<string> bases, string name, RipOptions opt)
        {
            var entry = new RipEntry { Name = name };
            var fileName = name + ".xml";
            var localPath = Path.Combine(s.MirrorDir, fileName);
            entry.LocalPath = localPath;
            var existing = File.Exists(localPath) ? File.ReadAllBytes(localPath) : null;

            foreach (var b in bases)
            {
                if (string.IsNullOrEmpty(b))
                {
                    // offline: chi doc mirror local
                    break;
                }
                var url = HttpUtil.Combine(b, fileName);
                var r = HttpUtil.Get(url, 20000, 2);
                if (r.IsOk)
                {
                    entry.Url = url;
                    entry.Size = r.Length;
                    entry.Sha256 = Hash.Sha256Bytes(r.Bytes);
                    if (existing != null && existing.Length == r.Bytes.Length)
                    {
                        var same = Hash.Sha256Bytes(existing) == entry.Sha256;
                        if (same) { entry.Status = "unchanged"; return entry; }
                    }
                    if (!opt.DryRun) Paths.WriteAtomic(localPath, r.Bytes);
                    entry.Status = "ok";
                    return entry;
                }
                if (r.IsMissing)
                {
                    entry.Url = url;
                    entry.Status = "missing";
                    entry.Error = r.Error;
                    return entry;
                }
                entry.Error = r.Error;
            }

            // khong tai duoc: van giu ban mirror local neu co
            if (existing != null)
            {
                entry.Status = "unchanged";
                entry.Size = existing.Length;
                entry.Sha256 = Hash.Sha256Bytes(existing);
                entry.Error = "publisher khong phan hoi - dung ban mirror local";
            }
            else
            {
                entry.Status = entry.Error != null ? "failed" : "missing";
            }
            return entry;
        }

        // ------------------------------------------------------------ promote

        /// <summary>
        /// Copy cac file template (map duoc bang CSDL) tu mirror vao thu muc Request goc.
        /// File cu duoc backup thanh *.bak-yyyyMMdd-HHmmss.
        /// </summary>
        public RipReport Promote(AppSettings s, RipReport report, bool overwrite, bool promoteAll, Action<RipEntry> progress, CancellationToken ct)
        {
            if (_mapper == null) throw new InvalidOperationException("Thieu TableMapper de phan loai template.");
            int promoted = 0, unchanged = 0;
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            foreach (var e in report.Entries)
            {
                ct.ThrowIfCancellationRequested();
                if (e.Status != "ok" && e.Status != "unchanged") continue;
                var src = Path.Combine(s.MirrorDir, e.Name + ".xml");
                if (!File.Exists(src)) continue;
                XmlDocInfo doc;
                try { doc = XmlCodec.Inspect(src); } catch { continue; }
                doc.Name = e.Name;
                var map = _mapper.Resolve(doc);
                e.TableName = map.TableName;
                e.MapStatus = map.Status;
                bool isTemplate = DbImporter.IsImportable(map);
                if (!isTemplate && !promoteAll) continue;

                var dst = Path.Combine(s.RequestDir, e.Name + ".xml");
                try
                {
                    var newBytes = File.ReadAllBytes(src);
                    if (File.Exists(dst))
                    {
                        var oldBytes = File.ReadAllBytes(dst);
                        if (oldBytes.Length == newBytes.Length && Hash.Sha256Bytes(oldBytes) == Hash.Sha256Bytes(newBytes))
                        {
                            unchanged++;
                            continue;
                        }
                        if (!overwrite) continue;
                        var bak = dst + ".bak-" + stamp;
                        File.Copy(dst, bak, true);
                    }
                    Paths.WriteAtomic(dst, newBytes);
                    promoted++;
                }
                catch (Exception ex)
                {
                    e.Error = "promote: " + ex.Message;
                    _log.Warn("Promote loi " + e.Name + ": " + ex.Message);
                }
            }
            report.Stats.Promoted = promoted;
            report.Stats.PromotedUnchanged = unchanged;
            report.Note = string.Format("Promote vao Request: {0} file moi/cap nhat, {1} file giong het (giu nguyen).", promoted, unchanged);
            _log.Okay(report.Note);
            return report;
        }

        public void SaveReport(RipReport report, string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            Json.SaveFile(path, report);
            // manifest: chi file OK
            var manifest = new RipManifest
            {
                GeneratedAt = report.GeneratedAt,
                TargetDir = report.TargetDir,
                Note = "Manifest RIP request tu nha phat hanh",
                Files = report.Entries.Where(e => e.Status == "ok" || e.Status == "unchanged")
                    .Select(e => new RipManifestFile { Name = e.Name, Table = e.TableName, MapStatus = e.MapStatus, Size = e.Size, Sha256 = e.Sha256 })
                    .ToList()
            };
            var mp = Path.ChangeExtension(path, null) + "-manifest.json";
            Json.SaveFile(mp, manifest);
        }

        [System.Runtime.Serialization.DataContract]
        public class RipManifest
        {
            [System.Runtime.Serialization.DataMember(Order = 1)] public string GeneratedAt;
            [System.Runtime.Serialization.DataMember(Order = 2)] public string TargetDir;
            [System.Runtime.Serialization.DataMember(Order = 3)] public string Note;
            [System.Runtime.Serialization.DataMember(Order = 4)] public List<RipManifestFile> Files = new List<RipManifestFile>();
        }

        [System.Runtime.Serialization.DataContract]
        public class RipManifestFile
        {
            [System.Runtime.Serialization.DataMember(Order = 1)] public string Name;
            [System.Runtime.Serialization.DataMember(Order = 2)] public string Table;
            [System.Runtime.Serialization.DataMember(Order = 3)] public string MapStatus;
            [System.Runtime.Serialization.DataMember(Order = 4)] public long Size;
            [System.Runtime.Serialization.DataMember(Order = 5)] public string Sha256;
        }
    }
}

