using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using GunnySuite.Core;

namespace GunnyPanel.Jobs;

/// <summary>
/// Request XML -> SQL (tuong thich ToolRip GunTools):
/// - Nhan XML tu mirror local / VNG / file local (path).
/// - Ho tro wrapper: ItemTemplate / Store / RuneTemplate.
/// - Ho tro AutoID (them cot ID tang dan) nhu ToolRip.
/// - Kieu xuat: "compact" (multi-row batch, nhanh) hoac "toolrip" (1 INSERT/dong).
/// - Mac dinh dry-run: sinh file .sql trong data/sql; neu execute=true thi ghi DB trong transaction.
/// </summary>
public sealed class RequestSqlJobHandler : IJobHandler
{
    private const int RowsPerBatch = 400;

    private readonly IConfiguration _cfg;
    private readonly HttpFetcher _http;
    private readonly SqlDatabase _db;

    public RequestSqlJobHandler(IConfiguration cfg, HttpFetcher http, SqlDatabase db)
    {
        _cfg = cfg;
        _http = http;
        _db = db;
    }

    public string Type => "request-sql";

    public async Task<string> RunAsync(JobExecutionContext ctx, CancellationToken ct)
    {
        var xmlName = Param(ctx, "xml", "").Trim();
        var source = Param(ctx, "source", "local").Trim().ToLowerInvariant();
        var localPath = Param(ctx, "path", "").Trim();
        var connection = Param(ctx, "connection", "Db_Tank_All").Trim();
        var execute = string.Equals(Param(ctx, "execute", "false"), "true", StringComparison.OrdinalIgnoreCase);
        var tableOverride = Param(ctx, "table", "").Trim();
        var autoId = string.Equals(Param(ctx, "autoId", "false"), "true", StringComparison.OrdinalIgnoreCase);
        var style = Param(ctx, "style", "compact").Trim().ToLowerInvariant();
        if (style != "toolrip") style = "compact";

        if (string.IsNullOrWhiteSpace(xmlName) && source != "file")
            throw new InvalidOperationException("Thieu ten XML (vi du: levellist, balllist, item...)");

        ctx.Report(2, $"XML={xmlName} | source={source} | connection={connection} | execute={execute} | autoId={autoId} | style={style}");

        // 1) Lay XML
        byte[]? data = null;
        var origin = "";
        if (source == "file")
        {
            if (string.IsNullOrWhiteSpace(localPath) || !System.IO.File.Exists(localPath))
                throw new InvalidOperationException("Khong thay file XML local: " + localPath);
            data = await System.IO.File.ReadAllBytesAsync(localPath, ct);
            origin = localPath;
            if (string.IsNullOrWhiteSpace(xmlName)) xmlName = Path.GetFileNameWithoutExtension(localPath);
            ctx.Report(15, "Da doc XML local: " + localPath);
        }
        else if (source == "vng")
        {
            var url = (_cfg["Vng:RequestSource"] ?? "https://quest.gn.zing.vn/").TrimEnd('/') + "/" + xmlName + ".xml";
            data = await _http.TryGetAsync(url, ct);
            if (data == null) throw new InvalidOperationException("Khong tai duoc XML: " + url);
            origin = url;
            ctx.Report(15, "Da tai XML tu quest VNG");
        }
        else
        {
            foreach (var dir in new[] { _cfg["Paths:MirrorDir"], _cfg["Paths:WebRequestDir"] })
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;
                var path = Path.Combine(dir, xmlName + ".xml");
                if (!System.IO.File.Exists(path)) continue;
                data = await System.IO.File.ReadAllBytesAsync(path, ct);
                origin = path;
                ctx.Report(15, "Da doc XML local: " + path);
                break;
            }
            if (data == null)
                throw new InvalidOperationException($"Khong thay file XML local: {xmlName}.xml (kiem tra Paths:MirrorDir / WebRequestDir)");
        }

        // 2) Giai nen zlib + parse
        if (Zlib.LooksZlib(data)) data = Zlib.Decompress(data);
        XDocument doc;
        try
        {
            doc = XDocument.Load(new MemoryStream(data));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("XML khong hop le: " + ex.Message);
        }

        var root = doc.Root ?? throw new InvalidOperationException("XML khong co root element");
        var (dataEl, items) = FindData(root);
        if (items.Count == 0)
            throw new InvalidOperationException($"XML '{xmlName}' khong co ban ghi con nao");
        ctx.Report(22, $"Root={root.Name.LocalName}, wrapper={dataEl.Name.LocalName}, {items.Count} ban ghi");

        // 3) Map XML -> ten bang (Db_Gm.dbo.Table_Games)
        var table = tableOverride;
        if (string.IsNullOrWhiteSpace(table))
        {
            var map = await _db.QueryAsync("Db_Gm",
                "SELECT TOP 1 [Name] FROM [dbo].[Table_Games] WHERE LOWER(LTRIM(RTRIM([XmlName]))) = LOWER(@xml) ORDER BY [ID]",
                new Dictionary<string, object?> { ["xml"] = xmlName }, 5, 15, ct);
            if (!map.Ok) throw new InvalidOperationException("Loi doc Db_Gm.Table_Games: " + map.Error);
            table = map.FirstCell();
            if (string.IsNullOrWhiteSpace(table))
                throw new InvalidOperationException($"Khong tim thay bang cho XmlName='{xmlName}' trong Db_Gm.Table_Games");
        }

        if (!Regex.IsMatch(table!, "^[A-Za-z0-9_]+$"))
            throw new InvalidOperationException("Ten bang khong hop le: " + table);
        ctx.Report(28, $"Bang dich: [dbo].[{table}]");

        // 4) Lay metadata cot trong DB dich
        var meta = await _db.QueryAsync(connection,
            "SELECT [COLUMN_NAME],[DATA_TYPE] FROM INFORMATION_SCHEMA.COLUMNS WHERE [TABLE_SCHEMA]='dbo' AND [TABLE_NAME]=@table ORDER BY [ORDINAL_POSITION]",
            new Dictionary<string, object?> { ["table"] = table }, 800, 20, ct);
        if (!meta.Ok) throw new InvalidOperationException("Loi doc metadata cot: " + meta.Error);
        if (meta.Rows.Count == 0)
            throw new InvalidOperationException($"Bang [{table}] khong ton tai trong DB '{connection}'");

        var firstAttrs = items[0].Attributes().Select(a => a.Name.LocalName).ToList();
        var columns = new List<(string Name, string Type)>();
        foreach (var row in meta.Rows)
        {
            var colName = row[0] ?? "";
            var colType = (row[1] ?? "").ToLowerInvariant();
            if (colName.Length == 0) continue;
            var hasAttr = firstAttrs.Any(a => string.Equals(a, colName, StringComparison.OrdinalIgnoreCase));
            if (hasAttr) columns.Add((colName, colType));
        }

        if (autoId)
        {
            var idCol = meta.Rows.FirstOrDefault(r => string.Equals(r[0], "ID", StringComparison.OrdinalIgnoreCase) ||
                                                       string.Equals(r[0], "Id", StringComparison.OrdinalIgnoreCase));
            if (idCol == null)
                throw new InvalidOperationException($"Bang [{table}] khong co cot ID de dung AutoID");
            columns.RemoveAll(c => string.Equals(c.Name, "ID", StringComparison.OrdinalIgnoreCase));
            columns.Insert(0, ("ID", "int"));
        }

        if (columns.Count == 0)
            throw new InvalidOperationException(
                $"Khong cot nao khop: XML co [{string.Join(", ", firstAttrs.Take(12))}], bang [{table}] co {meta.Rows.Count} cot");
        ctx.Report(34, $"Khop {columns.Count} cot: {string.Join(", ", columns.Select(c => c.Name))}");

        // 5) Sinh SQL
        var header = "INSERT INTO [dbo].[" + table + "] (" +
                     string.Join(",", columns.Select(c => "[" + c.Name + "]")) + ") VALUES ";
        var statements = new List<string>();
        var sb = new StringBuilder();
        var total = items.Count;
        var done = 0;
        var warnings = 0;
        var preview = "";

        foreach (var item in items)
        {
            ct.ThrowIfCancellationRequested();
            var attrs = item.Attributes().ToList();
            var values = new List<string>(columns.Count);
            foreach (var col in columns)
            {
                if (autoId && string.Equals(col.Name, "ID", StringComparison.OrdinalIgnoreCase))
                {
                    // ToolRip bat dau ID tu 1
                    values.Add((done + 1).ToString(CultureInfo.InvariantCulture));
                    continue;
                }
                var attr = attrs.FirstOrDefault(a => string.Equals(a.Name.LocalName, col.Name, StringComparison.OrdinalIgnoreCase));
                values.Add(ToSqlLiteral(attr?.Value, col.Type, ref warnings));
            }
            done++;

            if (style == "toolrip")
            {
                var line = header + "(" + string.Join(",", values) + ");";
                statements.Add(line);
                if (preview.Length == 0) preview = line;
                if (done % RowsPerBatch == 0 || done == total)
                    ctx.Report(34 + (int)(36.0 * done / total), $"Sinh SQL (ToolRip style): {done}/{total} dong");
            }
            else
            {
                if (sb.Length == 0) sb.Append(header);
                else sb.Append(',');
                sb.Append('(').Append(string.Join(",", values)).Append(')');
                if (done % RowsPerBatch == 0 || done == total)
                {
                    sb.Append(';');
                    statements.Add(sb.ToString());
                    if (preview.Length == 0) preview = sb.ToString();
                    sb.Clear();
                    ctx.Report(34 + (int)(36.0 * done / total), $"Sinh SQL: {done}/{total} ban ghi, {statements.Count} batch");
                }
            }
        }

        // 6) Ghi file SQL
        var dataDir = _cfg["Paths:DataDir"] ?? Path.Combine(AppContext.BaseDirectory, "data");
        var sqlDir = Path.Combine(dataDir, "sql");
        Directory.CreateDirectory(sqlDir);
        var file = Path.Combine(sqlDir, $"{Safe(xmlName)}-{DateTime.Now:yyyyMMdd-HHmmss}.sql");
        var fileLines = new List<string>
        {
            "-- GunnyPanel request-sql (ToolRip compatible)",
            $"-- xml={xmlName} table={table} connection={connection} rows={total} wrapper={dataEl.Name.LocalName} autoId={autoId} style={style} date={DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            "-- CANH BAO: chay lai nhieu lan se bi trung du lieu. Hay backup DB truoc khi import that."
        };
        fileLines.AddRange(statements);
        await System.IO.File.WriteAllLinesAsync(file, fileLines, ct);
        ctx.Report(72, $"Da ghi file SQL: {file}");

        // 7) Ket qua
        var summary = new StringBuilder();
        summary.AppendLine($"XML: {xmlName} ({total} ban ghi, wrapper={dataEl.Name.LocalName}) -> bang [dbo].[{table}] trong DB '{connection}'");
        summary.AppendLine($"Nguon: {origin}");
        summary.AppendLine($"Cot khop ({columns.Count}): {string.Join(", ", columns.Select(c => c.Name))}");
        summary.AppendLine($"Kieu SQL: {style} | AutoID: {autoId} | File: {file}");
        if (warnings > 0) summary.AppendLine($"Canh bao: {warnings} gia tri so khong parse duoc -> NULL");
        summary.AppendLine("Preview:");
        summary.AppendLine(PreviewLines(preview));

        if (!execute)
        {
            summary.AppendLine(">> DRY-RUN: chua ghi DB.");
            return summary.ToString();
        }

        ctx.Report(75, "Bat dau ghi DB trong transaction...");
        var (affected, error) = await _db.ExecuteBatchAsync(
            connection,
            statements,
            240,
            (i, n) => ctx.Report(75 + (int)(24.0 * i / Math.Max(1, n)), $"Ghi DB: batch {i}/{n}"),
            ct);

        if (error != null)
            throw new InvalidOperationException("Ghi DB that bai (da rollback): " + error);

        summary.AppendLine($">> DA GHI DB '{connection}': {affected} dong (transaction commit).");
        return summary.ToString();
    }

    /// <summary>
    /// Tim phan tu chua cac ban ghi: ho tro wrapper ItemTemplate / Store / RuneTemplate,
    /// con lai lay cac phan tu con truc tiep cua root (vd: &lt;Result&gt;&lt;item .../&gt;...&lt;/Result&gt;).
    /// </summary>
    private static (XElement Data, List<XElement> Items) FindData(XElement root)
    {
        foreach (var wrapper in new[] { "ItemTemplate", "Store", "RuneTemplate" })
        {
            var el = root.Element(wrapper);
            if (el != null && el.HasElements)
                return (el, el.Elements().ToList());
        }
        return (root, root.Elements().ToList());
    }

    private static string PreviewLines(string preview)
    {
        var first = preview.Split(';')[0];
        var parts = first.Split('\n');
        var lines = new List<string>();
        foreach (var line in parts)
        {
            if (line.Length == 0) continue;
            lines.Add(line.Length > 240 ? line[..240] + " ..." : line);
            if (lines.Count >= 8) break;
        }
        return string.Join("\n", lines);
    }

    private static string ToSqlLiteral(string? raw, string type, ref int warnings)
    {
        if (raw == null) return "NULL";
        var s = raw.Trim();
        switch (type)
        {
            case "int":
            case "bigint":
            case "smallint":
            case "tinyint":
                if (s.Length == 0) return "NULL";
                if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
                    return l.ToString(CultureInfo.InvariantCulture);
                warnings++;
                return "NULL";

            case "bit":
                if (s.Length == 0) return "NULL";
                var low = s.ToLowerInvariant();
                if (low is "1" or "true") return "1";
                if (low is "0" or "false") return "0";
                warnings++;
                return "NULL";

            case "decimal":
            case "numeric":
            case "money":
            case "smallmoney":
                if (s.Length == 0) return "NULL";
                if (decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var dec))
                    return dec.ToString(CultureInfo.InvariantCulture);
                warnings++;
                return "NULL";

            case "float":
            case "real":
                if (s.Length == 0) return "NULL";
                if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var dbl))
                    return dbl.ToString("R", CultureInfo.InvariantCulture);
                warnings++;
                return "NULL";

            case "datetime":
            case "datetime2":
            case "smalldatetime":
            case "date":
            case "time":
                if (s.Length == 0) return "NULL";
                return "N'" + s.Replace("'", "''") + "'";

            case "binary":
            case "varbinary":
            case "image":
            case "timestamp":
                if (s.Length == 0) return "NULL";
                return s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? s : "NULL";

            default:
                return "N'" + s.Replace("'", "''") + "'";
        }
    }

    private static string Safe(string name)
    {
        var sb = new StringBuilder();
        foreach (var ch in name)
            sb.Append(char.IsLetterOrDigit(ch) || ch is '_' or '-' ? ch : '_');
        return sb.Length == 0 ? "xml" : sb.ToString();
    }

    private static string Param(JobExecutionContext ctx, string key, string fallback) =>
        ctx.Parameters.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : fallback;
}
