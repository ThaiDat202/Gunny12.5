using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GunnyRequestStudio.Core
{
    /// <summary>
    /// Trung tam trang thai cua tool: settings, logger, schema CSDL, mapper, cac service.
    /// Moi trang (PageBase) nhan context nay de dung chung.
    /// </summary>
    public class StudioContext
    {
        public AppSettings Settings { get; private set; }
        public Logger Log { get; private set; }
        public string ReportsDir { get { return Paths.ReportsDir(Settings); } }

        private DbImporter _importer;
        private TableMapper _mapper;
        private DbSchema _schema;
        private RipService _ripper;
        private string _dbStatus = "chua ket noi";
        private string _overridesPath;
        private OverridesFile _overrides;
        private List<XmlDocInfo> _docsCache;
        private string _docsCacheFolder;
        private readonly Dictionary<string, XmlTableMapping> _mapCache = new Dictionary<string, XmlTableMapping>(StringComparer.OrdinalIgnoreCase);
        private readonly object _cacheLock = new object();

        public event Action SettingsChanged;
        public event Action<string> StatusChanged;

        public StudioContext()
        {
            Settings = LoadSettings();
            Log = new Logger(Paths.LogPath());
            _overridesPath = Paths.OverridesPath();
            Log.Step("Khoi dong Gunny Request Studio");
            Log.Info("Request dir : " + Settings.RequestDir);
            Log.Info("Mirror dir  : " + Settings.MirrorDir);
            Log.Info("CSDL        : " + MaskConnection(Settings.DbConnection));
        }

        // ------------------------------------------------------------ settings

        public static AppSettings LoadSettings()
        {
            var path = Paths.SettingsPath();
            var s = Json.LoadFile<AppSettings>(path);
            if (s == null)
            {
                s = AppSettings.CreateDefault();
                try { Json.SaveFile(path, s); } catch { }
            }
            if (s.PublisherBases == null) s.PublisherBases = new List<string> { "https://quest.gn.zing.vn/" };
            if (s.ResBases == null) s.ResBases = new List<string> { "https://res.gn.zing.vn/", "https://gunny.vcdn.vn/" };
            return s;
        }

        public void SaveSettings()
        {
            try { Json.SaveFile(Paths.SettingsPath(), Settings); } catch (Exception ex) { Log.Warn("Khong luu duoc settings: " + ex.Message); }
            var h = SettingsChanged;
            if (h != null) { try { h(); } catch { } }
        }

        public void ApplySettings(AppSettings settings)
        {
            Settings = settings;
            _importer = null; _schema = null; _mapper = null; _ripper = null;
            InvalidateRequestCaches();
            SaveSettings();
            Log.Step("Da cap nhat cau hinh");
        }

        public string DbStatus { get { return _dbStatus; } }

        public void SetStatus(string text)
        {
            _dbStatus = text;
            var h = StatusChanged;
            if (h != null) { try { h(text); } catch { } }
        }

        public static string MaskConnection(string cs)
        {
            if (string.IsNullOrEmpty(cs)) return "";
            // an password khi log
            return System.Text.RegularExpressions.Regex.Replace(cs, @"(?i)(password|pwd)\s*=\s*[^;]*", "$1=***");
        }

        // ------------------------------------------------------------ schema / mapper

        public DbImporter Importer
        {
            get
            {
                if (_importer == null)
                {
                    if (_overrides == null) _overrides = OverridesFile.Load(_overridesPath);
                    _importer = new DbImporter(Settings.DbConnection, Log, ReportsDir, _overrides.Fills);
                }
                return _importer;
            }
        }

        /// <summary>File override hien tai (mapping + fills + danh sach bang can bao ve).</summary>
        public OverridesFile Overrides
        {
            get
            {
                if (_overrides == null) _overrides = OverridesFile.Load(_overridesPath);
                return _overrides;
            }
        }

        public DbSchema Schema
        {
            get
            {
                if (_schema == null) _schema = Importer.LoadSchema();
                return _schema;
            }
        }

        public TableMapper Mapper
        {
            get
            {
                if (_mapper == null)
                {
                    _overrides = OverridesFile.Load(_overridesPath);
                    _mapper = new TableMapper(Schema, _overrides.Map);
                }
                return _mapper;
            }
        }

        public RipService Ripper
        {
            get
            {
                if (_ripper == null) _ripper = new RipService(Log, Mapper);
                return _ripper;
            }
        }

        /// <summary>Test ket noi CSDL, tra ve true + thong tin phien ban.</summary>
        public bool TestDb(out string message)
        {
            try
            {
                using (var conn = new System.Data.SqlClient.SqlConnection(Settings.DbConnection))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT @@VERSION";
                        var v = Convert.ToString(cmd.ExecuteScalar());
                        var first = (v ?? "").Split('\n').FirstOrDefault();
                        message = "Ket noi OK: " + (first ?? "").Trim();
                    }
                }
                SetStatus("CSDL: OK");
                Log.Okay(message);
                return true;
            }
            catch (Exception ex)
            {
                message = "Ket noi CSDL that bai: " + ex.Message;
                SetStatus("CSDL: loi");
                Log.Error(message);
                return false;
            }
        }

        /// <summary>
        /// Danh sach XML request da parse + cache (dung chung cho Dashboard/Import/Rip/Bomb de khong parse lai).
        /// force=true de doc lai tu dia (sau khi rip/giai ma file).
        /// </summary>
        public List<XmlDocInfo> GetRequestDocs(string folder = null, bool force = false, Action<string> progress = null)
        {
            var target = string.IsNullOrEmpty(folder) ? Settings.MirrorDir : folder;
            lock (_cacheLock)
            {
                if (!force && _docsCache != null && string.Equals(_docsCacheFolder, target, StringComparison.OrdinalIgnoreCase))
                    return _docsCache;
            }
            var docs = LoadRequestDocs(target, true, progress);
            lock (_cacheLock)
            {
                _docsCache = docs;
                _docsCacheFolder = target;
                _mapCache.Clear();
            }
            return docs;
        }

        /// <summary>Map 1 file XML -> bang CSDL (cache theo ten file; rat nhanh khi quet lai).</summary>
        public XmlTableMapping MapDoc(XmlDocInfo doc, bool force = false)
        {
            if (doc == null) return null;
            var key = doc.Name ?? string.Empty;
            lock (_cacheLock)
            {
                XmlTableMapping cached;
                if (!force && _mapCache.TryGetValue(key, out cached)) return cached;
            }
            var map = Mapper.Resolve(doc);
            lock (_cacheLock)
            {
                if (!string.IsNullOrEmpty(key)) _mapCache[key] = map;
            }
            return map;
        }

        /// <summary>Xoa cache request/map (goi sau khi rip, giai ma, hoac doi cau hinh).</summary>
        public void InvalidateRequestCaches()
        {
            lock (_cacheLock)
            {
                _docsCache = null;
                _docsCacheFolder = null;
                _mapCache.Clear();
            }
        }

        /// <summary>Liet ke cac template XML trong thu muc Request mirror (da giai nen -> rows).</summary>
        public List<XmlDocInfo> LoadRequestDocs(string folder, bool preferMirror = true, Action<string> progress = null)
        {
            var docs = new List<XmlDocInfo>();
            var target = folder;
            if (string.IsNullOrEmpty(target)) target = preferMirror ? Settings.MirrorDir : Settings.RequestDir;
            if (!Directory.Exists(target)) return docs;
            var files = Directory.GetFiles(target, "*.xml", SearchOption.TopDirectoryOnly)
                .Where(f => !Path.GetFileName(f).StartsWith("_"))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
            foreach (var f in files)
            {
                try
                {
                    var doc = XmlCodec.Inspect(f);
                    doc.Name = Path.GetFileNameWithoutExtension(f);
                    docs.Add(doc);
                }
                catch { }
                var h = progress;
                if (h != null) { try { h(Path.GetFileName(f)); } catch { } }
            }
            return docs;
        }

        /// <summary>Reload schema + mapper (sau khi cau hinh CSDL doi).</summary>
        public void ReloadDb()
        {
            _schema = null; _mapper = null; _importer = null; _ripper = null;
            InvalidateRequestCaches();
            try
            {
                var schema = Schema;
                _overrides = OverridesFile.Load(_overridesPath);
                _mapper = new TableMapper(schema, _overrides.Map);
                Log.Okay("Da nap schema: " + schema.Tables.Count + " bang");
            }
            catch (Exception ex)
            {
                Log.Error("Nap schema that bai: " + ex.Message);
            }
        }

        public void SaveOverrides(Dictionary<string, string> overrides)
        {
            if (_overrides == null) _overrides = OverridesFile.Load(_overridesPath);
            _overrides.Map = new Dictionary<string, string>(overrides, StringComparer.OrdinalIgnoreCase);
            _overrides.Save();
            _mapper = null;
            _ripper = null;
            Log.Okay("Da luu " + overrides.Count + " mapping override");
        }
    }
}


