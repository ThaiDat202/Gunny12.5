using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.IO;
using System.Linq;

namespace GunnyRequestStudio.Core
{
    public class ImportOptions
    {
        public bool Backup = true;
        public bool DryRun = false;
        public bool Force = false;
        public int BatchSize = 2000;
        public bool StopOnError = false;
        public List<string> OnlyXmls = new List<string>();   // rong = tat ca
        public List<string> OnlyTables = new List<string>();

        // ---- an toan du lieu server (chong loi Fight.exe / Road.exe sau khi import) ----
        /// <summary>Giu cac cot server-local (PosX, HasTunnel, NPC.X/Y...) theo PK khi ghi de.</summary>
        public bool PreserveLocalColumns = true;
        /// <summary>Gia tri thieu/NULL -> default (string='', so=0, bit=false) thay vi NULL (tranh NullReference/Cast).</summary>
        public bool FillMissingDefaults = true;
        /// <summary>Cho phep import bang co cot server-local nhung khong co PK map duoc (mac dinh: tu choi).</summary>
        public bool AllowLocalColumnLoss = false;
        /// <summary>Bang publisher KHONG tuong thich server local -> bo qua hoan toan.</summary>
        public List<string> SkipTables = new List<string>(DefaultProtectedTables);
        /// <summary>Bang co cot server-local nhung da xac nhan gia tri vo hai (toan default) -> cho phep ghi de.</summary>
        public List<string> AllowLocalColumnLossTables = new List<string>(DefaultLossAllowedTables);

        public static readonly string[] DefaultLossAllowedTables =
        {
            "Shop_Goods_Box",              // loadboxtemp: cac cot IsSelect/Random/IsLogs/addDate deu default
            "TS_StoryCopySectionTemplate"  // SweepReward = 0 toan bo
        };

        /// <summary>Cac bang da tung lam Fight.exe/Road.exe loi khi import publisher (giu nguyen du lieu goc).</summary>
        public static readonly string[] DefaultProtectedTables =
        {
            "CampWarItems",                        // ProduceBussiness.GetCampWarItems: cast bool/int khong hop le
            "DevilTreasRankRewardList",            // EF Percent float -> Single -> DevilTurnMgr.Init false
            "Every_Day_Active_Reward_Template",    // RewardItemID int trong DB nhung code cast string
            "Communal_Active",                     // NULL AddPropertyByMoney -> InvalidCastException
            "Communal_Active_Award",
            "Communal_Active_Exp"
        };
    }

    /// <summary>
    /// Doc schema Db_Tank_All va import du lieu request XML vao cac bang template.
    /// Quy trinh 1 bang: kiem tra -> so hash (bo qua neu khong doi) -> backup -> DELETE -> SqlBulkCopy.
    /// </summary>
    public class DbImporter
    {
        private readonly string _connectionString;
        private readonly Logger _log;
        private DbSchema _schema;
        private readonly Dictionary<string, ImportHash> _manifest = new Dictionary<string, ImportHash>(StringComparer.OrdinalIgnoreCase);
        private readonly string _manifestPath;
        private readonly object _manifestLock = new object();
        private readonly FillMap _fills;

        public DbImporter(string connectionString, Logger log, string reportsDir, FillMap fills = null)
        {
            _fills = fills ?? new FillMap();
            _connectionString = connectionString;
            _log = log ?? new Logger();
            _manifestPath = Path.Combine(reportsDir ?? ".", "import-manifest.json");
            LoadManifest();
        }

        public string ManifestPath { get { return _manifestPath; } }

        // ------------------------------------------------------------ schema

        public DbSchema LoadSchema()
        {
            var schema = new DbSchema();
            using (var conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                var sql = @"
SELECT t.name AS TableName, c.name AS ColumnName, ty.name AS DataType, c.max_length AS MaxLength,
       c.is_nullable AS IsNullable, c.is_identity AS IsIdentity, c.is_computed AS IsComputed,
       CASE WHEN c.default_object_id <> 0 THEN 1 ELSE 0 END AS HasDefault
FROM sys.tables t
JOIN sys.columns c ON c.object_id = t.object_id
JOIN sys.types ty ON ty.user_type_id = c.user_type_id
WHERE t.is_ms_shipped = 0
ORDER BY t.name, c.column_id";
                using (var cmd = new SqlCommand(sql, conn))
                using (var rd = cmd.ExecuteReader())
                {
                    DbTable current = null;
                    while (rd.Read())
                    {
                        var tname = rd.GetString(0);
                        if (current == null || current.Name != tname)
                        {
                            current = new DbTable { Name = tname };
                            schema.Tables.Add(current);
                        }
                        current.Columns.Add(new DbColumn
                        {
                            Name = rd.GetString(1),
                            DataType = rd.GetString(2),
                            MaxLength = rd.GetInt16(3),
                            IsNullable = rd.GetBoolean(4),
                            IsIdentity = rd.GetBoolean(5),
                            IsComputed = rd.GetBoolean(6),
                            HasDefault = rd.GetInt32(7) == 1
                        });
                    }
                }
                // primary key columns
                using (var cmd = new SqlCommand(@"
SELECT t.name AS TableName, c.name AS ColumnName
FROM sys.indexes i
JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
JOIN sys.tables t ON t.object_id = i.object_id
WHERE i.is_primary_key = 1 AND t.is_ms_shipped = 0
ORDER BY t.name, ic.key_ordinal", conn))
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        var t = schema.FindTable(rd.GetString(0));
                        if (t != null) t.PrimaryKey.Add(rd.GetString(1));
                    }
                }

                // row counts
                using (var cmd = new SqlCommand(@"
SELECT t.name, SUM(p.rows) AS Rows
FROM sys.tables t
JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0,1)
WHERE t.is_ms_shipped = 0
GROUP BY t.name", conn))
                using (var rd = cmd.ExecuteReader())
                {
                    while (rd.Read())
                    {
                        var t = schema.FindTable(rd.GetString(0));
                        if (t != null) t.RowCount = Convert.ToInt64(rd.GetValue(1));
                    }
                }
            }
            _schema = schema;
            return schema;
        }

        public DbSchema Schema
        {
            get
            {
                if (_schema == null) LoadSchema();
                return _schema;
            }
        }

        // ------------------------------------------------------------ manifest

        private void LoadManifest()
        {
            try
            {
                var m = Json.LoadFile<ImportManifest>(_manifestPath);
                if (m == null || m.Entries == null) return;
                foreach (var e in m.Entries)
                {
                    if (e != null && !string.IsNullOrEmpty(e.XmlName)) _manifest[e.XmlName] = e;
                }
            }
            catch { }
        }

        private void SaveManifest()
        {
            try
            {
                var m = new ImportManifest { GeneratedAt = DateTime.Now.ToString("s") };
                lock (_manifestLock)
                {
                    m.Entries = _manifest.Values.OrderBy(x => x.XmlName).ToList();
                }
                Json.SaveFile(_manifestPath, m);
            }
            catch (Exception ex) { _log.Warn("Khong ghi duoc manifest: " + ex.Message); }
        }

        // ------------------------------------------------------------ import

        public List<ImportResult> ImportAll(IEnumerable<XmlDocInfo> docs, TableMapper mapper, ImportOptions opt)
        {
            var results = new List<ImportResult>();
            // sap xep: template chat luong cao truoc (exact/override -> auto -> warn), nhieu dong hon
            var planned = new List<Tuple<XmlDocInfo, XmlTableMapping>>();
            foreach (var doc in docs)
            {
                if (opt.OnlyXmls.Count > 0 && !opt.OnlyXmls.Any(x => string.Equals(x, doc.Name, StringComparison.OrdinalIgnoreCase))) continue;
                var map = mapper.Resolve(doc);
                if (!IsImportable(map)) continue;
                if (opt.OnlyTables.Count > 0 && !opt.OnlyTables.Any(x => string.Equals(x, map.TableName, StringComparison.OrdinalIgnoreCase))) continue;
                planned.Add(Tuple.Create(doc, map));
            }
            planned = planned
                .OrderBy(t => StatusPriority(t.Item2.Status))
                .ThenByDescending(t => t.Item1.ItemCount)
                .ThenBy(t => t.Item1.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var usedTables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in planned)
            {
                var doc = p.Item1;
                var map = p.Item2;
                string otherXml;
                if (!opt.OnlyTables.Any() && usedTables.TryGetValue(map.TableName, out otherXml))
                {
                    results.Add(new ImportResult
                    {
                        XmlName = doc.Name,
                        TableName = map.TableName,
                        Status = "skipped",
                        RowsRead = doc.Rows.Count,
                        Message = "bang " + map.TableName + " da duoc import boi file " + otherXml + " (bo qua de tranh ghi de)"
                    });
                    continue;
                }
                var r = ImportOne(doc, map, opt);
                results.Add(r);
                if (r.Status == "imported" || r.Status == "unchanged") usedTables[map.TableName] = doc.Name;
                if (r.Status == "failed" && opt.StopOnError) break;
            }
            return results;
        }

        private static int StatusPriority(string status)
        {
            switch ((status ?? string.Empty).ToLowerInvariant())
            {
                case "exact": return 0;
                case "override": return 1;
                case "auto": return 2;
                case "exact-warn":
                case "auto-warn": return 3;
                default: return 4;
            }
        }

        public static bool IsImportable(XmlTableMapping map)
        {
            return map != null && !string.IsNullOrEmpty(map.TableName)
                   && map.Status != "none" && map.Status != "ambiguous";
        }

        public ImportResult ImportOne(XmlDocInfo doc, XmlTableMapping map, ImportOptions opt)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            if (opt == null) opt = new ImportOptions();
            var result = new ImportResult
            {
                XmlName = doc.Name,
                TableName = map == null ? null : map.TableName,
                Status = "skipped",
                RowsRead = doc == null ? 0 : doc.Rows.Count
            };
            try
            {
                if (map == null || string.IsNullOrEmpty(map.TableName)) { result.Message = "chua map duoc bang"; return result; }
                var table = Schema.FindTable(map.TableName);
                if (table == null) { result.Status = "failed"; result.Message = "khong thay bang " + map.TableName; return result; }

                // bang nam trong danh sach bao ve server -> khong dong vao
                if (opt.SkipTables != null && opt.SkipTables.Any(x => string.Equals(x, table.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    result.Status = "skipped";
                    result.Message = "bang nam trong danh sach bao ve server (SkipTables) - giu nguyen de tranh loi Fight.exe/Road.exe";
                    return result;
                }

                // hash du lieu XML (canonical) de bo qua neu khong doi
                var xmlHash = ComputeRowsHash(doc);
                ImportHash last;
                if (!opt.Force && _manifest.TryGetValue(doc.Name, out last) && last != null
                    && string.Equals(last.Sha256, xmlHash, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(last.TableName, table.Name, StringComparison.OrdinalIgnoreCase))
                {
                    result.Status = "unchanged";
                    result.Message = "du lieu khong doi so voi lan import truoc (" + last.ImportedAt + ")";
                    return result;
                }

                // preflight: cot NOT NULL khong co trong XML nhung co the dien gia tri tu override
                var fillColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var remainMissing = new List<string>();
                if (map.MissingRequiredColumns != null && map.MissingRequiredColumns.Count > 0)
                {
                    Dictionary<string, string> tableFills = null;
                    bool hasFills = _fills != null && _fills.TryGetTable(table.Name, out tableFills);
                    foreach (var colName in map.MissingRequiredColumns)
                    {
                        string val;
                        if (hasFills && tableFills.TryGetValue(colName, out val)) { fillColumns[colName] = val; continue; }
                        // auto-ID: cot dau tien, kieu int, khong identity -> danh so 1..N
                        var col = table.FindColumn(colName);
                        bool isFirst = col != null && table.Columns.Count > 0 && string.Equals(table.Columns[0].Name, col.Name, StringComparison.OrdinalIgnoreCase);
                        var clrT = col == null ? null : ToClrType(col.DataType);
                        if (isFirst && col != null && !col.IsIdentity && (clrT == typeof(int) || clrT == typeof(long) || clrT == typeof(short)))
                        {
                            fillColumns[colName] = "__AUTOID__";
                            continue;
                        }
                        remainMissing.Add(colName);
                    }
                    if (remainMissing.Count > 0)
                    {
                        result.Status = "failed";
                        result.Message = "thieu cot NOT NULL khong co default: " + string.Join(", ", remainMissing.ToArray()) + " (them vao Fills trong table-map.overrides.json neu muon dien)";
                        return result;
                    }
                }
                if (doc.Rows.Count == 0)
                {
                    result.Status = "skipped";
                    result.Message = "file rong (0 dong)";
                    return result;
                }

                var colMap = new List<KeyValuePair<DbColumn, string>>(); // dest column <- xml attribute
                var colMaxChars = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                int truncated = 0;
                var dt = new DataTable();
                var attrOrder = doc.Attributes;
                foreach (var attr in attrOrder)
                {
                    var col = table.FindColumnFuzzy(attr);
                    if (col == null || col.IsComputed) continue;
                    var clr = ToClrType(col.DataType);
                    if (clr == null)
                    {
                        _log.Warn(doc.Name + ": bo qua cot " + col.Name + " (kieu khong ho tro: " + col.DataType + ")");
                        continue;
                    }
                    dt.Columns.Add(col.Name, clr);
                    int limit = StringLimit(col);
                    if (limit > 0) colMaxChars[col.Name] = limit;
                    colMap.Add(new KeyValuePair<DbColumn, string>(col, attr));
                }
                // cot dien gia tri co dinh (fill) cho NOT NULL khong co trong XML
                foreach (var fc in fillColumns)
                {
                    var col = table.FindColumn(fc.Key);
                    if (col == null) continue;
                    var clr = ToClrType(col.DataType);
                    if (clr == null) continue;
                    if (dt.Columns.Contains(col.Name)) continue;
                    dt.Columns.Add(col.Name, clr);
                    colMap.Add(new KeyValuePair<DbColumn, string>(col, null));
                }
                if (dt.Columns.Count == 0)
                {
                    result.Status = "failed";
                    result.Message = "khong co cot nao khop giua XML va bang " + table.Name;
                    return result;
                }

                int autoId = 0;
                var warnings = new List<string>();
                var preservedCols = new List<string>();
                var overflowNotes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                var overflowSamples = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var row in doc.Rows)
                {
                    autoId++;
                    var dr = dt.NewRow();
                    foreach (var kv in colMap)
                    {
                        if (kv.Value == null)
                        {
                            // cot fill
                            string fillRaw;
                            fillColumns.TryGetValue(kv.Key.Name, out fillRaw);
                            if (fillRaw == "__AUTOID__")
                            {
                                var t = dt.Columns[kv.Key.Name].DataType;
                                dr[kv.Key.Name] = t == typeof(long) ? (object)(long)autoId : t == typeof(short) ? (object)(short)autoId : (object)autoId;
                                continue;
                            }
                            object fillVal;
                            dr[kv.Key.Name] = TryConvert(fillRaw ?? string.Empty, dt.Columns[kv.Key.Name].DataType, out fillVal)
                                ? fillVal
                                : (kv.Key.IsNullable ? (object)DBNull.Value : GetClrDefault(dt.Columns[kv.Key.Name].DataType));
                            continue;
                        }
                        string raw;
                        if (!row.TryGetValue(kv.Value, out raw) || raw == null)
                        {
                            var c = kv.Key;
                            dr[c.Name] = DefaultValueFor(c, dt.Columns[c.Name].DataType, opt.FillMissingDefaults);
                            continue;
                        }
                        int lim;
                        if (colMaxChars.TryGetValue(kv.Key.Name, out lim) && raw != null && raw.Length > lim)
                        {
                            raw = raw.Substring(0, lim);
                            truncated++;
                        }
                        object val;
                        string convertIssue;
                        if (!TryConvert(raw, dt.Columns[kv.Key.Name].DataType, out val, out convertIssue))
                        {
                            dr[kv.Key.Name] = DefaultValueFor(kv.Key, dt.Columns[kv.Key.Name].DataType, opt.FillMissingDefaults);
                        }
                        else
                        {
                            dr[kv.Key.Name] = val;
                            if (!string.IsNullOrEmpty(convertIssue))
                            {
                                int n;
                                overflowNotes.TryGetValue(kv.Key.Name, out n);
                                overflowNotes[kv.Key.Name] = n + 1;
                                if (!overflowSamples.ContainsKey(kv.Key.Name))
                                    overflowSamples[kv.Key.Name] = raw.Length > 40 ? raw.Substring(0, 40) + "..." : raw;
                            }
                        }
                    }
                    dt.Rows.Add(dr);
                }

                if (truncated > 0) _log.Warn(doc.Name + ": cat bot " + truncated + " gia tri vuot do dai cot nvarchar/varchar");

                // canh bao kieu du lieu: publisher doi int -> long (fightpower...) hoac gia tri vuot pham vi
                foreach (var kv in overflowNotes)
                {
                    var oc = table.FindColumn(kv.Key);
                    string ctype = oc == null ? "?" : oc.DataType;
                    string sample = overflowSamples.ContainsKey(kv.Key) ? overflowSamples[kv.Key] : "";
                    warnings.Add("cot " + kv.Key + " (" + ctype + "): " + kv.Value + " gia tri vuot pham vi, da clamp (vi du " + sample
                                 + "). Publisher co the da doi kieu int->long/bigint - can nhac ALTER COLUMN sang kieu lon hon.");
                }

                // loai bo dong trung khoa chinh (publisher co the gui trung)
                if (table.PrimaryKey != null && table.PrimaryKey.Count > 0
                    && table.PrimaryKey.All(pk => dt.Columns.Contains(pk)))
                {
                    var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var dup = new List<DataRow>();
                    foreach (DataRow dr in dt.Rows)
                    {
                        var parts = new List<string>(table.PrimaryKey.Count);
                        foreach (var pk in table.PrimaryKey) parts.Add(Convert.ToString(dr[pk], CultureInfo.InvariantCulture) ?? string.Empty);
                        var key = string.Join("\u0001", parts.ToArray());
                        if (!seen.Add(key)) dup.Add(dr);
                    }
                    for (int i = dup.Count - 1; i >= 0; i--) dt.Rows.Remove(dup[i]);
                    if (dup.Count > 0) _log.Warn(doc.Name + ": bo " + dup.Count + " dong trung khoa chinh (" + string.Join(",", table.PrimaryKey.ToArray()) + ")");
                }

                // ---- giu cac cot server-local (cot server dung ma publisher XML khong gui) ----
                // Cot XML map -> lay tu XML. Cot chi-co-trong-Fills -> uu tien giu du lieu cu (neu khop PK), dong moi lay Fill.
                var xmlMappedNames = new HashSet<string>(colMap.Where(kv => kv.Value != null).Select(kv => kv.Key.Name), StringComparer.OrdinalIgnoreCase);
                var fillOnlyNames = new HashSet<string>(colMap.Where(kv => kv.Value == null).Select(kv => kv.Key.Name), StringComparer.OrdinalIgnoreCase);
                var fillOnlyCols = table.Columns.Where(c => fillOnlyNames.Contains(c.Name)).ToList();
                var extraCols = table.Columns.Where(c => !xmlMappedNames.Contains(c.Name) && !fillOnlyNames.Contains(c.Name)
                                                          && !c.IsIdentity && !c.IsComputed && ToClrType(c.DataType) != null).ToList();
                var preserveCols = new List<DbColumn>();
                preserveCols.AddRange(extraCols);
                preserveCols.AddRange(fillOnlyCols);
                if (preserveCols.Count > 0)
                {
                    var pks = table.PrimaryKey ?? new List<string>();
                    bool pkMapped = pks.Count > 0 && pks.All(pk => dt.Columns.Contains(pk));
                    int rowsBeforeNow = CountRows(table);
                    bool allowLoss = opt.AllowLocalColumnLoss
                                     || (opt.AllowLocalColumnLossTables != null && opt.AllowLocalColumnLossTables.Any(x => string.Equals(x, table.Name, StringComparison.OrdinalIgnoreCase)));
                    if (opt.PreserveLocalColumns && pkMapped)
                    {
                        string loadErr;
                        var oldValues = LoadLocalValues(table, pks, preserveCols, out loadErr);
                        if (loadErr != null) warnings.Add("khong doc duoc du lieu cu de giu cot server-local: " + loadErr);
                        foreach (var lc in extraCols) dt.Columns.Add(lc.Name, ToClrType(lc.DataType));
                        var keyOf = new Func<DataRow, string>(dr =>
                        {
                            var parts = new List<string>(pks.Count);
                            foreach (var pk in pks) parts.Add(Convert.ToString(dr[pk], CultureInfo.InvariantCulture) ?? string.Empty);
                            return string.Join("\u0001", parts.ToArray());
                        });
                        int matched = 0;
                        foreach (DataRow dr in dt.Rows)
                        {
                            object[] vals;
                            if (oldValues.TryGetValue(keyOf(dr), out vals))
                            {
                                matched++;
                                for (int ci = 0; ci < preserveCols.Count; ci++)
                                    dr[preserveCols[ci].Name] = vals[ci] ?? DBNull.Value;
                            }
                            else
                            {
                                // dong moi: cot extra lay default, cot Fill giu gia tri Fill da dien
                                for (int ci = 0; ci < extraCols.Count; ci++)
                                    dr[extraCols[ci].Name] = DefaultValueFor(extraCols[ci], dt.Columns[extraCols[ci].Name].DataType, opt.FillMissingDefaults);
                            }
                        }
                        preservedCols = preserveCols.Select(c => c.Name).ToList();
                        if (matched > 0)
                            warnings.Add("giu " + preserveCols.Count + " cot server-local (" + string.Join(",", preservedCols.Take(10).ToArray()) + (preservedCols.Count > 10 ? ",..." : "") + ") cho " + matched + "/" + dt.Rows.Count + " dong tu bang cu");
                        else
                            warnings.Add("bang co " + preserveCols.Count + " cot server-local nhung khong khop khoa dong nao voi du lieu cu");
                    }
                    else if (extraCols.Count > 0 && !allowLoss && rowsBeforeNow > 0)
                    {
                        result.Status = "failed";
                        result.Message = "bang co " + extraCols.Count + " cot server-local (" + string.Join(",", extraCols.Select(c => c.Name).Take(8).ToArray())
                            + (extraCols.Count > 8 ? ",..." : "") + ") ma khong co PK map tu XML -> tu choi import de bao ve server. "
                            + "Them bang vao SkipTables / AllowLocalColumnLossTables (neu da xac nhan vo hai) hoac bat AllowLocalColumnLoss.";
                        return result;
                    }
                    else
                    {
                        if (allowLoss)
                            warnings.Add("cho phep ghi de " + extraCols.Count + " cot server-local (bang nam trong AllowLocalColumnLossTables) - da xac nhan gia tri vo hai");
                        else
                            warnings.Add("KHONG giu duoc " + extraCols.Count + " cot server-local (khong co PK map) - co the lam hong du lieu server");
                    }
                }

                result.RowsWritten = dt.Rows.Count;
                result.LocalColumnsPreserved = preservedCols.Count;
                result.Warnings = warnings.Count == 0 ? null : string.Join("\n", warnings.ToArray());
                if (opt.DryRun)
                {
                    result.Status = "skipped";
                    result.Message = "DRY-RUN: se ghi " + dt.Rows.Count + " dong vao " + table.Name;
                    return result;
                }

                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var tx = conn.BeginTransaction())
                    {
                        try
                        {
                            // so dong hien tai
                            using (var cmd = new SqlCommand("SELECT COUNT_BIG(*) FROM " + Q(table.Schema) + "." + Q(table.Name), conn, tx))
                            {
                                result.RowsBefore = Convert.ToInt32(cmd.ExecuteScalar());
                            }

                            // backup
                            if (opt.Backup && result.RowsBefore > 0)
                            {
                                var suffix = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                                var backupName = MakeBackupName(table.Name, suffix);
                                var sqlBak = "SELECT * INTO " + Q(table.Schema) + "." + Q(backupName) + " FROM " + Q(table.Schema) + "." + Q(table.Name);
                                using (var cmd = new SqlCommand(sqlBak, conn, tx)) { cmd.CommandTimeout = 600; cmd.ExecuteNonQuery(); }
                                result.BackupTable = backupName;
                            }

                            // xoa du lieu cu
                            using (var cmd = new SqlCommand("DELETE FROM " + Q(table.Schema) + "." + Q(table.Name), conn, tx))
                            {
                                cmd.CommandTimeout = 600;
                                cmd.ExecuteNonQuery();
                            }

                            // bulk insert
                            var bulkOptions = SqlBulkCopyOptions.Default;
                            bool identityInXml = colMap.Any(kv => kv.Key.IsIdentity);
                            if (identityInXml) bulkOptions |= SqlBulkCopyOptions.KeepIdentity;
                            using (var bulk = new SqlBulkCopy(conn, bulkOptions, tx))
                            {
                                bulk.DestinationTableName = Q(table.Schema) + "." + Q(table.Name);
                                bulk.BatchSize = opt.BatchSize;
                                bulk.BulkCopyTimeout = 600;
                                foreach (DataColumn dc in dt.Columns)
                                {
                                    bulk.ColumnMappings.Add(dc.ColumnName, dc.ColumnName);
                                }
                                bulk.WriteToServer(dt);
                            }

                            tx.Commit();
                            result.Status = "imported";
                            result.Message = "OK " + dt.Rows.Count + " dong"
                                + (preservedCols.Count > 0 ? " · giu " + preservedCols.Count + " cot server-local" : "")
                                + (result.BackupTable != null ? " (backup " + result.BackupTable + ")" : "");
                        }
                        catch (Exception ex)
                        {
                            try { tx.Rollback(); } catch { }
                            result.Status = "failed";
                            var debug = string.Join("; ", colMap.Select(kv => kv.Value == null
                                ? kv.Key.Name + "=FILL->" + dt.Columns[kv.Key.Name].DataType.Name
                                : kv.Value + "->" + kv.Key.Name + "(" + dt.Columns[kv.Key.Name].DataType.Name + ")").ToArray());
                            result.Message = ex.Message + " | mappings: " + debug;
                        }
                    }
                }

                if (result.Status == "imported")
                {
                    lock (_manifestLock)
                    {
                        _manifest[doc.Name] = new ImportHash
                        {
                            XmlName = doc.Name,
                            TableName = table.Name,
                            Sha256 = xmlHash,
                            Rows = dt.Rows.Count,
                            ImportedAt = DateTime.Now.ToString("s")
                        };
                    }
                    SaveManifest();
                }
                return result;
            }
            catch (Exception ex)
            {
                result.Status = "failed";
                result.Message = ex.Message;
                return result;
            }
            finally
            {
                sw.Stop();
                result.ElapsedMs = sw.ElapsedMilliseconds;
            }
        }

        /// <summary>Import 1 file XML tu duong dan (dung cho CLI/tool).</summary>
        public ImportResult ImportFile(string xmlPath, TableMapper mapper, ImportOptions opt)
        {
            var doc = XmlCodec.Inspect(xmlPath);
            doc.Name = Path.GetFileNameWithoutExtension(xmlPath);
            var map = mapper.Resolve(doc);
            return ImportOne(doc, map, opt);
        }

        // ------------------------------------------------------------ helpers

        public static string ComputeRowsHash(XmlDocInfo doc)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(doc.Name).Append('|').Append(doc.ItemElement ?? "?").Append('|').Append(doc.Rows.Count).Append('\n');
            foreach (var r in doc.Rows)
            {
                foreach (var a in doc.Attributes)
                {
                    string v;
                    if (r.TryGetValue(a, out v)) sb.Append(a).Append('=').Append(v).Append(';');
                }
                sb.Append('\n');
            }
            return Hash.Sha256String(sb.ToString());
        }

        private static int StringLimit(DbColumn col)
        {
            var t = (col.DataType ?? string.Empty).ToLowerInvariant();
            if (col.MaxLength <= 0) return -1;                       // nvarchar(max)
            if (t == "nvarchar" || t == "nchar") return col.MaxLength / 2;
            if (t == "varchar" || t == "char") return col.MaxLength;
            return -1;
        }

        private static string MakeBackupName(string table, string suffix)
        {
            var name = table + "_bak_" + suffix;
            if (name.Length > 120) name = table.Substring(0, Math.Max(1, 120 - 5 - suffix.Length)) + "_bak_" + suffix;
            return name;
        }

        private static string Q(string identifier)
        {
            return "[" + (identifier ?? string.Empty).Replace("]", "]]") + "]";
        }

        public static Type ToClrType(string sqlType)
        {
            switch ((sqlType ?? string.Empty).ToLowerInvariant())
            {
                case "int": return typeof(int);
                case "smallint": return typeof(short);
                case "tinyint": return typeof(byte);
                case "bigint": return typeof(long);
                case "bit": return typeof(bool);
                case "float": return typeof(double);
                case "real": return typeof(float);
                case "decimal": case "numeric": case "money": case "smallmoney": return typeof(decimal);
                case "nvarchar": case "varchar": case "nchar": case "char": case "text": case "ntext": return typeof(string);
                case "datetime": case "datetime2": case "smalldatetime": case "date": return typeof(DateTime);
                case "time": return typeof(TimeSpan);
                case "uniqueidentifier": return typeof(Guid);
                case "varbinary": case "binary": case "image": return typeof(byte[]);
                default: return null;
            }
        }

        private static object GetClrDefault(Type t)
        {
            if (t == typeof(string)) return string.Empty;
            if (t == typeof(byte[])) return new byte[0];
            if (t == typeof(DateTime)) return new DateTime(1970, 1, 1);
            if (t == typeof(TimeSpan)) return TimeSpan.Zero;
            if (t == typeof(Guid)) return Guid.Empty;
            return Activator.CreateInstance(t);
        }

        // ------------------------------------------------------------ helpers (an toan server)

        private static object DefaultValueFor(DbColumn col, Type clr, bool fillDefault)
        {
            if (!fillDefault && col != null && col.IsNullable) return DBNull.Value;
            return GetClrDefault(clr);
        }

        private int CountRows(DbTable table)
        {
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand("SELECT COUNT_BIG(*) FROM " + Q(table.Schema) + "." + Q(table.Name), conn))
                        return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
            catch { return 0; }
        }

        private Dictionary<string, object[]> LoadLocalValues(DbTable table, List<string> pk, List<DbColumn> localCols, out string error)
        {
            var result = new Dictionary<string, object[]>(StringComparer.OrdinalIgnoreCase);
            error = null;
            try
            {
                var cols = new List<string>();
                cols.AddRange(pk);
                cols.AddRange(localCols.Select(c => c.Name));
                var sql = "SELECT " + string.Join(", ", cols.Select(c => Q(c)).ToArray()) + " FROM " + Q(table.Schema) + "." + Q(table.Name);
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var cmd = new SqlCommand(sql, conn))
                    {
                        cmd.CommandTimeout = 300;
                        using (var rd = cmd.ExecuteReader())
                        {
                            while (rd.Read())
                            {
                                var keyParts = new List<string>(pk.Count);
                                for (int i = 0; i < pk.Count; i++)
                                    keyParts.Add(Convert.ToString(rd.GetValue(i), CultureInfo.InvariantCulture) ?? string.Empty);
                                var vals = new object[localCols.Count];
                                for (int i = 0; i < localCols.Count; i++)
                                {
                                    int ci = pk.Count + i;
                                    vals[i] = rd.IsDBNull(ci) ? null : rd.GetValue(ci);
                                }
                                result[string.Join("\u0001", keyParts.ToArray())] = vals;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { error = ex.Message; }
            return result;
        }

        /// <summary>Kiem tra nhanh du lieu ma Fight.exe/Road.exe bat buoc phai co sau khi import.</summary>
        public List<string> CheckServerSafety()
        {
            var warns = new List<string>();
            try
            {
                using (var conn = new SqlConnection(_connectionString))
                {
                    conn.Open();
                    Func<string, object> scalar = sql =>
                    {
                        using (var cmd = new SqlCommand(sql, conn)) { cmd.CommandTimeout = 120; return cmd.ExecuteScalar(); }
                    };
                    Func<string, bool> tableExists = t => Convert.ToInt32(scalar("SELECT CASE WHEN OBJECT_ID('dbo." + t.Replace("'", "''") + "') IS NULL THEN 0 ELSE 1 END")) > 0;
                    if (tableExists("Game_Map"))
                    {
                        int total = Convert.ToInt32(scalar("SELECT COUNT(*) FROM dbo.Game_Map"));
                        int ok = Convert.ToInt32(scalar("SELECT COUNT(*) FROM dbo.Game_Map WHERE PosX IS NOT NULL AND PosX <> ''"));
                        if (total > 0 && ok == 0)
                            warns.Add("Game_Map: " + total + " dong nhung PosX/PosX1 deu rong -> Fight.exe MapMgr se bao 'maps:0' va khong khoi dong. Import da lam mat cot server-local PosX/PosX1.");
                        else if (total > 0 && ok < total / 10)
                            warns.Add("Game_Map: chi " + ok + "/" + total + " dong co PosX (co the thieu diem spawn).");
                    }
                    if (tableExists("Ball"))
                    {
                        int total = Convert.ToInt32(scalar("SELECT COUNT(*) FROM dbo.Ball"));
                        int tunnel = Convert.ToInt32(scalar("SELECT COUNT(*) FROM dbo.Ball WHERE HasTunnel = 1"));
                        int delay = Convert.ToInt32(scalar("SELECT COUNT(*) FROM dbo.Ball WHERE Delay = 0"));
                        if (total > 0 && tunnel == 0)
                            warns.Add("Ball: HasTunnel=1 cho 0/" + total + " dong -> Fight.exe BallMgr Init: False. Can restore cot HasTunnel.");
                        if (total > 0 && delay == total)
                            warns.Add("Ball: tat ca Delay=0 (publisher khong gui cot nay) -> can restore Delay tu backup.");
                    }
                }
            }
            catch (Exception ex) { warns.Add("khong kiem tra duoc server safety: " + ex.Message); }
            return warns;
        }

        public static bool TryConvert(string raw, Type target, out object value)
        {
            string issue;
            return TryConvert(raw, target, out value, out issue);
        }

        /// <summary>Chuyen gia tri XML sang kieu cot; phat hien tran so (int->long) thay vi am tham mat du lieu.</summary>
        public static bool TryConvert(string raw, Type target, out object value, out string issue)
        {
            value = null;
            issue = null;
            if (raw == null) return false;
            var s = raw.Trim();
            try
            {
                var inv = CultureInfo.InvariantCulture;
                if (target == typeof(string)) { value = raw; return true; }
                if (target == typeof(bool))
                {
                    if (s.Length == 0) { value = false; return true; }
                    var l = s.ToLowerInvariant();
                    if (l == "true" || l == "1") { value = true; return true; }
                    if (l == "false" || l == "0") { value = false; return true; }
                    decimal db;
                    if (decimal.TryParse(s, NumberStyles.Float, inv, out db)) { value = db != 0; return true; }
                    value = bool.Parse(s); return true;
                }
                if (s.Length == 0)
                {
                    if (target == typeof(int)) { value = 0; return true; }
                    if (target == typeof(short)) { value = (short)0; return true; }
                    if (target == typeof(byte)) { value = (byte)0; return true; }
                    if (target == typeof(long)) { value = 0L; return true; }
                    if (target == typeof(double)) { value = 0d; return true; }
                    if (target == typeof(float)) { value = 0f; return true; }
                    if (target == typeof(decimal)) { value = 0m; return true; }
                    return false;
                }
                if (target == typeof(int) || target == typeof(short) || target == typeof(byte) || target == typeof(long))
                {
                    long lv;
                    decimal dv;
                    bool isNum = long.TryParse(s, NumberStyles.Integer, inv, out lv);
                    if (!isNum && decimal.TryParse(s, NumberStyles.Float, inv, out dv))
                    {
                        decimal rounded = Math.Round(dv);
                        if (rounded > long.MaxValue) { lv = long.MaxValue; isNum = true; issue = "overflow:bigint"; }
                        else if (rounded < long.MinValue) { lv = long.MinValue; isNum = true; issue = "overflow:bigint"; }
                        else { lv = (long)rounded; isNum = true; }
                    }
                    if (!isNum)
                    {
                        var l2 = s.ToLowerInvariant();
                        if (l2 == "true") { value = Convert.ChangeType(1, target, inv); return true; }
                        if (l2 == "false") { value = Convert.ChangeType(0, target, inv); return true; }
                        return false;
                    }
                    if (target == typeof(int))
                    {
                        if (lv > int.MaxValue) { value = int.MaxValue; issue = "overflow:Int32 (publisher co the da doi int->long/bigint)"; return true; }
                        if (lv < int.MinValue) { value = int.MinValue; issue = "overflow:Int32"; return true; }
                        value = (int)lv; return true;
                    }
                    if (target == typeof(short))
                    {
                        if (lv > short.MaxValue) { value = short.MaxValue; issue = "overflow:Int16"; return true; }
                        if (lv < short.MinValue) { value = short.MinValue; issue = "overflow:Int16"; return true; }
                        value = (short)lv; return true;
                    }
                    if (target == typeof(byte))
                    {
                        if (lv > byte.MaxValue) { value = byte.MaxValue; issue = "overflow:Byte"; return true; }
                        if (lv < byte.MinValue) { value = byte.MinValue; issue = "overflow:Byte"; return true; }
                        value = (byte)lv; return true;
                    }
                    value = lv; return true;
                }
                if (target == typeof(double) || target == typeof(float) || target == typeof(decimal))
                {
                    double d;
                    if (double.TryParse(s, NumberStyles.Float, inv, out d)) { value = Convert.ChangeType(d, target, inv); return true; }
                    var l3 = s.ToLowerInvariant();
                    if (l3 == "true") { value = Convert.ChangeType(1, target, inv); return true; }
                    if (l3 == "false") { value = Convert.ChangeType(0, target, inv); return true; }
                    return false;
                }
                if (target == typeof(DateTime))
                {
                    DateTime dt;
                    if (DateTime.TryParse(s, inv, DateTimeStyles.None, out dt)) { value = dt; return true; }
                    double oa;
                    if (double.TryParse(s, NumberStyles.Float, inv, out oa)) { value = DateTime.FromOADate(oa); return true; }
                    return false;
                }
                if (target == typeof(TimeSpan)) { value = TimeSpan.Parse(s, inv); return true; }
                if (target == typeof(Guid)) { value = Guid.Parse(s); return true; }
                if (target == typeof(byte[])) { value = Convert.FromBase64String(s); return true; }
            }
            catch { return false; }
            return false;
        }
    }
}
