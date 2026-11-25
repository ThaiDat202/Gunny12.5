using System.Text;
using GunnyPanel.Pages.Shared;
using GunnySuite.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GunnyPanel.Pages;

public sealed class UserRow
{
    public int UserID { get; set; }
    public string UserName { get; set; } = "";
    public string NickName { get; set; } = "";
    public bool Sex { get; set; }
    public int Grade { get; set; }
    public long FightPower { get; set; }
    public long GP { get; set; }
    public long Money { get; set; }
    public long Gold { get; set; }
    public int GiftToken { get; set; }
    public int State { get; set; }
    public string LastDate { get; set; } = "";
    public int ConsortiaID { get; set; }
    public string Email { get; set; } = "";

    public static UserRow From(string?[] row) => new()
    {
        UserID = ParseInt(row.ElementAtOrDefault(0)),
        UserName = row.ElementAtOrDefault(1) ?? "",
        NickName = row.ElementAtOrDefault(2) ?? "",
        Sex = ParseBit(row.ElementAtOrDefault(3)),
        Grade = ParseInt(row.ElementAtOrDefault(4)),
        FightPower = ParseLong(row.ElementAtOrDefault(5)),
        GP = ParseLong(row.ElementAtOrDefault(6)),
        Money = ParseLong(row.ElementAtOrDefault(7)),
        Gold = ParseLong(row.ElementAtOrDefault(8)),
        State = ParseInt(row.ElementAtOrDefault(9)),
        LastDate = FormatDate(row.ElementAtOrDefault(10)),
        ConsortiaID = ParseInt(row.ElementAtOrDefault(11)),
        Email = row.ElementAtOrDefault(12) ?? "",
        GiftToken = ParseInt(row.ElementAtOrDefault(13))
    };

    public static int ParseInt(string? value) => int.TryParse(value, out var v) ? v : 0;
    public static long ParseLong(string? value) => long.TryParse(value, out var v) ? v : 0;

    /// <summary>
    /// Cột bit của SQL Server được SqlDatabase chuyển thành chuỗi "True"/"False"
    /// (không phải "1"/"0"), nên phải chấp nhận cả hai dạng.
    /// </summary>
    public static bool ParseBit(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var v = value.Trim();
        return v == "1"
            || v.Equals("true", StringComparison.OrdinalIgnoreCase)
            || v.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    public static string FormatDate(string? value) =>
        DateTime.TryParse(value, out var dt) ? dt.ToString("dd/MM/yyyy HH:mm") : (value ?? "");
}

public sealed class UsersModel : PageModel
{
    public const int PageSize = 20;
    public const string Conn = "Db_Tank";

    private readonly SqlDatabase _db;

    public UsersModel(SqlDatabase db) => _db = db;

    public List<UserRow> Users { get; } = new();
    public int Total { get; private set; }
    public string? Error { get; private set; }

    [BindProperty(SupportsGet = true, Name = "q")] public string? Q { get; set; }
    public int PageIndex { get; private set; } = 1;
    [BindProperty(SupportsGet = true, Name = "sort")] public string Sort { get; set; } = "userid";
    [BindProperty(SupportsGet = true, Name = "online")] public int? Online { get; set; }

    public Pager Pager { get; private set; } = new();

    private static readonly Dictionary<string, string> SortOrders = new(StringComparer.OrdinalIgnoreCase)
    {
        ["userid"] = "UserID",
        ["level"] = "Grade DESC, UserID",
        ["fight"] = "FightPower DESC, UserID",
        ["money"] = "Money DESC, UserID",
        ["gold"] = "Gold DESC, UserID",
        ["gp"] = "GP DESC, UserID",
        ["last"] = "LastDate DESC, UserID"
    };

    public async Task OnGetAsync([FromQuery(Name = "page")] int pageNumber = 1)
    {
        // Razor Pages dùng route value tên "page" (= đường dẫn trang) nên KHÔNG bind trực tiếp
        // [BindProperty(Name="page")] được — phải đọc từ query string.
        PageIndex = pageNumber > 0 ? pageNumber : 1;
        var q = (Q ?? "").Trim();
        Sort = SortOrders.ContainsKey(Sort ?? "") ? Sort!.ToLowerInvariant() : "userid";
        var where = "WHERE (@q = '' OR UserName LIKE @like OR NickName LIKE @like OR Email LIKE @like OR CAST(UserID AS nvarchar(20)) = @q)";
        if (Online.HasValue && Online.Value == 1) where += " AND State > 0";

        var pars = new Dictionary<string, object?> { ["q"] = q, ["like"] = "%" + q + "%" };
        var count = await _db.QueryAsync(Conn, "SELECT COUNT(*) FROM dbo.Sys_Users_Detail " + where, pars, 1, 20);
        if (!count.Ok)
        {
            Error = count.Error;
            return;
        }
        Total = UserRow.ParseInt(count.FirstCell());
        PageIndex = Pager.Clamp(PageIndex, Total, PageSize);

        pars["skip"] = (PageIndex - 1) * PageSize;
        pars["take"] = PageSize;
        var page = await _db.QueryAsync(Conn,
            "SELECT UserID, UserName, NickName, Sex, Grade, FightPower, GP, Money, Gold, State, LastDate, ConsortiaID, Email " +
            "FROM dbo.Sys_Users_Detail " + where +
            " ORDER BY " + SortOrders[Sort] + " OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY",
            pars, PageSize, 30);
        if (!page.Ok)
        {
            Error = page.Error;
            return;
        }

        foreach (var row in page.Rows) Users.Add(UserRow.From(row));
        Pager = new Pager
        {
            Page = PageIndex,
            PageSize = PageSize,
            Total = Total,
            Path = "/Users",
            PageParam = "page",
            Query = new Dictionary<string, string?>
            {
                ["q"] = q,
                ["sort"] = Sort,
                ["online"] = Online?.ToString()
            }
        };
    }

    /// <summary>Xuất CSV danh sách user (chỉ đọc).</summary>
    public async Task<IActionResult> OnGetExportAsync()
    {
        var q = (Q ?? "").Trim();
        Sort = SortOrders.ContainsKey(Sort ?? "") ? Sort!.ToLowerInvariant() : "userid";
        var where = "WHERE (@q = '' OR UserName LIKE @like OR NickName LIKE @like OR Email LIKE @like OR CAST(UserID AS nvarchar(20)) = @q)";
        if (Online.HasValue && Online.Value == 1) where += " AND State > 0";
        var pars = new Dictionary<string, object?> { ["q"] = q, ["like"] = "%" + q + "%" };
        var result = await _db.QueryAsync(Conn,
            "SELECT UserID, UserName, NickName, Sex, Grade, FightPower, GP, Money, Gold, State, LastDate, ConsortiaID, Email, GiftToken " +
            "FROM dbo.Sys_Users_Detail " + where + " ORDER BY " + SortOrders[Sort],
            pars, 5000, 60);
        if (!result.Ok) return Content("LOI: " + result.Error);

        var sb = new StringBuilder();
        sb.AppendLine("UserID,UserName,NickName,Sex,Grade,FightPower,GP,Money,Gold,State,LastDate,ConsortiaID,Email,GiftToken");
        foreach (var row in result.Rows)
        {
            var user = UserRow.From(row);
            sb.AppendLine(string.Join(",", new[]
            {
                user.UserID.ToString(),
                Csv(user.UserName),
                Csv(user.NickName),
                user.Sex ? "1" : "0",
                user.Grade.ToString(),
                user.FightPower.ToString(),
                user.GP.ToString(),
                user.Money.ToString(),
                user.Gold.ToString(),
                user.State.ToString(),
                Csv(user.LastDate),
                user.ConsortiaID.ToString(),
                Csv(user.Email),
                user.GiftToken.ToString()
            }));
        }
        return File(Encoding.UTF8.GetBytes("\uFEFF" + sb), "text/csv", $"users_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
    }

    private static string Csv(string? value)
    {
        var v = value ?? "";
        if (v.Contains('"') || v.Contains(',') || v.Contains('\n') || v.Contains('\r'))
            return "\"" + v.Replace("\"", "\"\"") + "\"";
        return v;
    }
}
