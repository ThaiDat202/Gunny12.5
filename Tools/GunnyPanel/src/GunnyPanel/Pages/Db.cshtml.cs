using System.Text;
using GunnySuite.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GunnyPanel.Pages;

public sealed class DbModel : PageModel
{
    private readonly SqlDatabase _db;

    public DbModel(SqlDatabase db) => _db = db;

    public IReadOnlyCollection<string> Connections { get; private set; } = Array.Empty<string>();
    public List<string> Tables { get; } = new();

    [BindProperty] public string Connection { get; set; } = "Db_Tank_All";
    [BindProperty] public string Sql { get; set; } = "SELECT TOP 20 * FROM [dbo].[Sys_Users_Detail]";

    public SqlResult? Result { get; private set; }
    public string? Message { get; set; }

    public async Task OnGetAsync()
    {
        Connections = _db.Names;
        await LoadTablesAsync();
    }

    public async Task OnPostAsync()
    {
        Connections = _db.Names;
        await LoadTablesAsync();

        var sqlText = (Sql ?? "").Trim();
        if (!IsSelect(sqlText))
        {
            Message = "Giai đoạn test chỉ cho phép câu lệnh SELECT/WITH.";
            return;
        }

        Result = await _db.QueryAsync(Connection, sqlText, 300, 30);
        Message = Result.Ok
            ? $"OK: đọc {Result.TotalRead} dòng."
            : "LỖI: " + Result.Error;
    }

    public async Task<IActionResult> OnGetExportAsync(string connection, string sql)
    {
        if (!IsSelect((sql ?? "").Trim())) return Content("Chi cho phep SELECT/WITH.");
        if (!_db.Has(connection)) return Content("Connection khong ton tai.");

        var result = await _db.QueryAsync(connection, sql, 5000, 60);
        if (!result.Ok) return Content("LOI: " + result.Error);

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", result.Columns.Select(Csv)));
        foreach (var row in result.Rows)
            sb.AppendLine(string.Join(",", row.Select(v => Csv(v ?? ""))));
        return File(Encoding.UTF8.GetBytes("\uFEFF" + sb), "text/csv", $"export_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
    }

    private async Task LoadTablesAsync()
    {
        var tables = await _db.QueryAsync("Db_Tank_All", "SELECT name FROM sys.tables ORDER BY name", 1000, 10);
        if (!tables.Ok) return;
        foreach (var row in tables.Rows)
            if (row[0] != null) Tables.Add(row[0]);
    }

    /// <summary>
    /// Từ khoá bị chặn trong câu SELECT/WITH. Chặn batch nhiều câu lệnh và các lệnh ghi/DDL
    /// (kể cả CTE dạng WITH ... DELETE/UPDATE, SELECT ... INTO, OPENROWSET, xp_cmdshell...).
    /// </summary>
    private static readonly string[] ForbiddenKeywords =
    {
        "insert", "update", "delete", "merge", "exec", "execute", "drop", "alter", "create",
        "truncate", "grant", "revoke", "deny", "backup", "restore", "dbcc", "kill", "shutdown",
        "bulk", "into", "openrowset", "openquery", "opendatasource", "xp_cmdshell", "sp_executesql",
        "sp_oacreate", "waitfor", "reconfigure", "attach", "detach", "checkpoint"
    };

    /// <summary>
    /// Giai đoạn test chỉ cho phép ĐÚNG MỘT câu SELECT/WITH.
    /// Không dựa vào StartsWith nữa vì "SELECT 1; DELETE ..." hay "WITH x AS (...) DELETE ..." đều lọt.
    /// </summary>
    private static bool IsSelect(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql)) return false;

        var masked = MaskLiteralsAndComments(sql).Trim();
        if (masked.Length == 0) return false;

        // Cho phép 1 dấu ; ở cuối câu, còn lại là nhiều câu lệnh -> chặn.
        if (masked.EndsWith(";")) masked = masked.Substring(0, masked.Length - 1).TrimEnd();
        if (masked.Contains(';')) return false;

        if (!(StartsWithWord(masked, "select") || StartsWithWord(masked, "with"))) return false;

        foreach (var keyword in ForbiddenKeywords)
            if (ContainsWord(masked, keyword)) return false;

        return true;
    }

    private static bool StartsWithWord(string text, string word)
    {
        if (!text.StartsWith(word, StringComparison.OrdinalIgnoreCase)) return false;
        if (text.Length == word.Length) return true;
        return !IsWordChar(text[word.Length]);
    }

    private static bool ContainsWord(string text, string word)
    {
        var index = 0;
        while (true)
        {
            index = text.IndexOf(word, index, StringComparison.OrdinalIgnoreCase);
            if (index < 0) return false;
            var beforeOk = index == 0 || !IsWordChar(text[index - 1]);
            var after = index + word.Length;
            var afterOk = after >= text.Length || !IsWordChar(text[after]);
            if (beforeOk && afterOk) return true;
            index += word.Length;
        }
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_' || c == '@' || c == '#';

    /// <summary>
    /// Thay nội dung chuỗi ký tự / định danh [..] / comment bằng khoảng trắng
    /// để việc tìm từ khoá và dấu ; không bị đánh lừa bởi dữ liệu literal.
    /// </summary>
    private static string MaskLiteralsAndComments(string sql)
    {
        var sb = new StringBuilder(sql.Length);
        var i = 0;
        while (i < sql.Length)
        {
            var c = sql[i];

            if (c == '-' && i + 1 < sql.Length && sql[i + 1] == '-')
            {
                while (i < sql.Length && sql[i] != '\n') { sb.Append(' '); i++; }
                continue;
            }

            if (c == '/' && i + 1 < sql.Length && sql[i + 1] == '*')
            {
                sb.Append(' ').Append(' ');
                i += 2;
                while (i < sql.Length)
                {
                    if (sql[i] == '*' && i + 1 < sql.Length && sql[i + 1] == '/')
                    {
                        sb.Append(' ').Append(' ');
                        i += 2;
                        break;
                    }
                    sb.Append(' ');
                    i++;
                }
                continue;
            }

            if (c == '\'' || c == '"' || c == '[')
            {
                var close = c == '[' ? ']' : c;
                sb.Append(' ');
                i++;
                while (i < sql.Length)
                {
                    if (sql[i] == close)
                    {
                        if (i + 1 < sql.Length && sql[i + 1] == close)
                        {
                            sb.Append(' ').Append(' ');
                            i += 2;
                            continue;
                        }
                        sb.Append(' ');
                        i++;
                        break;
                    }
                    sb.Append(' ');
                    i++;
                }
                continue;
            }

            sb.Append(c);
            i++;
        }
        return sb.ToString();
    }

    private static string Csv(string value)
    {
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }
}
