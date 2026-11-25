using System.Globalization;
using GunnyPanel.Pages.Shared;
using GunnySuite.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GunnyPanel.Pages;

/// <summary>Một dòng hộp thư trong Db_Tank.dbo.User_Messages (chỉ đọc).</summary>
public sealed class MailRow
{
    public int ID { get; set; }
    public int SenderID { get; set; }
    public string Sender { get; set; } = "";
    public int ReceiverID { get; set; }
    public string Receiver { get; set; } = "";
    public string ReceiverNick { get; set; } = "";
    public string SenderNick { get; set; } = "";
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string SendTime { get; set; } = "";
    public string SendDate { get; set; } = "";
    public bool IsRead { get; set; }
    public bool IsDelR { get; set; }
    public bool IfDelS { get; set; }
    public bool IsDelete { get; set; }
    public bool IsExist { get; set; }
    public int Type { get; set; }
    public int Gold { get; set; }
    public int Money { get; set; }
    public int GiftToken { get; set; }
    public int ValidDate { get; set; }
    public string Remark { get; set; } = "";
    public string AnnexRemark { get; set; } = "";
    public int AnnexCount { get; set; }
    public string[] AnnexRaw { get; set; } = Array.Empty<string>();
    public string[] AnnexNames { get; set; } = Array.Empty<string>();

    public string SenderDisplay =>
        !string.IsNullOrWhiteSpace(Sender) ? Sender
        : !string.IsNullOrWhiteSpace(SenderNick) ? SenderNick
        : SenderID > 0 ? "User " + SenderID : "Hệ thống";

    public string ReceiverDisplay =>
        !string.IsNullOrWhiteSpace(ReceiverNick) ? ReceiverNick
        : !string.IsNullOrWhiteSpace(Receiver) ? Receiver
        : "User " + ReceiverID;

    public bool IsUnread => !IsRead;

    public static MailRow From(string?[] row) => new()
    {
        ID = ParseInt(row, 0),
        SenderID = ParseInt(row, 1),
        Sender = Text(row, 2),
        ReceiverID = ParseInt(row, 3),
        Receiver = Text(row, 4),
        ReceiverNick = Text(row, 5),
        SenderNick = Text(row, 6),
        Title = Text(row, 7),
        Content = Text(row, 8),
        SendTime = FormatDate(Text(row, 9)),
        IsRead = IsTrue(Text(row, 10)),
        IsDelR = IsTrue(Text(row, 11)),
        IfDelS = IsTrue(Text(row, 12)),
        IsDelete = IsTrue(Text(row, 13)),
        IsExist = IsTrue(Text(row, 14)),
        Type = ParseInt(row, 15),
        Gold = ParseInt(row, 16),
        Money = ParseInt(row, 17),
        GiftToken = ParseInt(row, 18),
        ValidDate = ParseInt(row, 19),
        SendDate = FormatDate(Text(row, 20)),
        Remark = Text(row, 21),
        AnnexRemark = Text(row, 22),
        AnnexCount = ParseInt(row, 23),
        AnnexRaw = new[] { Text(row, 24), Text(row, 25), Text(row, 26), Text(row, 27), Text(row, 28) },
        AnnexNames = new[] { Text(row, 29), Text(row, 30), Text(row, 31), Text(row, 32), Text(row, 33) }
    };

    private static string Text(string?[] row, int index) => row.ElementAtOrDefault(index) ?? "";

    private static int ParseInt(string?[] row, int index) =>
        int.TryParse(Text(row, index), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;

    private static bool IsTrue(string? value) =>
        value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    private static string FormatDate(string? value) =>
        DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)
            ? dt.ToString("dd/MM/yyyy HH:mm")
            : (value ?? "");
}

/// <summary>Phân bố loại thư (GROUP BY Type) trong phạm vi lọc.</summary>
public sealed class MailTypeStat
{
    public int Type { get; set; }
    public int Count { get; set; }
    public int Unread { get; set; }
    public string Label => MailModel.TypeLabel(Type);
    public string BadgeClass => MailModel.TypeBadgeClass(Type);
}

/// <summary>Một vật phẩm đính kèm (Annex1..Annex5) của thư, có tên + ảnh nếu tra được.</summary>
public sealed class MailAnnex
{
    public int Slot { get; set; }
    public string Raw { get; set; } = "";
    public string MailName { get; set; } = "";
    public int? TemplateId { get; set; }
    public int? ItemId { get; set; }
    public string Name { get; set; } = "";
    public string ShopName { get; set; } = "";
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public string ImageUrl { get; set; } = "";
    public string Source { get; set; } = "";

    public bool HasShopName => !string.IsNullOrWhiteSpace(ShopName);
    public string SourceText => Source switch
    {
        "template-id" => "Khớp TemplateID trong Shop_Goods",
        "item-id" => "Khớp ItemID trong Sys_Users_Goods (thư GM gửi vật phẩm)",
        "mail-name" => "Không khớp ID — khớp theo tên trong Shop_Goods (ảnh tham khảo)",
        _ => "Không tìm thấy trong Shop_Goods"
    };
}

/// <summary>Trang xem hộp thư User_Messages — CHỈ ĐỌC (SELECT), không xoá/sửa mail.</summary>
public sealed class MailModel : PageModel
{
    public const int PageSize = 20;
    public const string Conn = "Db_Tank";

    private readonly SqlDatabase _db;
    private readonly string _itemImageBase;

    public MailModel(SqlDatabase db, IConfiguration cfg)
    {
        _db = db;
        _itemImageBase = (cfg["Vng:ItemImageBase"] ?? "https://gunny.vcdn.vn/").TrimEnd('/') + "/";
    }

    // ---------------- Bộ lọc (query string, giữ khi phân trang / xem chi tiết) ----------------
    [BindProperty(SupportsGet = true, Name = "user")] public string? UserFilter { get; set; }
    [BindProperty(SupportsGet = true, Name = "q")] public string? Q { get; set; }
    [BindProperty(SupportsGet = true, Name = "type")] public int? Type { get; set; }
    [BindProperty(SupportsGet = true, Name = "read")] public int? Read { get; set; }
    [BindProperty(SupportsGet = true, Name = "from")] public string? From { get; set; }
    [BindProperty(SupportsGet = true, Name = "to")] public string? To { get; set; }
    [BindProperty(SupportsGet = true, Name = "del")] public int? Del { get; set; }
    [BindProperty(SupportsGet = true, Name = "sort")] public string Sort { get; set; } = "time";
    [BindProperty(SupportsGet = true, Name = "sel")] public int? Sel { get; set; }

    /// <summary>
    /// Trang hiện tại. Đọc trực tiếp từ query string (không dùng [BindProperty] cho "page"):
    /// Razor Pages đã dùng route value tên "page" cho route của trang ("/Mail"), nên route value
    /// sẽ lấn query string và làm bind thất bại.
    /// </summary>
    public int PageIndex { get; private set; } = 1;

    // ---------------- Kết quả ----------------
    public List<MailRow> Mails { get; } = new();
    public List<MailTypeStat> TypeStats { get; } = new();
    public List<MailTypeStat> TypeOptions { get; } = new();
    public MailRow? Detail { get; private set; }
    public List<MailAnnex> DetailAnnexes { get; } = new();
    public string? DetailError { get; private set; }
    public string? Error { get; private set; }
    public int Total { get; private set; }
    public int UnreadTotal { get; private set; }
    public int DeletedTotal { get; private set; }
    public long SumGold { get; private set; }
    public long SumMoney { get; private set; }
    public long SumGiftToken { get; private set; }
    public Pager Pager { get; private set; } = new();

    public bool IncludeDeleted => Del == 1;
    public bool HasFilter =>
        !string.IsNullOrWhiteSpace(UserFilter) || !string.IsNullOrWhiteSpace(Q) || Type.HasValue ||
        Read.HasValue || !string.IsNullOrWhiteSpace(From) || !string.IsNullOrWhiteSpace(To) || IncludeDeleted ||
        !string.Equals(Sort, "time", StringComparison.OrdinalIgnoreCase);

    private static readonly Dictionary<string, string> SortOrders = new(StringComparer.OrdinalIgnoreCase)
    {
        ["time"] = "m.SendTime DESC, m.ID DESC",
        ["id"] = "m.ID DESC",
        ["old"] = "m.SendTime ASC, m.ID ASC"
    };

    // ---------------- Nhãn loại thư ----------------
    public static string TypeLabel(int type) => type switch
    {
        51 => "Thư GM (vật phẩm/Award)",
        8 => "Quà VIP/Event",
        83 => "Thư hệ thống/GM",
        _ => "Loại " + type
    };

    public static string TypeBadgeClass(int type) => type switch
    {
        51 => "mail-type-gm",
        8 => "mail-type-vip",
        83 => "mail-type-system",
        _ => "mail-type-other"
    };

    public async Task OnGetAsync()
    {
        UserFilter = (UserFilter ?? "").Trim();
        Q = (Q ?? "").Trim();
        Sort = SortOrders.ContainsKey(Sort ?? "") ? Sort!.ToLowerInvariant() : "time";

        // Razor Pages dùng route value "page" cho route trang → đọc trực tiếp query string.
        PageIndex = int.TryParse(Request.Query["page"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var pageQuery) && pageQuery > 0
            ? pageQuery
            : 1;

        var fromDate = ParseDate(From, out var fromInvalid);
        var toDate = ParseDate(To, out var toInvalid);
        if (fromInvalid || toInvalid)
            Error = "Ngày lọc không hợp lệ (định dạng cần là yyyy-MM-dd) — tạm bỏ qua lọc ngày.";

        await LoadTypeOptionsAsync();

        var (where, pars) = BuildWhere(fromDate, toDate);

        var summary = await _db.QueryAsync(Conn,
            "SELECT COUNT(*) AS Total, " +
            "SUM(CASE WHEN m.IsRead = 0 THEN 1 ELSE 0 END) AS Unread, " +
            "SUM(CASE WHEN m.IsDelete = 1 THEN 1 ELSE 0 END) AS Deleted, " +
            "ISNULL(SUM(m.Gold), 0) AS SumGold, ISNULL(SUM(m.Money), 0) AS SumMoney, ISNULL(SUM(m.GiftToken), 0) AS SumGiftToken " +
            "FROM dbo.User_Messages m " + JoinUserTables + where,
            pars, 1, 30);
        if (!summary.Ok)
        {
            Error = summary.Error;
            return;
        }
        if (summary.Rows.Count > 0)
        {
            var row = summary.Rows[0];
            Total = ParseCellInt(row, 0);
            UnreadTotal = ParseCellInt(row, 1);
            DeletedTotal = ParseCellInt(row, 2);
            SumGold = ParseCellLong(row, 3);
            SumMoney = ParseCellLong(row, 4);
            SumGiftToken = ParseCellLong(row, 5);
        }

        var types = await _db.QueryAsync(Conn,
            "SELECT m.Type, COUNT(*) AS N, SUM(CASE WHEN m.IsRead = 0 THEN 1 ELSE 0 END) AS Unread " +
            "FROM dbo.User_Messages m " + JoinUserTables + where + " GROUP BY m.Type ORDER BY m.Type",
            pars, 50, 30);
        if (!types.Ok) Error = types.Error;
        else foreach (var row in types.Rows)
            TypeStats.Add(new MailTypeStat
            {
                Type = ParseCellInt(row, 0),
                Count = ParseCellInt(row, 1),
                Unread = ParseCellInt(row, 2)
            });

        PageIndex = Pager.Clamp(PageIndex, Total, PageSize);

        var listPars = new Dictionary<string, object?>(pars) { ["skip"] = (PageIndex - 1) * PageSize, ["take"] = PageSize };
        var list = await _db.QueryAsync(Conn,
            "SELECT m.ID, m.SenderID, ISNULL(m.Sender, N'') AS Sender, m.ReceiverID, ISNULL(m.Receiver, N'') AS Receiver, " +
            "r.NickName AS ReceiverNick, s.NickName AS SenderNick, ISNULL(m.Title, N'') AS Title, ISNULL(m.Content, N'') AS Content, " +
            "m.SendTime, m.IsRead, m.IsDelR, m.IfDelS, m.IsDelete, m.IsExist, m.Type, m.Gold, m.Money, m.GiftToken, m.ValidDate, " +
            "m.SendDate, ISNULL(m.Remark, N'') AS Remark, ISNULL(m.AnnexRemark, N'') AS AnnexRemark, " + AnnexCountSql + " " +
            "FROM dbo.User_Messages m " + JoinUserTables + where +
            " ORDER BY " + SortOrders[Sort] + " OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY",
            listPars, PageSize, 30);
        if (!list.Ok)
        {
            Error = list.Error;
            return;
        }
        foreach (var row in list.Rows) Mails.Add(MailRow.From(row));

        Pager = new Pager
        {
            Page = PageIndex,
            PageSize = PageSize,
            Total = Total,
            Path = "/Mail",
            PageParam = "page",
            Query = new Dictionary<string, string?>
            {
                ["user"] = UserFilter,
                ["q"] = Q,
                ["type"] = Type?.ToString(),
                ["read"] = Read?.ToString(),
                ["from"] = From,
                ["to"] = To,
                ["del"] = IncludeDeleted ? "1" : null,
                ["sort"] = Sort
            }
        };

        await LoadDetailAsync();
    }

    // ---------------- Truy vấn dùng chung ----------------
    private const string JoinUserTables =
        "LEFT JOIN dbo.Sys_Users_Detail r ON r.UserID = m.ReceiverID " +
        "LEFT JOIN dbo.Sys_Users_Detail s ON s.UserID = m.SenderID ";

    /// <summary>
    /// Số annex của thư: đếm slot có AnnexN hoặc AnnexNName.
    /// AnnexN là TemplateID dạng chuỗi; kiểm tra số bằng LIKE (compat 100 — không có TRY_CONVERT).
    /// </summary>
    private const string AnnexCountSql =
        "(CASE WHEN NULLIF(LTRIM(RTRIM(ISNULL(m.Annex1, N''))), N'') IS NOT NULL OR NULLIF(LTRIM(RTRIM(ISNULL(m.Annex1Name, N''))), N'') IS NOT NULL THEN 1 ELSE 0 END + " +
        "CASE WHEN NULLIF(LTRIM(RTRIM(ISNULL(m.Annex2, N''))), N'') IS NOT NULL OR NULLIF(LTRIM(RTRIM(ISNULL(m.Annex2Name, N''))), N'') IS NOT NULL THEN 1 ELSE 0 END + " +
        "CASE WHEN NULLIF(LTRIM(RTRIM(ISNULL(m.Annex3, N''))), N'') IS NOT NULL OR NULLIF(LTRIM(RTRIM(ISNULL(m.Annex3Name, N''))), N'') IS NOT NULL THEN 1 ELSE 0 END + " +
        "CASE WHEN NULLIF(LTRIM(RTRIM(ISNULL(m.Annex4, N''))), N'') IS NOT NULL OR NULLIF(LTRIM(RTRIM(ISNULL(m.Annex4Name, N''))), N'') IS NOT NULL THEN 1 ELSE 0 END + " +
        "CASE WHEN NULLIF(LTRIM(RTRIM(ISNULL(m.Annex5, N''))), N'') IS NOT NULL OR NULLIF(LTRIM(RTRIM(ISNULL(m.Annex5Name, N''))), N'') IS NOT NULL THEN 1 ELSE 0 END) AS AnnexCount";

    private (string Where, Dictionary<string, object?> Params) BuildWhere(DateTime? fromDate, DateTime? toDate)
    {
        var conditions = new List<string>();
        var pars = new Dictionary<string, object?>();

        if (!IncludeDeleted) conditions.Add("m.IsDelete = 0");

        if (!string.IsNullOrWhiteSpace(UserFilter))
        {
            conditions.Add("(CAST(m.ReceiverID AS nvarchar(20)) = @user OR m.Receiver LIKE @userLike OR r.NickName LIKE @userLike)");
            pars["user"] = UserFilter;
            pars["userLike"] = "%" + UserFilter + "%";
        }
        if (!string.IsNullOrWhiteSpace(Q))
        {
            conditions.Add("(m.Title LIKE @qLike OR m.Content LIKE @qLike)");
            pars["qLike"] = "%" + Q + "%";
        }
        if (Type.HasValue)
        {
            conditions.Add("m.Type = @type");
            pars["type"] = Type.Value;
        }
        if (Read.HasValue)
        {
            conditions.Add("m.IsRead = @isRead");
            pars["isRead"] = Read.Value == 1;
        }
        if (fromDate.HasValue)
        {
            conditions.Add("m.SendTime >= @fromDate");
            pars["fromDate"] = fromDate.Value;
        }
        if (toDate.HasValue)
        {
            // "đến ngày" lấy trọn ngày → so sánh < ngày kế tiếp.
            conditions.Add("m.SendTime < @toDate");
            pars["toDate"] = toDate.Value.AddDays(1);
        }

        return (conditions.Count == 0 ? "" : " WHERE " + string.Join(" AND ", conditions), pars);
    }

    // ---------------- Chi tiết + annex ----------------
    private async Task LoadDetailAsync()
    {
        if (!Sel.HasValue || Sel.Value <= 0) return;

        var detail = await _db.QueryAsync(Conn,
            "SELECT m.ID, m.SenderID, ISNULL(m.Sender, N'') AS Sender, m.ReceiverID, ISNULL(m.Receiver, N'') AS Receiver, " +
            "r.NickName AS ReceiverNick, s.NickName AS SenderNick, ISNULL(m.Title, N'') AS Title, ISNULL(m.Content, N'') AS Content, " +
            "m.SendTime, m.IsRead, m.IsDelR, m.IfDelS, m.IsDelete, m.IsExist, m.Type, m.Gold, m.Money, m.GiftToken, m.ValidDate, " +
            "m.SendDate, ISNULL(m.Remark, N'') AS Remark, ISNULL(m.AnnexRemark, N'') AS AnnexRemark, " + AnnexCountSql + ", " +
            "ISNULL(m.Annex1, N'') AS Annex1, ISNULL(m.Annex2, N'') AS Annex2, ISNULL(m.Annex3, N'') AS Annex3, " +
            "ISNULL(m.Annex4, N'') AS Annex4, ISNULL(m.Annex5, N'') AS Annex5, " +
            "ISNULL(m.Annex1Name, N'') AS Annex1Name, ISNULL(m.Annex2Name, N'') AS Annex2Name, ISNULL(m.Annex3Name, N'') AS Annex3Name, " +
            "ISNULL(m.Annex4Name, N'') AS Annex4Name, ISNULL(m.Annex5Name, N'') AS Annex5Name " +
            "FROM dbo.User_Messages m " + JoinUserTables + " WHERE m.ID = @id",
            new Dictionary<string, object?> { ["id"] = Sel.Value }, 1, 30);
        if (!detail.Ok)
        {
            DetailError = detail.Error;
            return;
        }
        if (detail.Rows.Count == 0)
        {
            DetailError = $"Không tìm thấy thư ID={Sel.Value}.";
            return;
        }

        Detail = MailRow.From(detail.Rows[0]);
        await LoadAnnexesAsync(Detail);
    }

    private sealed class ShopItemRow
    {
        public int TemplateId { get; set; }
        public string Name { get; set; } = "";
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = "";
        public string Pic { get; set; } = "";
        public int NeedSex { get; set; }
    }

    /// <summary>
    /// Tra cứu Annex1..5: (1) TemplateID trong Shop_Goods, (2) với thư Type=51 còn thử ItemID trong
    /// Sys_Users_Goods (SP_Admin_SendUserItem ghi ItemID vào Annex1), (3) nếu không khớp ID thì khớp theo tên.
    /// </summary>
    private async Task LoadAnnexesAsync(MailRow mail)
    {
        var slots = new List<(int Slot, string Raw, string MailName)>();
        for (var i = 0; i < 5; i++)
        {
            var raw = (mail.AnnexRaw.ElementAtOrDefault(i) ?? "").Trim();
            var name = (mail.AnnexNames.ElementAtOrDefault(i) ?? "").Trim();
            if (raw.Length > 0 || name.Length > 0) slots.Add((i + 1, raw, name));
        }
        if (slots.Count == 0) return;

        var parsedIds = new List<int?>();
        foreach (var slot in slots)
            parsedIds.Add(int.TryParse(slot.Raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : null);

        var shopById = new Dictionary<int, ShopItemRow>();
        var templateIds = parsedIds.Where(v => v.HasValue).Select(v => v!.Value).Distinct().ToList();
        if (templateIds.Count > 0)
        {
            var pars = new Dictionary<string, object?>();
            var inClause = BuildInClause(templateIds, pars, "tpl");
            var result = await _db.QueryAsync("Db_Tank_All",
                "SELECT g.TemplateID, g.Name, g.CategoryID, ISNULL(c.Name, N'') AS CategoryName, ISNULL(g.Pic, N'') AS Pic, g.NeedSex " +
                "FROM dbo.Shop_Goods g LEFT JOIN Db_Gm.dbo.Category c ON c.CategoryID = g.CategoryID " +
                "WHERE g.TemplateID IN (" + inClause + ")",
                pars, Math.Max(5, templateIds.Count), 30);
            if (result.Ok)
                foreach (var row in result.Rows)
                {
                    var item = ShopItemRowFrom(row);
                    shopById[item.TemplateId] = item;
                }
        }

        // Thư GM (Type 51) do SP_Admin_SendUserItem tạo: Annex1 có thể là ItemID của Sys_Users_Goods.
        var itemById = new Dictionary<int, ShopItemRow>();
        if (mail.Type == 51)
        {
            var unresolvedIds = new List<int>();
            for (var i = 0; i < slots.Count; i++)
                if (parsedIds[i].HasValue && !shopById.ContainsKey(parsedIds[i]!.Value))
                    unresolvedIds.Add(parsedIds[i]!.Value);
            if (unresolvedIds.Count > 0)
            {
                var pars = new Dictionary<string, object?>();
                var inClause = BuildInClause(unresolvedIds.Distinct().ToList(), pars, "item");
                var result = await _db.QueryAsync(Conn,
                    "SELECT g.ItemID, sg.TemplateID, sg.Name, sg.CategoryID, ISNULL(c.Name, N'') AS CategoryName, " +
                    "ISNULL(sg.Pic, N'') AS Pic, sg.NeedSex " +
                    "FROM dbo.Sys_Users_Goods g " +
                    "LEFT JOIN Db_Tank_All.dbo.Shop_Goods sg ON sg.TemplateID = g.TemplateID " +
                    "LEFT JOIN Db_Gm.dbo.Category c ON c.CategoryID = sg.CategoryID " +
                    "WHERE g.ItemID IN (" + inClause + ")",
                    pars, Math.Max(5, unresolvedIds.Count), 30);
                if (result.Ok)
                    foreach (var row in result.Rows)
                    {
                        var itemId = ParseCellInt(row, 0);
                        if (ParseCellInt(row, 1) <= 0) continue;
                        itemById[itemId] = ShopItemRowFrom(row, 1);
                    }
            }
        }

        // Khớp theo tên (một số dữ liệu cũ ghi Annex không khớp ID nhưng AnnexNName đúng tên Shop_Goods).
        var nameToShop = new Dictionary<string, ShopItemRow>(StringComparer.OrdinalIgnoreCase);
        var unresolvedNames = new List<string>();
        for (var i = 0; i < slots.Count; i++)
        {
            var idResolved = parsedIds[i].HasValue &&
                             (shopById.ContainsKey(parsedIds[i]!.Value) || itemById.ContainsKey(parsedIds[i]!.Value));
            if (!idResolved && slots[i].MailName.Length > 0) unresolvedNames.Add(slots[i].MailName);
        }
        if (unresolvedNames.Count > 0)
        {
            var pars = new Dictionary<string, object?>();
            var inClause = BuildInClause(unresolvedNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList(), pars, "nm");
            var result = await _db.QueryAsync("Db_Tank_All",
                "SELECT g.TemplateID, g.Name, g.CategoryID, ISNULL(c.Name, N'') AS CategoryName, ISNULL(g.Pic, N'') AS Pic, g.NeedSex " +
                "FROM dbo.Shop_Goods g LEFT JOIN Db_Gm.dbo.Category c ON c.CategoryID = g.CategoryID " +
                "WHERE g.Name IN (" + inClause + ") ORDER BY g.TemplateID",
                pars, 50, 30);
            if (result.Ok)
                foreach (var row in result.Rows)
                {
                    var item = ShopItemRowFrom(row);
                    if (!nameToShop.ContainsKey(item.Name)) nameToShop[item.Name] = item;
                }
        }

        for (var i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            var annex = new MailAnnex
            {
                Slot = slot.Slot,
                Raw = slot.Raw,
                MailName = slot.MailName
            };

            ShopItemRow? shop = null;
            if (parsedIds[i].HasValue)
            {
                var id = parsedIds[i]!.Value;
                if (shopById.TryGetValue(id, out var byTemplate))
                {
                    shop = byTemplate;
                    annex.TemplateId = byTemplate.TemplateId;
                    annex.Source = "template-id";
                }
                else if (itemById.TryGetValue(id, out var byItem))
                {
                    shop = byItem;
                    annex.TemplateId = byItem.TemplateId;
                    annex.ItemId = id;
                    annex.Source = "item-id";
                }
            }
            if (shop == null && slot.MailName.Length > 0 && nameToShop.TryGetValue(slot.MailName, out var byName))
            {
                shop = byName;
                annex.TemplateId = byName.TemplateId;
                annex.Source = "mail-name";
            }

            if (shop != null)
            {
                annex.ShopName = shop.Name;
                annex.CategoryId = shop.CategoryId;
                annex.CategoryName = shop.CategoryName;
                annex.ImageUrl = BuildItemImageUrl(shop.CategoryId, shop.Pic, shop.NeedSex);
                annex.Name = slot.MailName.Length > 0 ? slot.MailName : shop.Name;
            }
            else
            {
                annex.Name = slot.MailName.Length > 0
                    ? slot.MailName
                    : (slot.Raw.Length > 0 ? "TemplateID " + slot.Raw : "(không rõ)");
            }
            DetailAnnexes.Add(annex);
        }
    }

    private static ShopItemRow ShopItemRowFrom(string?[] row, int offset = 0) => new()
    {
        TemplateId = ParseCellInt(row, offset),
        Name = CellText(row, offset + 1),
        CategoryId = ParseCellInt(row, offset + 2),
        CategoryName = CellText(row, offset + 3),
        Pic = CellText(row, offset + 4),
        NeedSex = ParseCellInt(row, offset + 5)
    };

    private static string BuildInClause(IReadOnlyList<int> values, Dictionary<string, object?> pars, string prefix)
    {
        var names = new List<string>(values.Count);
        for (var i = 0; i < values.Count; i++)
        {
            var name = prefix + i;
            pars[name] = values[i];
            names.Add("@" + name);
        }
        return string.Join(", ", names);
    }

    private static string BuildInClause(IReadOnlyList<string> values, Dictionary<string, object?> pars, string prefix)
    {
        var names = new List<string>(values.Count);
        for (var i = 0; i < values.Count; i++)
        {
            var name = prefix + i;
            pars[name] = values[i];
            names.Add("@" + name);
        }
        return string.Join(", ", names);
    }

    // ---------------- Link giữ bộ lọc ----------------
    public string DetailUrl(int mailId) => BuildUrl(mailId);

    public string CloseDetailUrl() => BuildUrl(null);

    private string BuildUrl(int? sel)
    {
        var parts = new List<string>();
        void Add(string key, string? value)
        {
            if (!string.IsNullOrEmpty(value)) parts.Add(Uri.EscapeDataString(key) + "=" + Uri.EscapeDataString(value));
        }
        Add("user", UserFilter);
        Add("q", Q);
        Add("type", Type?.ToString());
        Add("read", Read?.ToString());
        Add("from", From);
        Add("to", To);
        if (IncludeDeleted) Add("del", "1");
        Add("sort", Sort);
        if (PageIndex > 1) Add("page", PageIndex.ToString());
        if (sel.HasValue) Add("sel", sel.Value.ToString());
        return "/Mail" + (parts.Count > 0 ? "?" + string.Join("&", parts) : "");
    }

    // ---------------- Helpers ----------------
    private async Task LoadTypeOptionsAsync()
    {
        var result = await _db.QueryAsync(Conn,
            "SELECT Type, COUNT(*) FROM dbo.User_Messages GROUP BY Type ORDER BY Type", 50, 30);
        if (!result.Ok) return;
        foreach (var row in result.Rows)
            TypeOptions.Add(new MailTypeStat { Type = ParseCellInt(row, 0), Count = ParseCellInt(row, 1) });
    }

    private static DateTime? ParseDate(string? value, out bool invalid)
    {
        invalid = false;
        var text = (value ?? "").Trim();
        if (text.Length == 0) return null;
        if (DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return dt;
        invalid = true;
        return null;
    }

    private static string CellText(string?[] row, int index) => row.ElementAtOrDefault(index) ?? "";

    private static int ParseCellInt(string?[] row, int index) =>
        int.TryParse(CellText(row, index), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;

    private static long ParseCellLong(string?[] row, int index) =>
        long.TryParse(CellText(row, index), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;

    /// <summary>Ảnh item theo cấu trúc resource Gunny (giống Html.ItemImage / Admincp cũ).</summary>
    private string BuildItemImageUrl(int categoryId, string? pic, int needSex)
    {
        if (string.IsNullOrWhiteSpace(pic)) return "";
        pic = pic.Trim().ToLowerInvariant();
        var sex = needSex == 1 ? "m" : "f";
        var path = categoryId switch
        {
            1 => $"image/equip/{sex}/head/{pic}/icon_1.png",
            2 => $"image/equip/{sex}/glass/{pic}/icon_1.png",
            3 => $"image/equip/{sex}/hair/{pic}/icon_1.png",
            4 => $"image/equip/{sex}/eff/{pic}/icon_1.png",
            5 => $"image/equip/{sex}/cloth/{pic}/icon_1.png",
            6 => $"image/equip/{sex}/face/{pic}/icon_1.png",
            13 => $"image/equip/{sex}/suits/{pic}/icon_1.png",
            7 or 27 or 64 => $"image/arm/{pic}/00.png",
            8 or 28 => $"image/equip/armlet/{pic}/icon.png",
            9 or 29 => $"image/equip/ring/{pic}/icon.png",
            11 or 20 or 24 or 30 or 34 or 35 or 36 or 37 or 40 or 53 or 60 or 61 or 62 or 68 or 69 or 71 or 72
                => $"image/unfrightprop/{pic}/icon.png",
            12 => $"image/task/{pic}/icon.png",
            14 => $"image/equip/necklace/{pic}/icon.png",
            15 => $"image/equip/wing/{pic}/icon.png",
            16 => $"image/specialprop/chatball/{pic}/icon.png",
            17 or 31 => $"image/equip/offhand/{pic}/icon.png",
            18 or 66 => $"image/cardbox/{pic}/icon.png",
            19 => $"image/equip/recover/{pic}/icon.png",
            26 => $"image/card/{pic}/icon.jpg",
            25 => $"image/gift/{pic}/icon.png",
            50 => $"image/petequip/arm/{pic}/icon.png",
            51 => $"image/petequip/hat/{pic}/icon.png",
            52 => $"image/petequip/cloth/{pic}/icon.png",
            80 => $"image/prop/{pic}/icon.png",
            _ => ""
        };
        return string.IsNullOrEmpty(path) ? "" : _itemImageBase + path;
    }
}
