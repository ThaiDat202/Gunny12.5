using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace GunnyRequestStudio.Core
{
    // ============================================================ DTO phu tro

    [DataContract]
    public class ProbeRecord
    {
        [DataMember(Order = 1)] public string Path;     // duong dan tuong doi, vd image/bomb/crater/101/crater.png
        [DataMember(Order = 2)] public int Status;      // HTTP status (200 = co, 404 = khong co); 0 = loi mang (khong cache)
        [DataMember(Order = 3)] public string Base;     // base URL da tra ve status
    }

    [DataContract]
    public class ProbeCacheFile
    {
        [DataMember(Order = 1)] public string GeneratedAt;
        [DataMember(Order = 2)] public List<ProbeRecord> Entries = new List<ProbeRecord>();
    }

    /// <summary>Thong ke cho RipAssets.</summary>
    public class BombRipStats
    {
        public int Downloaded;
        public int Skipped;
        public int Unavailable;
        public int Failed;
        public readonly Dictionary<string, int> DownloadedByType = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, int> SkippedByType = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, int> UnavailableByType = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, int> FailedByType = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Thong ke cho GenerateAndDeployMasks.</summary>
    public class BombMaskStats
    {
        public int GeneratedIds;  // tong so id da ghi mask (moi id tinh 1)
        public int NewIds;        // id thieu mask o folder Fight (bomb/crater moi)
        public int FilledIds;     // id da co mask o Fight nhung thieu o folder khac (vd ban debug)
        public int Deployed;      // so file .bomb da ghi (moi folder tinh 1)
        public int SkippedIds;
        public int NoSource;
        public int Failed;
        public readonly Dictionary<string, int> DeployedByFolder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    }

    // ============================================================ Service

    /// <summary>
    /// Dong bo "bomb" cho GunnyRequestStudio:
    ///  - Quet bomb id duoc tham chieu tu request XML (BallList / bombconfig).
    ///  - Kiem tra mask .bomb tren server, asset client da mirror, asset con tren CDN publisher.
    ///  - Mirror asset tu CDN ve WebSite\image\bomb (crater png, bullet swf, blastOut swf...).
    ///  - Sinh mask .bomb tu crater.png (PNG -> 1bpp) cho id thieu mask va deploy vao cac folder Bomb.
    ///
    /// Dinh dang .bomb: int32 LE width, int32 LE height, anh 1bpp, stride = (width>>3)+1,
    /// bit cao (MSB) la pixel trai nhat, bit = 1 => dat bi pha.
    /// </summary>
    public class BombService
    {
        // -------- cau hinh tinh cho harness/UI (co the set truoc khi goi)
        /// <summary>Duong dan file cache probe (json). null => &lt;BaseDir&gt;\cache\probe-cache.json</summary>
        public static string ProbeCachePath = null;
        /// <summary>Bat/tat cache probe remote.</summary>
        public static bool UseProbeCache = true;

        // -------- hang so
        public const string TypeCrater = "crater";
        public const string TypeBrink = "craterBrink";
        public const string TypeBullet = "bullet";
        public const string TypeBlastOut = "blastOut";

        private static readonly string[] AssetTypes = new[] { TypeCrater, TypeBrink, TypeBullet, TypeBlastOut };
        private static readonly string[] BombConfigKeys = new[] { "Common", "CommonAddWound", "CommonMultiBall", "Special" };
        private const int ProbeTimeoutMs = 12000;
        private const int DownloadTimeoutMs = 25000;

        // -------- trang thai
        private readonly Logger _log;
        private readonly object _cacheLock = new object();
        private readonly object _callbackLock = new object();
        private readonly object _noteLock = new object();
        private Dictionary<string, ProbeRecord> _cache;
        private bool _cacheDirty;
        private List<BombIdInfo> _lastScan = new List<BombIdInfo>();
        private BombRipStats _lastRipStats = new BombRipStats();
        private BombMaskStats _lastMaskStats = new BombMaskStats();
        private Dictionary<int, int> _ballCrater = new Dictionary<int, int>();
        private Dictionary<int, int> _ballBombPartical = new Dictionary<int, int>();
        private HashSet<int> _craterIds = new HashSet<int>();

        public BombService(Logger log)
        {
            _log = log ?? new Logger();
            ExtraCraterDirs = new List<string>();
            _cache = new Dictionary<string, ProbeRecord>(StringComparer.OrdinalIgnoreCase);
            LoadProbeCache();
        }

        /// <summary>Cac thu muc local bo sung co chua crater.png (layout: {dir}\{id}\crater.png hoac {dir}\{id}.png).</summary>
        public List<string> ExtraCraterDirs { get; private set; }

        /// <summary>Ket qua lan Scan gan nhat (dung cho ClientSiteCheck / UI).</summary>
        public List<BombIdInfo> LastScan { get { return _lastScan; } }
        public BombRipStats LastRipStats { get { return _lastRipStats; } }
        public BombMaskStats LastMaskStats { get { return _lastMaskStats; } }

        /// <summary>Map ball id -&gt; Crater id (tu BallList). Dung de tim nguon crater.png cho mask.</summary>
        public Dictionary<int, int> BallCraterMap { get { return _ballCrater; } }
        /// <summary>Map ball id -&gt; BombPartical (id cua bullet&lt;id&gt;.swf client dung).</summary>
        public Dictionary<int, int> BallBombParticalMap { get { return _ballBombPartical; } }
        /// <summary>Tap crater id duoc BallList tham chieu.</summary>
        public HashSet<int> CraterIds { get { return _craterIds; } }

        /// <summary>
        /// Id cua crater.png dung lam nguon mask cho 1 bomb id:
        ///  - id la ball: lay thuoc tinh Crater cua ball (server nap mask theo ball id)
        ///  - id la crater id / khac: dung chinh id.
        /// Tra ve 0 neu ball khong co crater (khong can mask).
        /// </summary>
        public int CraterSourceId(int id)
        {
            int c;
            if (_ballCrater.TryGetValue(id, out c)) return c > 0 ? c : 0;
            return id;
        }

        /// <summary>Id nay co can file .bomb tren server khong (ball co Crater khac rong/0).</summary>
        public bool IsMaskNeeded(int id)
        {
            int c;
            if (_ballCrater.TryGetValue(id, out c)) return c > 0;
            return true;
        }

        // ============================================================ duong dan

        public static string AssetRoot(AppSettings s)
        {
            if (s == null) return Path.Combine("image", "bomb");
            if (!string.IsNullOrWhiteSpace(s.BombAssetDir)) return s.BombAssetDir;
            var web = s.WebSiteRoot ?? string.Empty;
            return Path.Combine(web, "image", "bomb");
        }

        /// <summary>Thu muc chua .bomb cua server Fight (nguon "ServerMask").</summary>
        public static string ServerBombDir(AppSettings s)
        {
            if (s == null || string.IsNullOrWhiteSpace(s.ServiceRoot)) return null;
            return Path.Combine(s.ServiceRoot, "Fight", "Bomb");
        }

        /// <summary>Tat ca cac thu muc Bomb dang ton tai ma server nap (Fight/Road + ban build debug).</summary>
        public static List<string> TargetMaskDirs(AppSettings s)
        {
            var list = new List<string>();
            if (s == null) return list;
            var candidates = new List<string>();
            if (!string.IsNullOrWhiteSpace(s.ServiceRoot))
            {
                candidates.Add(Path.Combine(s.ServiceRoot, "Fight", "Bomb"));
                candidates.Add(Path.Combine(s.ServiceRoot, "Road", "Bomb"));
            }
            if (!string.IsNullOrWhiteSpace(s.FullSourceRoot))
            {
                candidates.Add(Path.Combine(s.FullSourceRoot, "Fighting.Service", "bin", "Debug", "net472", "Bomb"));
                candidates.Add(Path.Combine(s.FullSourceRoot, "Road.Service", "bin", "Debug", "net472", "Bomb"));
            }
            foreach (var c in candidates)
            {
                try { if (Directory.Exists(c)) list.Add(c); } catch { }
            }
            return list;
        }

        public static string CraterLocalPath(AppSettings s, int id)
        {
            return Path.Combine(AssetRoot(s), "crater", id.ToString(CultureInfo.InvariantCulture), "crater.png");
        }

        public static string CraterBrinkLocalPath(AppSettings s, int id)
        {
            return Path.Combine(AssetRoot(s), "crater", id.ToString(CultureInfo.InvariantCulture), "craterBrink.png");
        }

        public static string BulletLocalPath(AppSettings s, int id)
        {
            return Path.Combine(AssetRoot(s), "bullet", "bullet" + id.ToString(CultureInfo.InvariantCulture) + ".swf");
        }

        public static string BlastOutLocalPath(AppSettings s, int id)
        {
            return Path.Combine(AssetRoot(s), "blastOut", "blastOut" + id.ToString(CultureInfo.InvariantCulture) + ".swf");
        }

        /// <summary>Duong dan tuong doi (dung de ghep URL va lam key cache).</summary>
        public static string RelPath(string type, int id)
        {
            string sid = id.ToString(CultureInfo.InvariantCulture);
            switch (type)
            {
                case TypeCrater: return "image/bomb/crater/" + sid + "/crater.png";
                case TypeBrink: return "image/bomb/crater/" + sid + "/craterBrink.png";
                case TypeBullet: return "image/bomb/bullet/bullet" + sid + ".swf";
                case TypeBlastOut: return "image/bomb/blastOut/blastOut" + sid + ".swf";
            }
            throw new ArgumentException("asset type khong hop le: " + type);
        }

        public static string LocalPath(AppSettings s, string type, int id)
        {
            switch (type)
            {
                case TypeCrater: return CraterLocalPath(s, id);
                case TypeBrink: return CraterBrinkLocalPath(s, id);
                case TypeBullet: return BulletLocalPath(s, id);
                case TypeBlastOut: return BlastOutLocalPath(s, id);
            }
            throw new ArgumentException("asset type khong hop le: " + type);
        }

        private static List<string> Bases(AppSettings s)
        {
            var list = new List<string>();
            if (s != null && s.ResBases != null)
            {
                foreach (var b in s.ResBases)
                {
                    if (!string.IsNullOrWhiteSpace(b))
                    {
                        var v = b.Trim();
                        if (!v.EndsWith("/")) v += "/";
                        if (!list.Contains(v)) list.Add(v);
                    }
                }
            }
            if (list.Count == 0)
            {
                list.Add("https://res.gn.zing.vn/");
                list.Add("https://gunny.vcdn.vn/");
            }
            return list;
        }

        // ============================================================ thu thap id

        /// <summary>
        /// Tap bomb id duoc request tham chieu: hop cua
        ///  - BallList: thuoc tinh Crater (crater id, gom ca "0")
        ///  - bombconfig: cac thuoc tinh Common / CommonAddWound / CommonMultiBall / Special (bomb id &gt; 0)
        /// </summary>
        public List<int> CollectReferencedIds(XmlDocInfo ballList, XmlDocInfo bombConfig)
        {
            var set = new SortedSet<int>();
            if (ballList != null && ballList.Rows != null)
            {
                foreach (var row in ballList.Rows)
                {
                    int id;
                    if (TryParseInt(RowValue(row, "Crater"), out id) && id >= 0) set.Add(id);
                }
            }
            if (bombConfig != null && bombConfig.Rows != null)
            {
                foreach (var row in bombConfig.Rows)
                {
                    foreach (var key in BombConfigKeys)
                    {
                        int id;
                        if (TryParseInt(RowValue(row, key), out id) && id > 0) set.Add(id);
                    }
                }
            }
            return new List<int>(set);
        }

        // ============================================================ scan

        /// <summary>
        /// Quet trang thai cho tat ca bomb id tham chieu trong request docs
        /// (tu dong nhan dien BallList / bombconfig trong danh sach).
        /// </summary>
        public List<BombIdInfo> Scan(AppSettings settings, IEnumerable<XmlDocInfo> requestDocs, Action<BombIdInfo> progress, CancellationToken ct)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            var docs = new List<XmlDocInfo>();
            if (requestDocs != null)
            {
                foreach (var d in requestDocs) if (d != null) docs.Add(d);
            }
            XmlDocInfo ballDoc, bombDoc;
            SplitDocs(docs, out ballDoc, out bombDoc);
            BuildIdMaps(ballDoc);
            var ids = CollectReferencedIds(ballDoc, bombDoc);
            _log.Step("Bomb scan: " + ids.Count + " id tham chieu (" +
                      (ballDoc == null ? "khong co BallList" : "BallList=" + ballDoc.ItemCount) + ", " +
                      (bombDoc == null ? "khong co bombconfig" : "bombconfig=" + bombDoc.ItemCount) + ")");
            return ScanIds(settings, ids, progress, ct);
        }

        /// <summary>Quet mot danh sach id bat ky (BallList/BombPartical/ball id...).</summary>
        public List<BombIdInfo> ScanIds(AppSettings settings, List<int> ids, Action<BombIdInfo> progress, CancellationToken ct)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            var idList = ids ?? new List<int>();
            var results = new BombIdInfo[idList.Count];
            if (idList.Count == 0) { _lastScan = new List<BombIdInfo>(); return _lastScan; }

            int concurrency = 8;
            try
            {
                if (settings.Concurrency > 0) concurrency = Math.Max(1, Math.Min(8, settings.Concurrency));
            }
            catch { concurrency = 8; }

            try
            {
                int done = 0;
                var options = new ParallelOptions { MaxDegreeOfParallelism = concurrency, CancellationToken = ct };
                Parallel.For(0, idList.Count, options, i =>
                {
                    ct.ThrowIfCancellationRequested();
                    var info = BuildInfo(settings, idList[i]);
                    results[i] = info;
                    var cb = progress;
                    if (cb != null)
                    {
                        lock (_callbackLock) { try { cb(info); } catch { } }
                    }
                    int n = Interlocked.Increment(ref done);
                    if (n % 250 == 0) _log.Info("Bomb scan: " + n + "/" + idList.Count);
                });
            }
            finally
            {
                SaveProbeCache();
            }

            var list = new List<BombIdInfo>(idList.Count);
            foreach (var r in results) if (r != null) list.Add(r);
            list.Sort((a, b) => a.Id.CompareTo(b.Id));
            _lastScan = list;
            int withMask = list.Count(x => x.ServerMask);
            _log.Okay("Bomb scan xong: " + list.Count + " id, co mask=" + withMask + ", thieu mask=" + (list.Count - withMask));
            return list;
        }

        /// <summary>Xay map ball id -&gt; Crater/BombPartical va tap crater id tu BallList.</summary>
        public void BuildIdMaps(XmlDocInfo ballDoc)
        {
            var map = new Dictionary<int, int>();
            var bombPartical = new Dictionary<int, int>();
            var craters = new HashSet<int>();
            if (ballDoc != null && ballDoc.Rows != null)
            {
                foreach (var row in ballDoc.Rows)
                {
                    int id;
                    if (!TryParseInt(RowValue(row, "ID"), out id) || id < 0) continue;
                    int c;
                    if (TryParseInt(RowValue(row, "Crater"), out c))
                    {
                        map[id] = c;
                        if (c >= 0) craters.Add(c);
                    }
                    else map[id] = 0;
                    int bp;
                    if (TryParseInt(RowValue(row, "BombPartical"), out bp) && bp > 0) bombPartical[id] = bp;
                }
            }
            _ballCrater = map;
            _ballBombPartical = bombPartical;
            _craterIds = craters;
        }

        /// <summary>Build 1 BombIdInfo: kiem tra file local + probe remote (cache).</summary>
        private BombIdInfo BuildInfo(AppSettings s, int id)
        {
            var info = new BombIdInfo { Id = id };
            var targets = TargetMaskDirs(s);
            string serverDir = ServerBombDir(s);
            info.ServerMask = !string.IsNullOrEmpty(serverDir) && File.Exists(Path.Combine(serverDir, id + ".bomb"));
            bool maskAny = info.ServerMask;
            if (!maskAny)
            {
                foreach (var t in targets)
                {
                    if (File.Exists(Path.Combine(t, id + ".bomb"))) { maskAny = true; break; }
                }
            }

            info.LocalCrater = SafeExists(CraterLocalPath(s, id));
            info.LocalBrink = SafeExists(CraterBrinkLocalPath(s, id));
            info.LocalBullet = SafeExists(BulletLocalPath(s, id));
            info.LocalBlastOut = SafeExists(BlastOutLocalPath(s, id));

            if (!info.CraterPng) info.CraterPng = ProbeOk(s, RelPath(TypeCrater, id));
            if (!info.CraterBrink) info.CraterBrink = ProbeOk(s, RelPath(TypeBrink, id));
            if (!info.Bullet) info.Bullet = ProbeOk(s, RelPath(TypeBullet, id));
            if (!info.BlastOut) info.BlastOut = ProbeOk(s, RelPath(TypeBlastOut, id));

            // Xac dinh nguon crater.png dung de sinh mask: voi ball id thi lay theo Crater cua ball.
            int sourceId = CraterSourceId(id);
            bool maskNeeded = IsMaskNeeded(id);
            bool sourceKnown = sourceId > 0;
            bool sourceLocal = sourceKnown && SafeExists(CraterLocalPath(s, sourceId));
            bool sourceRemote = sourceKnown && ProbeOk(s, RelPath(TypeCrater, sourceId));
            bool hasCraterSource = sourceKnown && (sourceLocal || sourceRemote);
            if (sourceId == id) hasCraterSource = info.LocalCrater || info.CraterPng;

            if (!maskAny)
            {
                if (!maskNeeded) info.Resolution = "ok";
                else info.Resolution = hasCraterSource ? "missing-mask" : "no-source";
            }
            else
            {
                info.Resolution = "ok";
            }

            var missing = new List<string>();
            if (!(info.CraterPng || info.LocalCrater)) missing.Add("crater.png");
            if (!(info.CraterBrink || info.LocalBrink)) missing.Add("craterBrink.png");
            if (!(info.Bullet || info.LocalBullet)) missing.Add("bullet" + id + ".swf");
            if (!(info.BlastOut || info.LocalBlastOut)) missing.Add("blastOut" + id + ".swf");
            var notes = new List<string>();
            if (!maskAny)
            {
                if (!maskNeeded) notes.Add("khong can .bomb (ball khong co Crater)");
                else if (hasCraterSource)
                    notes.Add(sourceId == id ? "thieu .bomb, co the tao tu crater.png" : "thieu .bomb, nguon crater.png = id " + sourceId);
                else notes.Add("thieu .bomb va khong co crater.png");
            }
            else if (maskNeeded && sourceId > 0 && sourceId != id)
            {
                notes.Add("mask nguon tu crater " + sourceId);
            }
            if (missing.Count > 0) notes.Add("thieu asset: " + string.Join(", ", missing.ToArray()));
            if (id == 0) notes.Add("id 0 = khong co crater (gia tri mac dinh)");
            info.Note = notes.Count == 0 ? null : string.Join("; ", notes.ToArray());
            return info;
        }

        // ============================================================ rip asset

        /// <summary>
        /// Tai cac asset con thieu cho danh sach id ve settings.BombAssetDir.
        /// Bo qua file da co neu overwrite=false. Cap nhat cac co Local* tren info.
        /// Tra ve so file da tai.
        /// </summary>
        public int RipAssets(AppSettings settings, List<BombIdInfo> infos, bool overwrite, Action<string> progress, CancellationToken ct)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            var stats = new BombRipStats();
            try
            {
                var work = new List<RipWorkItem>();
                if (infos != null)
                {
                    foreach (var info in infos)
                    {
                        if (info == null) continue;
                        foreach (var type in AssetTypes)
                        {
                            work.Add(new RipWorkItem
                            {
                                Info = info,
                                Type = type,
                                Rel = RelPath(type, info.Id),
                                Local = LocalPath(settings, type, info.Id),
                                GetLocal = MakeLocalGetter(info, type),
                                SetLocal = MakeLocalSetter(info, type)
                            });
                        }
                    }
                }
                if (work.Count == 0) { _lastRipStats = stats; return 0; }

                int concurrency = 4;
                try
                {
                    if (settings.Concurrency > 0) concurrency = Math.Max(1, Math.Min(4, settings.Concurrency));
                }
                catch { concurrency = 4; }

                _log.Step("Rip bomb asset: " + work.Count + " asset cho " + (infos == null ? 0 : infos.Count) + " id");
                int done = 0;
                var options = new ParallelOptions { MaxDegreeOfParallelism = concurrency, CancellationToken = ct };
                Parallel.ForEach(work, options, item =>
                {
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        bool existing = SafeExists(item.Local);
                        if (existing && !overwrite)
                        {
                            item.SetLocal(true);
                            stats.AddSkipped(item.Type);
                            return;
                        }
                        var rec = Probe(settings, item.Rel);
                        if (rec == null || rec.Status != 200)
                        {
                            stats.AddUnavailable(item.Type);
                            return;
                        }
                        byte[] data;
                        string err;
                        if (!TryDownload(settings, item.Rel, item.Type, rec.Base, out data, out err))
                        {
                            stats.AddFailed(item.Type);
                            _log.Warn("Tai loi " + item.Rel + ": " + err);
                            return;
                        }
                        Paths.WriteAtomic(item.Local, data);
                        item.SetLocal(true);
                        stats.AddDownloaded(item.Type);
                        if (progress != null)
                        {
                            string msg = "asset " + item.Rel + " (" + Paths.FormatSize(data.Length) + ")";
                            lock (_callbackLock) { try { progress(msg); } catch { } }
                        }
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        stats.AddFailed(item.Type);
                        _log.Error("Rip asset loi " + item.Rel + ": " + ex.Message);
                        lock (_noteLock)
                        {
                            item.Info.Note = AppendNote(item.Info.Note, "loi tai " + item.Type + ": " + ex.Message);
                        }
                    }
                    finally
                    {
                        int n = Interlocked.Increment(ref done);
                        if (n % 300 == 0) _log.Info("Rip asset: " + n + "/" + work.Count);
                    }
                });
            }
            finally
            {
                SaveProbeCache();
            }
            _lastRipStats = stats;
            _log.Okay("Rip asset xong: tai=" + stats.Downloaded + ", bo qua=" + stats.Skipped +
                      ", khong co tren CDN=" + stats.Unavailable + ", loi=" + stats.Failed);
            foreach (var type in AssetTypes)
            {
                int d, sk, un, fa;
                stats.TypeCounts(type, out d, out sk, out un, out fa);
                _log.Info("  " + type + ": tai=" + d + " bo qua=" + sk + " 404=" + un + " loi=" + fa);
            }
            return stats.Downloaded;
        }

        private Func<bool> MakeLocalGetter(BombIdInfo info, string type)
        {
            switch (type)
            {
                case TypeCrater: return () => info.LocalCrater;
                case TypeBrink: return () => info.LocalBrink;
                case TypeBullet: return () => info.LocalBullet;
                case TypeBlastOut: return () => info.LocalBlastOut;
            }
            return () => false;
        }

        private Action<bool> MakeLocalSetter(BombIdInfo info, string type)
        {
            switch (type)
            {
                case TypeCrater: return v => info.LocalCrater = v;
                case TypeBrink: return v => info.LocalBrink = v;
                case TypeBullet: return v => info.LocalBullet = v;
                case TypeBlastOut: return v => info.LocalBlastOut = v;
            }
            return v => { };
        }

        private sealed class RipWorkItem
        {
            public BombIdInfo Info;
            public string Type;
            public string Rel;
            public string Local;
            public Func<bool> GetLocal;
            public Action<bool> SetLocal;
        }

        // ============================================================ generate mask

        /// <summary>
        /// Voi cac id thieu mask tren server: lay crater.png (mirror local -> CDN -> ExtraCraterDirs),
        /// chuyen thanh .bomb va deploy vao tat ca folder Bomb dang ton tai.
        /// Tra ve so file .bomb da ghi.
        /// </summary>
        public int GenerateAndDeployMasks(AppSettings settings, List<BombIdInfo> infos, bool overwrite, Action<string> progress, CancellationToken ct)
        {
            if (settings == null) throw new ArgumentNullException("settings");
            var stats = new BombMaskStats();
            try
            {
                var targets = TargetMaskDirs(settings);
                if (targets.Count == 0)
                {
                    _log.Error("Khong tim thay thu muc Bomb nao de deploy (kiem tra ServiceRoot/FullSourceRoot).");
                    _lastMaskStats = stats;
                    return 0;
                }
                _log.Step("Generate mask: " + targets.Count + " folder dich: " + string.Join(" | ", targets.ToArray()));
                string backupTag = "_bak_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string serverDir = ServerBombDir(settings);

                foreach (var info in infos ?? new List<BombIdInfo>())
                {
                    ct.ThrowIfCancellationRequested();
                    if (info == null) continue;
                    bool hadServerMask = info.ServerMask;

                    var need = new List<string>();
                    foreach (var dir in targets)
                    {
                        string dst = Path.Combine(dir, info.Id + ".bomb");
                        if (!File.Exists(dst)) need.Add(dst);
                        else if (overwrite) need.Add(dst);
                    }
                    if (need.Count == 0) { stats.SkippedIds++; continue; }

                    if (!IsMaskNeeded(info.Id))
                    {
                        // ball khong co Crater => server khong nap mask, khong can tao
                        stats.SkippedIds++;
                        lock (_noteLock) { info.Note = AppendNote(info.Note, "khong can .bomb (ball khong co Crater)"); }
                        continue;
                    }

                    int sourceId = CraterSourceId(info.Id);
                    byte[] bomb = null;
                    int w = 0, h = 0;
                    if (hadServerMask)
                    {
                        // Da co .bomb o folder chinh (Fight): chep nguyen file sang cac folder con thieu
                        // de mask giong het nhau giua cac ban server (khong tao lai tu PNG).
                        bomb = FindExistingMask(settings, info.Id);
                        if (bomb != null && !TryReadBombHeader(bomb, out w, out h)) bomb = null;
                    }
                    if (bomb == null)
                    {
                        var png = LoadCraterSource(settings, sourceId);
                        if (png == null && sourceId != info.Id) png = LoadCraterSource(settings, info.Id);
                        if (png == null)
                        {
                            stats.NoSource++;
                            info.Resolution = "no-source";
                            lock (_noteLock) { info.Note = AppendNote(info.Note, "khong co crater.png de tao mask"); }
                            _log.Warn("Khong co crater.png cho id " + info.Id + " (nguon crater " + sourceId + ") -> bo qua tao mask");
                            continue;
                        }
                        try
                        {
                            bomb = PngToBombMask(png);
                            w = BitConverter.ToInt32(bomb, 0);
                            h = BitConverter.ToInt32(bomb, 4);
                        }
                        catch (Exception ex)
                        {
                            stats.Failed++;
                            _log.Error("PNG -> mask loi id " + info.Id + ": " + ex.Message);
                            continue;
                        }
                    }

                    int wrote = 0;
                    foreach (var dst in need)
                    {
                        try
                        {
                            if (File.Exists(dst) && overwrite)
                            {
                                string bakDir = Path.Combine(Path.GetDirectoryName(dst), backupTag);
                                Paths.EnsureDir(bakDir);
                                File.Copy(dst, Path.Combine(bakDir, Path.GetFileName(dst)), true);
                            }
                            Paths.WriteAtomic(dst, bomb);
                            wrote++;
                            string label = FolderLabel(settings, Path.GetDirectoryName(dst));
                            lock (_statsLock) { stats.DeployedByFolder[label] = GetCount(stats.DeployedByFolder, label) + 1; }
                        }
                        catch (Exception ex)
                        {
                            _log.Error("Ghi mask loi " + dst + ": " + ex.Message);
                        }
                    }
                    if (wrote > 0)
                    {
                        stats.GeneratedIds++;
                        if (hadServerMask) stats.FilledIds++; else stats.NewIds++;
                        stats.Deployed += wrote;
                        info.ServerMask = !string.IsNullOrEmpty(serverDir) && File.Exists(Path.Combine(serverDir, info.Id + ".bomb"));
                        if (info.ServerMask) info.Resolution = "ok";
                        lock (_noteLock)
                        {
                            info.Note = AppendNote(info.Note, hadServerMask
                                ? "da dong bo mask " + w + "x" + h + " tu folder chinh -> " + wrote + " folder"
                                : "da tao mask " + w + "x" + h +
                                  (sourceId == info.Id ? "" : " (nguon crater " + sourceId + ")") + " -> " + wrote + " folder");
                        }
                        if (progress != null)
                        {
                            string msg = (hadServerMask ? "dong bo mask " : "tao mask ") + info.Id + ".bomb (" + w + "x" + h + ")" +
                                         (hadServerMask || sourceId == info.Id ? "" : " tu crater " + sourceId) + " -> " + wrote + " folder";
                            lock (_callbackLock) { try { progress(msg); } catch { } }
                        }
                    }
                    else stats.Failed++;
                }
            }
            finally
            {
                SaveProbeCache();
            }
            _lastMaskStats = stats;
            _log.Okay("Generate mask xong: tao=" + stats.GeneratedIds + " id (moi=" + stats.NewIds + ", bo sung folder khac=" +
                      stats.FilledIds + "), ghi=" + stats.Deployed + " file, bo qua=" + stats.SkippedIds +
                      ", khong co nguon=" + stats.NoSource + ", loi=" + stats.Failed);
            return stats.Deployed;
        }

        private readonly object _statsLock = new object();

        private static string FolderLabel(AppSettings s, string bombDir)
        {
            if (string.IsNullOrEmpty(bombDir)) return "?";
            string full = Path.GetFullPath(bombDir).TrimEnd('\\');
            var parts = full.Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 1; i < parts.Length; i++)
            {
                if (string.Equals(parts[i], "bin", StringComparison.OrdinalIgnoreCase))
                {
                    return "FullSource\\" + parts[i - 1] + "\\Bomb";
                }
            }
            string parent = Path.GetFileName(Path.GetDirectoryName(full));
            return "Service\\" + parent + "\\Bomb";
        }

        private static int GetCount(Dictionary<string, int> dict, string key)
        {
            int v;
            return dict.TryGetValue(key, out v) ? v : 0;
        }

        /// <summary>Tim bytes .bomb da co trong cac folder dich (uu tien folder Fight cua server).</summary>
        public byte[] FindExistingMask(AppSettings settings, int id)
        {
            var dirs = new List<string>();
            string server = ServerBombDir(settings);
            if (!string.IsNullOrEmpty(server)) dirs.Add(server);
            foreach (var d in TargetMaskDirs(settings)) if (!dirs.Contains(d)) dirs.Add(d);
            foreach (var d in dirs)
            {
                try
                {
                    string p = Path.Combine(d, id + ".bomb");
                    if (File.Exists(p)) return File.ReadAllBytes(p);
                }
                catch { }
            }
            return null;
        }

        /// <summary>Lay bytes crater.png cho 1 id: mirror local -> CDN -> ExtraCraterDirs.</summary>
        public byte[] LoadCraterSource(AppSettings settings, int id)
        {
            string local = CraterLocalPath(settings, id);
            try
            {
                if (File.Exists(local)) return File.ReadAllBytes(local);
            }
            catch (Exception ex) { _log.Warn("Doc crater local loi " + local + ": " + ex.Message); }

            string rel = RelPath(TypeCrater, id);
            if (ProbeOk(settings, rel))
            {
                byte[] data;
                string err;
                if (TryDownload(settings, rel, TypeCrater, null, out data, out err)) return data;
                if (!string.IsNullOrEmpty(err)) _log.Warn("Tai crater.png loi id " + id + ": " + err);
            }
            foreach (var dir in ExtraCraterDirs)
            {
                try
                {
                    string p1 = Path.Combine(dir, id.ToString(CultureInfo.InvariantCulture), "crater.png");
                    if (File.Exists(p1)) return File.ReadAllBytes(p1);
                    string p2 = Path.Combine(dir, id.ToString(CultureInfo.InvariantCulture) + ".png");
                    if (File.Exists(p2)) return File.ReadAllBytes(p2);
                }
                catch { }
            }
            return null;
        }

        // ============================================================ PNG <-> mask

        /// <summary>
        /// Chuyen crater.png (32bppArgb) thanh bytes file .bomb:
        /// int32 LE width, int32 LE height, 1bpp, stride=(w>>3)+1, MSB trai nhat, bit=(alpha>=128).
        /// </summary>
        public static byte[] PngToBombMask(byte[] pngBytes)
        {
            if (pngBytes == null || pngBytes.Length == 0) throw new ArgumentException("pngBytes rong", "pngBytes");
            using (var ms = new MemoryStream(pngBytes))
            using (var src = new Bitmap(ms))
            {
                int w = src.Width;
                int h = src.Height;
                if (w <= 0 || h <= 0) throw new InvalidDataException("PNG kich thuoc khong hop le: " + w + "x" + h);
                if (w > 8192 || h > 8192) throw new InvalidDataException("PNG qua lon: " + w + "x" + h);

                int stride = (w >> 3) + 1;
                var outBytes = new byte[8 + stride * h];
                WriteInt32Le(outBytes, 0, w);
                WriteInt32Le(outBytes, 4, h);
                var px = ReadArgb(src, w, h);
                for (int y = 0; y < h; y++)
                {
                    int rowBase = 8 + y * stride;
                    int pxBase = y * w * 4;
                    for (int x = 0; x < w; x++)
                    {
                        int alpha = px[pxBase + x * 4 + 3];
                        if (alpha >= 128) outBytes[rowBase + (x >> 3)] |= (byte)(1 << (7 - (x & 7)));
                    }
                }
                return outBytes;
            }
        }

        /// <summary>
        /// Chuyen bytes .bomb thanh PNG preview: pixel bi pha = trang duc (255,255,255,255),
        /// pixel con lai = trong suot (0,0,0,0).
        /// </summary>
        public static byte[] BombMaskToPng(byte[] bombBytes)
        {
            int w, h;
            if (!TryReadBombHeader(bombBytes, out w, out h)) throw new InvalidDataException("File .bomb khong hop le");
            int stride = (w >> 3) + 1;
            int need = 8 + stride * h;
            if (bombBytes.Length < need) throw new InvalidDataException("File .bomb thieu du lieu: can " + need + " byte, co " + bombBytes.Length);

            using (var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb))
            {
                var rect = new Rectangle(0, 0, w, h);
                var data = bmp.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                try
                {
                    int absStride = Math.Abs(data.Stride);
                    var row = new byte[absStride];
                    for (int y = 0; y < h; y++)
                    {
                        Array.Clear(row, 0, row.Length);
                        int srcRow = 8 + y * stride;
                        for (int x = 0; x < w; x++)
                        {
                            int bit = (bombBytes[srcRow + (x >> 3)] >> (7 - (x & 7))) & 1;
                            if (bit == 0) continue;
                            int o = x * 4;
                            row[o] = 255; row[o + 1] = 255; row[o + 2] = 255; row[o + 3] = 255; // BGRA = trang
                        }
                        int dstRow = data.Stride >= 0 ? y : (h - 1 - y);
                        Marshal.Copy(row, 0, IntPtr.Add(data.Scan0, dstRow * absStride), absStride);
                    }
                }
                finally { bmp.UnlockBits(data); }
                using (var outMs = new MemoryStream())
                {
                    bmp.Save(outMs, ImageFormat.Png);
                    return outMs.ToArray();
                }
            }
        }

        /// <summary>Doc header .bomb; false neu khong hop le.</summary>
        public static bool TryReadBombHeader(byte[] bombBytes, out int width, out int height)
        {
            width = 0; height = 0;
            if (bombBytes == null || bombBytes.Length < 8) return false;
            width = BitConverter.ToInt32(bombBytes, 0);
            height = BitConverter.ToInt32(bombBytes, 4);
            return width > 0 && height > 0 && width <= 8192 && height <= 8192;
        }

        /// <summary>So byte toi thieu cua file .bomb theo cong thuc stride=(w>>3)+1.</summary>
        public static long ExpectedBombLength(int width, int height)
        {
            return 8L + (long)((width >> 3) + 1) * height;
        }

        private static byte[] ReadArgb(Bitmap src, int w, int h)
        {
            Bitmap normalized = null;
            Bitmap work = src;
            if (src.PixelFormat != PixelFormat.Format32bppArgb && src.PixelFormat != PixelFormat.Format32bppPArgb)
            {
                normalized = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(normalized))
                {
                    g.CompositingMode = CompositingMode.SourceCopy;
                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = PixelOffsetMode.Half;
                    g.DrawImage(src, new Rectangle(0, 0, w, h));
                }
                work = normalized;
            }
            try
            {
                var px = new byte[w * h * 4];
                var rect = new Rectangle(0, 0, w, h);
                var data = work.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                try
                {
                    int absStride = Math.Abs(data.Stride);
                    var row = new byte[absStride];
                    for (int y = 0; y < h; y++)
                    {
                        int srcRow = data.Stride >= 0 ? y : (h - 1 - y);
                        Marshal.Copy(IntPtr.Add(data.Scan0, srcRow * absStride), row, 0, absStride);
                        Buffer.BlockCopy(row, 0, px, y * w * 4, w * 4);
                    }
                }
                finally { work.UnlockBits(data); }
                return px;
            }
            finally
            {
                if (normalized != null) normalized.Dispose();
            }
        }

        private static void WriteInt32Le(byte[] buf, int offset, int value)
        {
            buf[offset] = (byte)(value & 0xFF);
            buf[offset + 1] = (byte)((value >> 8) & 0xFF);
            buf[offset + 2] = (byte)((value >> 16) & 0xFF);
            buf[offset + 3] = (byte)((value >> 24) & 0xFF);
        }

        // ============================================================ client site check

        /// <summary>
        /// Doc SITE trong settings.ClientConfigPath va kiem tra host do con thieu asset nao
        /// cho cac bomb id moi (id chua co mask trong Service Bomb). Tra ve loi khuyen tieng Viet.
        /// </summary>
        public string ClientSiteCheck(AppSettings settings)
        {
            return ClientSiteCheck(settings, null);
        }

        /// <summary>
        /// Kiem tra host SITE trong config.xml theo loai asset client thuc su can cho tung id moi:
        ///  - ball id: blastOut{ballId}.swf + bullet{BombPartical}.swf
        ///  - crater id: crater.png + craterBrink.png
        /// Tra ve loi khuyen tieng Viet.
        /// </summary>
        public string ClientSiteCheck(AppSettings settings, List<int> newIds)
        {
            var sb = new StringBuilder();
            if (settings == null) return "Thieu settings.";
            string site = null;
            try
            {
                if (!string.IsNullOrWhiteSpace(settings.ClientConfigPath) && File.Exists(settings.ClientConfigPath))
                {
                    var doc = XDocument.Load(settings.ClientConfigPath);
                    var el = doc.Descendants().FirstOrDefault(e => e.Name.LocalName == "SITE");
                    if (el != null) site = (string)el.Attribute("value");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine("Khong doc duoc config.xml: " + ex.Message);
            }
            if (string.IsNullOrWhiteSpace(site))
            {
                var bs = Bases(settings);
                if (bs.Count > 0) site = bs[0];
            }
            if (string.IsNullOrWhiteSpace(site)) return "Khong xac dinh duoc host SITE trong config.xml.";
            if (!site.EndsWith("/")) site += "/";

            var ids = new SortedSet<int>();
            if (newIds != null) foreach (var id in newIds) ids.Add(id);
            if (ids.Count == 0 && _lastScan != null)
            {
                foreach (var info in _lastScan)
                {
                    if (info != null && !info.ServerMask && IsMaskNeeded(info.Id)) ids.Add(info.Id);
                }
            }
            if (ids.Count == 0) foreach (var id in DiscoverIdsFromMirror(settings)) ids.Add(id);
            if (ids.Count == 0)
            {
                sb.AppendLine("SITE client (config.xml): " + site);
                sb.AppendLine("Chua co bomb id moi nao de kiem tra (hay chay 'scan' truoc).");
                return sb.ToString().TrimEnd();
            }

            var idList = ids.ToList();
            // Tap asset client thuc su can (dung chung logic voi PathManager.as / BallInfo.loadBombAsset)
            var required = new List<KeyValuePair<string, string>>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Action<string, int> add = (type, id) =>
            {
                string rel = RelPath(type, id);
                if (seen.Add(rel)) required.Add(new KeyValuePair<string, string>(type, rel));
            };
            foreach (var id in idList)
            {
                bool isBall = _ballCrater.ContainsKey(id);
                bool isCrater = _craterIds.Contains(id);
                if (isBall)
                {
                    add(TypeBlastOut, id);
                    int bp;
                    if (_ballBombPartical.TryGetValue(id, out bp) && bp > 0) add(TypeBullet, bp);
                }
                if (isCrater || !isBall)
                {
                    add(TypeCrater, id);
                    add(TypeBrink, id);
                }
                if (!isBall && !isCrater)
                {
                    add(TypeBlastOut, id);
                    add(TypeBullet, id);
                }
            }

            var counters = new ConcurrentDictionary<string, int[]>(StringComparer.OrdinalIgnoreCase);
            var missingRels = new ConcurrentBag<string>();
            int concurrency = 8;
            try { if (settings.Concurrency > 0) concurrency = Math.Max(1, Math.Min(8, settings.Concurrency)); } catch { }
            var options = new ParallelOptions { MaxDegreeOfParallelism = concurrency };
            Parallel.ForEach(required, options, req =>
            {
                var c = counters.GetOrAdd(req.Key, _ => new int[2]);
                Interlocked.Increment(ref c[0]);
                bool ok = HeadKeepAlive(HttpUtil.Combine(site, req.Value)) == 200;
                if (!ok)
                {
                    Interlocked.Increment(ref c[1]);
                    missingRels.Add(req.Value);
                }
            });

            sb.AppendLine("SITE client (config.xml): " + site);
            sb.AppendLine("Kiem tra " + idList.Count + " bomb id moi, theo dung asset client load:");
            int totalMissing = 0;
            foreach (var type in AssetTypes)
            {
                int[] c;
                if (!counters.TryGetValue(type, out c) || c[0] == 0) continue;
                sb.AppendLine("- " + Label(type) + ": co " + (c[0] - c[1]) + "/" + c[0] +
                              (c[1] > 0 ? ", thieu " + c[1] : ""));
                totalMissing += c[1];
            }
            if (totalMissing == 0)
            {
                sb.AppendLine("Host SITE hien tai da co du asset cho " + idList.Count + " id moi.");
            }
            else
            {
                int resOk = 0, resMissing = 0;
                foreach (var rel in missingRels)
                {
                    var rec = Probe(settings, rel);
                    if (rec != null && rec.Status == 200) resOk++;
                    else resMissing++;
                }
                sb.AppendLine("CDN du phong (" + string.Join(", ", Bases(settings).ToArray()) + "): co " + resOk + "/" +
                              totalMissing + " asset ma SITE dang thieu.");
                if (resOk > 0)
                {
                    sb.AppendLine("Khuyen nghi: chay buoc 'Rip' de mirror " + totalMissing + " asset thieu vao " + AssetRoot(settings) +
                                  ", roi tro SITE trong config.xml sang CDN con day du (vd https://res.gn.zing.vn/) hoac host mirror, restart web server.");
                }
                else
                {
                    sb.AppendLine("Khuyen nghi: khong CDN nao co du asset thieu - can bo sung thu cong vao " + AssetRoot(settings) + ".");
                }
            }
            sb.AppendLine("Danh sach id moi: " + string.Join(", ", idList.Take(30).Select(x => x.ToString()).ToArray()) +
                          (idList.Count > 30 ? ", ..." : ""));
            return sb.ToString().TrimEnd();
        }

        private static string Label(string type)
        {
            switch (type)
            {
                case TypeCrater: return "crater.png";
                case TypeBrink: return "craterBrink.png";
                case TypeBullet: return "bullet<id>.swf";
                case TypeBlastOut: return "blastOut<id>.swf";
            }
            return type;
        }

        private IEnumerable<int> DiscoverIdsFromMirror(AppSettings s)
        {
            var set = new SortedSet<int>();
            try
            {
                string root = AssetRoot(s);
                string craterDir = Path.Combine(root, "crater");
                if (Directory.Exists(craterDir))
                {
                    foreach (var d in Directory.GetDirectories(craterDir))
                    {
                        int id;
                        if (TryParseInt(Path.GetFileName(d), out id)) set.Add(id);
                    }
                }
                string bulletDir = Path.Combine(root, "bullet");
                if (Directory.Exists(bulletDir))
                {
                    foreach (var f in Directory.GetFiles(bulletDir, "bullet*.swf"))
                    {
                        int id;
                        string n = Path.GetFileNameWithoutExtension(f);
                        if (n.Length > 6 && TryParseInt(n.Substring(6), out id)) set.Add(id);
                    }
                }
            }
            catch { }
            return set;
        }

        // ============================================================ probe + cache

        private static string EffectiveCachePath()
        {
            return string.IsNullOrWhiteSpace(ProbeCachePath)
                ? Path.Combine(Paths.BaseDir, "cache", "probe-cache.json")
                : ProbeCachePath;
        }

        private void LoadProbeCache()
        {
            if (!UseProbeCache) return;
            try
            {
                string path = EffectiveCachePath();
                if (!File.Exists(path)) return;
                var file = Json.LoadFile<ProbeCacheFile>(path);
                if (file == null || file.Entries == null) return;
                lock (_cacheLock)
                {
                    foreach (var e in file.Entries)
                    {
                        if (e == null || string.IsNullOrEmpty(e.Path) || e.Status <= 0) continue;
                        _cache[e.Path] = e;
                    }
                }
                _log.Info("Probe cache: nap " + file.Entries.Count + " muc tu " + path);
            }
            catch (Exception ex)
            {
                _log.Warn("Khong nap duoc probe cache: " + ex.Message);
            }
        }

        /// <summary>Ghi cache probe xuong dia (atomic, chi khi co thay doi).</summary>
        public void SaveProbeCache()
        {
            if (!UseProbeCache) return;
            lock (_cacheLock)
            {
                if (!_cacheDirty) return;
                try
                {
                    var file = new ProbeCacheFile { GeneratedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") };
                    file.Entries = _cache.Values.ToList();
                    Json.SaveFile(EffectiveCachePath(), file);
                    _cacheDirty = false;
                }
                catch (Exception ex)
                {
                    _log.Warn("Khong ghi duoc probe cache: " + ex.Message);
                }
            }
        }

        private ProbeRecord Probe(AppSettings s, string relPath)
        {
            if (UseProbeCache)
            {
                lock (_cacheLock)
                {
                    ProbeRecord cached;
                    if (_cache.TryGetValue(relPath, out cached) && cached.Status > 0) return cached;
                }
            }
            ProbeRecord found = null;
            ProbeRecord lastMiss = null;
            foreach (var baseUrl in Bases(s))
            {
                int st = HeadKeepAlive(HttpUtil.Combine(baseUrl, relPath));
                if (st == 200)
                {
                    found = new ProbeRecord { Path = relPath, Status = 200, Base = baseUrl };
                    break;
                }
                if (st > 0 && st != 200 && lastMiss == null)
                {
                    lastMiss = new ProbeRecord { Path = relPath, Status = st, Base = baseUrl };
                }
            }
            var rec = found ?? lastMiss ?? new ProbeRecord { Path = relPath, Status = 0, Base = null };
            if (rec.Status > 0)
            {
                lock (_cacheLock)
                {
                    _cache[relPath] = rec;
                    _cacheDirty = true;
                }
            }
            return rec;
        }

        private bool ProbeOk(AppSettings s, string relPath)
        {
            var rec = Probe(s, relPath);
            return rec != null && rec.Status == 200;
        }

        // Probe HEAD rieng voi KeepAlive=true de tai su dung ket noi TLS.
        // (HttpUtil.Head dat KeepAlive=false => 6000+ probe se cham hon nhieu lan;
        //  o day chi can status 200/404 nen giu request HEAD toi gian, timeout 12s.)
        private static int HeadKeepAlive(string url)
        {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "HEAD";
                req.Timeout = ProbeTimeoutMs;
                req.ReadWriteTimeout = ProbeTimeoutMs;
                req.AllowAutoRedirect = true;
                req.KeepAlive = true;
                req.UserAgent = "GunnyRequestStudio/1.0";
                req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                using (var resp = (HttpWebResponse)req.GetResponse())
                {
                    return (int)resp.StatusCode;
                }
            }
            catch (WebException wex)
            {
                var http = wex.Response as HttpWebResponse;
                if (http != null)
                {
                    int code = (int)http.StatusCode;
                    try { http.Close(); } catch { }
                    return code;
                }
                return 0;
            }
            catch
            {
                return 0;
            }
        }

        // ============================================================ download

        private bool TryDownload(AppSettings s, string relPath, string type, string preferredBase, out byte[] data, out string error)
        {
            data = null;
            error = null;
            var bases = Bases(s);
            var order = new List<string>();
            if (!string.IsNullOrWhiteSpace(preferredBase))
            {
                string pb = preferredBase.Trim();
                if (!pb.EndsWith("/")) pb += "/";
                order.Add(pb);
            }
            foreach (var b in bases) if (!order.Contains(b)) order.Add(b);

            foreach (var baseUrl in order)
            {
                string url = HttpUtil.Combine(baseUrl, relPath);
                var res = HttpUtil.Get(url, DownloadTimeoutMs, 1);
                if (res == null || !res.IsOk) { if (res != null) error = "HTTP " + res.Status + (string.IsNullOrEmpty(res.Error) ? "" : " " + res.Error); continue; }
                if (!ValidateAsset(type, res.Bytes))
                {
                    error = "du lieu khong dung dinh dang " + type + " (" + (res.Bytes == null ? 0 : res.Bytes.Length) + " byte)";
                    continue;
                }
                data = res.Bytes;
                error = null;
                return true;
            }
            if (error == null) error = "khong tai duoc tu CDN";
            return false;
        }

        /// <summary>Kiem tra magic bytes: PNG (89 50 4E 47) hoac SWF (FWS/CWS/ZWS).</summary>
        public static bool ValidateAsset(string type, byte[] data)
        {
            if (data == null || data.Length < 8) return false;
            if (string.Equals(type, TypeCrater, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(type, TypeBrink, StringComparison.OrdinalIgnoreCase))
            {
                return data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47;
            }
            return (data[0] == 'F' || data[0] == 'C' || data[0] == 'Z') && data[1] == 'W' && data[2] == 'S';
        }

        // ============================================================ helpers

        private static bool SafeExists(string path)
        {
            try { return !string.IsNullOrEmpty(path) && File.Exists(path); }
            catch { return false; }
        }

        private static string RowValue(Dictionary<string, string> row, string key)
        {
            if (row == null || string.IsNullOrEmpty(key)) return null;
            string v;
            if (row.TryGetValue(key, out v)) return v;
            return null;
        }

        private static bool TryParseInt(string s, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(s)) return false;
            return int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        private static string AppendNote(string note, string extra)
        {
            if (string.IsNullOrEmpty(extra)) return note;
            if (string.IsNullOrEmpty(note)) return extra;
            return note + "; " + extra;
        }

        private static void SplitDocs(List<XmlDocInfo> docs, out XmlDocInfo ballList, out XmlDocInfo bombConfig)
        {
            ballList = null;
            bombConfig = null;
            foreach (var doc in docs)
            {
                if (doc == null) continue;
                string name = XmlCodec.NormalizeName(doc.Name);
                bool hasCrater = HasAttribute(doc, "Crater");
                bool hasCommon = HasAttribute(doc, "Common") && HasAttribute(doc, "TemplateID");
                bool isBallList = name.Contains("balllist") || (ballList == null && hasCrater);
                bool isBombConfig = name.Contains("bombconfig") || (bombConfig == null && hasCommon);
                if (isBallList && ballList == null) ballList = doc;
                if (isBombConfig && bombConfig == null) bombConfig = doc;
            }
        }

        private static bool HasAttribute(XmlDocInfo doc, string attr)
        {
            if (doc == null) return false;
            if (doc.Attributes != null)
            {
                foreach (var a in doc.Attributes)
                {
                    if (string.Equals(a, attr, StringComparison.OrdinalIgnoreCase)) return true;
                }
            }
            if (doc.Rows != null)
            {
                foreach (var row in doc.Rows)
                {
                    if (row != null && row.ContainsKey(attr)) return true;
                }
            }
            return false;
        }
    }

    // ============================================================ extension nho cho stats

    internal static class BombStatsExtensions
    {
        public static void AddDownloaded(this BombRipStats stats, string type)
        {
            lock (stats) { stats.Downloaded++; BombStatsExtensions.Bump(stats.DownloadedByType, type); }
        }

        public static void AddSkipped(this BombRipStats stats, string type)
        {
            lock (stats) { stats.Skipped++; BombStatsExtensions.Bump(stats.SkippedByType, type); }
        }

        public static void AddUnavailable(this BombRipStats stats, string type)
        {
            lock (stats) { stats.Unavailable++; BombStatsExtensions.Bump(stats.UnavailableByType, type); }
        }

        public static void AddFailed(this BombRipStats stats, string type)
        {
            lock (stats) { stats.Failed++; BombStatsExtensions.Bump(stats.FailedByType, type); }
        }

        public static void TypeCounts(this BombRipStats stats, string type, out int downloaded, out int skipped, out int unavailable, out int failed)
        {
            lock (stats)
            {
                downloaded = Get(stats.DownloadedByType, type);
                skipped = Get(stats.SkippedByType, type);
                unavailable = Get(stats.UnavailableByType, type);
                failed = Get(stats.FailedByType, type);
            }
        }

        private static int Get(Dictionary<string, int> dict, string key)
        {
            int v;
            return dict.TryGetValue(key, out v) ? v : 0;
        }

        internal static void Bump(Dictionary<string, int> dict, string key)
        {
            int v = Get(dict, key);
            dict[key] = v + 1;
        }
    }
}
