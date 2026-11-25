using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;

namespace GunnyRequestStudio.Core
{
    // ======================================================== TYPE AUDIT
    // Kiem tra kieu du lieu request publisher vs kieu cot CSDL local.
    // Muc dich: phat hien som truong hop publisher doi kieu (vi du fightpower int -> long),
    // tran so int32/smallint... de khong bi mat/lam tron du lieu khi import.

    [DataContract]
    public class TypeAuditIssue
    {
        [DataMember(Order = 1)] public string Xml;
        [DataMember(Order = 2)] public string Attr;
        [DataMember(Order = 3)] public string Table;
        [DataMember(Order = 4)] public string Column;
        [DataMember(Order = 5)] public string SqlType;
        [DataMember(Order = 6)] public string MaxValue;
        [DataMember(Order = 7)] public int RowsAffected;
        [DataMember(Order = 8)] public string Suggestion;
    }

    [DataContract]
    public class TypeAuditReport
    {
        [DataMember(Order = 1)] public string GeneratedAt;
        [DataMember(Order = 2)] public int DocsScanned;
        [DataMember(Order = 3)] public int RowsScanned;
        [DataMember(Order = 4)] public int NumericColumnsChecked;
        [DataMember(Order = 5)] public List<TypeAuditIssue> Issues = new List<TypeAuditIssue>();
        [DataMember(Order = 6)] public string Summary;
    }

    public static class TypeAudit
    {
        private sealed class Range
        {
            public decimal Min;
            public decimal Max;
            public string Bigger;       // kieu nen doi sang
        }

        private static Range GetRange(string sqlType)
        {
            switch ((sqlType ?? string.Empty).ToLowerInvariant())
            {
                case "tinyint": return new Range { Min = 0m, Max = 255m, Bigger = "smallint" };
                case "smallint": return new Range { Min = -32768m, Max = 32767m, Bigger = "int" };
                case "int": return new Range { Min = int.MinValue, Max = int.MaxValue, Bigger = "bigint" };
                case "bigint": return new Range { Min = long.MinValue, Max = long.MaxValue, Bigger = "decimal(38,0)" };
                case "real": return new Range { Min = decimal.MinValue, Max = decimal.MaxValue, Bigger = "float" };
                case "float": return new Range { Min = decimal.MinValue, Max = decimal.MaxValue, Bigger = "decimal(38,10)" };
                case "decimal":
                case "numeric":
                case "money":
                case "smallmoney": return new Range { Min = decimal.MinValue, Max = decimal.MaxValue, Bigger = "decimal(38,10)" };
                default: return null;
            }
        }

        /// <summary>Quet tat ca XML da map bang va so sanh gia tri so voi kieu cot CSDL.</summary>
        public static TypeAuditReport Run(IEnumerable<XmlDocInfo> docs, TableMapper mapper, DbSchema schema, Logger log = null)
        {
            var rep = new TypeAuditReport { GeneratedAt = DateTime.Now.ToString("s") };
            var issues = new List<TypeAuditIssue>();
            int numericCols = 0;
            foreach (var doc in docs ?? Enumerable.Empty<XmlDocInfo>())
            {
                rep.DocsScanned++;
                rep.RowsScanned += doc.Rows == null ? 0 : doc.Rows.Count;
                XmlTableMapping map;
                try { map = mapper.Resolve(doc); } catch { continue; }
                if (!DbImporter.IsImportable(map))
                {
                    ScanUnmappedBigValues(doc, issues);
                    continue;
                }
                var table = schema.FindTable(map.TableName);
                if (table == null) continue;
                foreach (var attr in doc.Attributes ?? new List<string>())
                {
                    var col = table.FindColumnFuzzy(attr);
                    if (col == null || col.IsComputed) continue;
                    var range = GetRange(col.DataType);
                    if (range == null) continue;
                    numericCols++;
                    decimal maxAbs = 0m, minVal = 0m, maxVal = 0m;
                    int overflowCount = 0, parseFail = 0, rows = 0;
                    string firstBad = null;
                    foreach (var row in doc.Rows)
                    {
                        string raw;
                        if (!row.TryGetValue(attr, out raw) || string.IsNullOrEmpty(raw)) continue;
                        rows++;
                        decimal v;
                        string trimmed = raw.Trim();
                        bool parsed = decimal.TryParse(trimmed, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out v);
                        if (!parsed && (string.Equals(trimmed, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(trimmed, "false", StringComparison.OrdinalIgnoreCase)))
                        {
                            v = string.Equals(trimmed, "true", StringComparison.OrdinalIgnoreCase) ? 1m : 0m;
                            parsed = true;
                        }
                        if (!parsed)
                        {
                            parseFail++;
                            if (firstBad == null) firstBad = raw;
                            continue;
                        }
                        if (v < minVal) minVal = v;
                        if (v > maxVal) maxVal = v;
                        decimal a = Math.Abs(v);
                        if (a > maxAbs) maxAbs = a;
                        if (v < range.Min || v > range.Max)
                        {
                            overflowCount++;
                            if (parseFail == 0 && firstBad == null) firstBad = raw;
                        }
                    }
                    if (overflowCount > 0 || parseFail > 0)
                    {
                        var issue = new TypeAuditIssue
                        {
                            Xml = doc.Name,
                            Attr = attr,
                            Table = table.Name,
                            Column = col.Name,
                            SqlType = col.DataType,
                            RowsAffected = overflowCount > 0 ? overflowCount : parseFail,
                            MaxValue = (maxAbs > Math.Abs(minVal) ? maxAbs : Math.Abs(minVal)).ToString(CultureInfo.InvariantCulture)
                        };
                        if (overflowCount > 0)
                        {
                            issue.Suggestion = string.Format(
                                "ALTER TABLE [{0}].[{1}] ALTER COLUMN [{2}] {3} {4}  -- publisher co the da doi kieu (int -> long); {5} dong vuot pham vi {6}",
                                table.Schema, table.Name, col.Name, range.Bigger,
                                col.IsNullable ? "NULL" : "NOT NULL", overflowCount, col.DataType);
                        }
                        else
                        {
                            issue.Suggestion = "gia tri khong phai so (vi du '" + firstBad + "') - kiem tra lai publisher/template";
                        }
                        issues.Add(issue);
                        if (log != null)
                            log.Warn("Kieu du lieu: " + doc.Name + "." + attr + " -> " + table.Name + "." + col.Name
                                     + " (" + col.DataType + "), " + issue.RowsAffected + " dong vuot pham vi. " + issue.Suggestion);
                    }
                }
            }
            rep.Issues = issues;
            rep.NumericColumnsChecked = numericCols;
            rep.Summary = issues.Count == 0
                ? "OK - khong phat hien vuot pham vi kieu du lieu (" + numericCols + " cot so da kiem tra)"
                : issues.Count + " canh bao kieu du lieu (" + numericCols + " cot so da kiem tra)";
            return rep;
        }

        private static readonly System.Text.RegularExpressions.Regex NumericAttr = new System.Text.RegularExpressions.Regex(
            "(?i)(power|fight|gp|gold|money|exp|damage|score|rank|point|price|blood|hp)",
            System.Text.RegularExpressions.RegexOptions.Compiled);

        /// <summary>File chua map bang local nhung publisher gui so lon (fightpower int->long...).</summary>
        private static void ScanUnmappedBigValues(XmlDocInfo doc, List<TypeAuditIssue> issues)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var attr in doc.Attributes ?? new List<string>())
            {
                if (!NumericAttr.IsMatch(attr)) continue;
                decimal maxAbs = 0m; string sample = null; int big = 0; int rows = 0;
                foreach (var row in doc.Rows)
                {
                    string raw;
                    if (!row.TryGetValue(attr, out raw) || string.IsNullOrEmpty(raw)) continue;
                    decimal v;
                    if (!decimal.TryParse(raw.Trim(), NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out v)) continue;
                    rows++;
                    decimal a = Math.Abs(v);
                    if (a > maxAbs) { maxAbs = a; sample = raw.Trim(); }
                    if (a > int.MaxValue) big++;
                }
                if (big > 0 && seen.Add(attr))
                {
                    issues.Add(new TypeAuditIssue
                    {
                        Xml = doc.Name,
                        Attr = attr,
                        Table = "(chua map bang local)",
                        Column = "(chua co bang)",
                        SqlType = "publisher: long",
                        MaxValue = maxAbs.ToString(CultureInfo.InvariantCulture),
                        RowsAffected = big,
                        Suggestion = "INFO: publisher dung gia tri > int32 (" + big + " dong, max " + maxAbs.ToString(CultureInfo.InvariantCulture)
                                     + "). Neu map file nay vao bang local phai dung kieu bigint (long), khong dung int."
                    });
                }
            }
        }

        public static string DefaultReportPath(string reportsDir)
        {
            return Path.Combine(string.IsNullOrEmpty(reportsDir) ? "reports" : reportsDir, "type-audit.json");
        }
    }
}
