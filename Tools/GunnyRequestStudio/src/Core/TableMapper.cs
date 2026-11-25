using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GunnyRequestStudio.Core
{
    /// <summary>
    /// Tu dong anh xa XML request -> bang CSDL trong Db_Tank_All.
    /// Thu tu uu tien: override thu cong -> trung ten (exact) -> giong tap attribute nhat.
    /// </summary>
    public class TableMapper
    {
        private readonly DbSchema _schema;
        private readonly Dictionary<string, string> _overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Bang he thong / dong / backup: khong bao gio import
        private static readonly string[] SkipPatterns = new[]
        {
            "_bak", "temp", "dtproperties", "sys_", "site_", "users", "user_", "comandos",
            "titulos", "evento", "itenscategoria", "webshop", "log", "record", "history",
            "data9", "rate_fight"
        };

        public TableMapper(DbSchema schema, Dictionary<string, string> overrides = null)
        {
            _schema = schema ?? new DbSchema();
            if (overrides != null)
            {
                foreach (var kv in overrides) _overrides[kv.Key ?? string.Empty] = kv.Value;
            }
        }

        public DbSchema Schema { get { return _schema; } }

        public Dictionary<string, string> Overrides { get { return _overrides; } }

        public static Dictionary<string, string> LoadOverrides(string path)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return dict;
            AppSettingsOverrides wrapper = null;
            try { wrapper = Json.LoadFile<AppSettingsOverrides>(path); } catch { }
            if (wrapper != null && wrapper.Map != null)
            {
                foreach (var item in wrapper.Map)
                {
                    if (!string.IsNullOrEmpty(item.Xml) && !string.IsNullOrEmpty(item.Table))
                        dict[item.Xml] = item.Table;
                }
                return dict;
            }
            // fallback: doc dang "xml=table" moi dong
            foreach (var line in File.ReadAllLines(path))
            {
                var s = (line ?? string.Empty).Trim();
                if (s.Length == 0 || s.StartsWith("#") || s.StartsWith("//")) continue;
                var idx = s.IndexOf('=');
                if (idx <= 0) continue;
                dict[s.Substring(0, idx).Trim()] = s.Substring(idx + 1).Trim();
            }
            return dict;
        }

        public static void SaveOverrides(string path, Dictionary<string, string> overrides)
        {
            var wrapper = new AppSettingsOverrides();
            wrapper.Map = overrides.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase)
                .Select(k => new OverrideItem { Xml = k.Key, Table = k.Value }).ToList();
            Json.SaveFile(path, wrapper);
        }

        private static bool IsSkippedTable(string name)
        {
            var n = (name ?? string.Empty).ToLowerInvariant();
            if (n.Length == 0) return true;
            if (n.Contains("_bak")) return true;                            // bang backup
            if (n == "dtproperties" || n == "temptable") return true;        // bang rac
            if (n.StartsWith("sys_") || n.StartsWith("site_")) return true;
            if (n.StartsWith("user_") || n == "users") return true;
            if (n.StartsWith("log_") || n.Contains("_log_") || n.EndsWith("_logs") || n.EndsWith("_log")) return true;
            if (n.Contains("itenscategoria") || n.Contains("eventopnts") || n.Contains("comandos")) return true;
            if (n.Contains("titulos") || n.Contains("webshop")) return true;
            if (n == "item_record" || n == "fight_record" || n == "fight_rate") return true;
            return false;
        }

        public XmlTableMapping Resolve(XmlDocInfo doc)
        {
            var res = new XmlTableMapping { XmlName = doc.Name ?? string.Empty };
            if (doc.Attributes == null || doc.Attributes.Count == 0 || doc.Rows == null || doc.Rows.Count == 0)
            {
                res.Status = "none";
                res.Problem = doc.Error ?? "khong co dong du lieu";
                return res;
            }
            res.XmlAttributeCount = doc.Attributes.Count;

            // 1) override
            string overridden;
            if (doc.Name != null && _overrides.TryGetValue(doc.Name, out overridden))
            {
                if (overridden == null || overridden == "-" || overridden.Equals("none", StringComparison.OrdinalIgnoreCase))
                {
                    res.Status = "none";
                    res.Problem = "override: khong import (danh dau bo qua)";
                    return res;
                }
                var t = _schema.FindTable(overridden);
                if (t != null)
                {
                    Fill(res, t, doc);
                    res.TableName = t.Name;
                    res.Status = "override";
                    res.Score = 1.0;
                    return res;
                }
            }

            var xmlNorm = XmlCodec.NormalizeName(doc.Name);
            var attrSet = new HashSet<string>(doc.Attributes.Select(a => a.ToLowerInvariant()));

            var candidates = new List<MappingCandidate>();
            foreach (var table in _schema.Tables)
            {
                if (IsSkippedTable(table.Name)) continue;
                var tableNorm = XmlCodec.NormalizeName(table.Name);
                bool exactEq = tableNorm == xmlNorm;
                bool nameMatch = exactEq
                                 || tableNorm == xmlNorm + "s"
                                 || xmlNorm == tableNorm + "s"
                                 || tableNorm == xmlNorm + "list"
                                 || tableNorm == xmlNorm + "info"
                                 || xmlNorm == tableNorm + "info"
                                 || tableNorm == xmlNorm + "data"
                                 || xmlNorm == tableNorm + "data";

                int covered = 0;
                int extra = 0;
                var notNullNoDefault = new List<string>();
                foreach (var col in table.Columns)
                {
                    bool inXml = attrSet.Contains(col.Name.ToLowerInvariant());
                    if (inXml) covered++;
                    else
                    {
                        if (!col.IsComputed) extra++;
                        if (!col.IsNullable && !col.IsIdentity && !col.IsComputed && !col.HasDefault) notNullNoDefault.Add(col.Name);
                    }
                }
                double coverage = (double)covered / doc.Attributes.Count;

                if (coverage >= 0.85 || nameMatch)
                {
                    candidates.Add(new MappingCandidate
                    {
                        TableName = table.Name,
                        Score = Math.Round(coverage, 4),
                        ExtraColumns = extra,
                        NameMatch = nameMatch,
                        ExactName = exactEq
                    });
                }
            }

            candidates = candidates
                .OrderByDescending(c => c.Score)
                .ThenBy(c => c.ExtraColumns)
                .ThenByDescending(c => c.NameMatch)
                .Take(8).ToList();
            res.Candidates = candidates;

            // 2) trung ten tuyet doi + attribute coverage cao
            var exact = candidates.FirstOrDefault(c => c.ExactName && c.Score >= 0.9);
            if (exact != null)
            {
                var t = _schema.FindTable(exact.TableName);
                Fill(res, t, doc);
                res.TableName = t.Name;
                res.Status = res.MissingRequiredColumns.Count == 0 ? "exact" : "exact-warn";
                res.Score = exact.Score;
                return res;
            }

            // 3) theo tap attribute (coverage cao nhat, it cot thua nhat)
            var best = candidates.FirstOrDefault(c => c.Score >= 0.9);
            if (best != null)
            {
                var t = _schema.FindTable(best.TableName);
                Fill(res, t, doc);
                res.TableName = t.Name;
                var ties = candidates.Where(c => c.TableName != best.TableName
                                                 && Math.Abs(c.Score - best.Score) < 0.0001
                                                 && c.ExtraColumns == best.ExtraColumns).ToList();
                if (ties.Count == 0)
                {
                    res.Status = res.MissingRequiredColumns.Count == 0 ? "auto" : "auto-warn";
                    res.Score = best.Score;
                    return res;
                }
                var better = ties.FirstOrDefault(c => c.NameMatch && !best.NameMatch);
                if (better != null)
                {
                    var tb = _schema.FindTable(better.TableName);
                    Fill(res, tb, doc);
                    res.TableName = tb.Name;
                    res.Status = res.MissingRequiredColumns.Count == 0 ? "auto" : "auto-warn";
                    res.Score = better.Score;
                    return res;
                }
                res.Status = "ambiguous";
                res.Score = best.Score;
                res.Problem = "nhieu bang cung khop attribute: " + string.Join(", ", ties.Take(4).Select(x => x.TableName).ToArray());
                return res;
            }

            res.Status = "none";
            res.Problem = "khong tim thay bang CSDL phu hop";
            return res;
        }

        private void Fill(XmlTableMapping res, DbTable table, XmlDocInfo doc)
        {
            res.TableColumnCount = table.Columns.Count;
            res.IgnoredAttributes = doc.Attributes.Where(a => table.FindColumnFuzzy(a) == null).ToList();
            var mappedCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var a in doc.Attributes)
            {
                var col = table.FindColumnFuzzy(a);
                if (col != null) mappedCols.Add(col.Name);
            }
            res.MissingRequiredColumns = table.Columns
                .Where(c => !c.IsNullable && !c.IsIdentity && !c.IsComputed && !c.HasDefault && !mappedCols.Contains(c.Name))
                .Select(c => c.Name).ToList();
        }

        // ------------------------------------------------------------ DTO cho file override

        [System.Runtime.Serialization.DataContract]
        public class AppSettingsOverrides
        {
            [System.Runtime.Serialization.DataMember(Order = 1)] public List<OverrideItem> Map = new List<OverrideItem>();
        }

        [System.Runtime.Serialization.DataContract]
        public class OverrideItem
        {
            [System.Runtime.Serialization.DataMember(Order = 1)] public string Xml;
            [System.Runtime.Serialization.DataMember(Order = 2)] public string Table;
        }
    }
}



