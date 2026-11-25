using System.Globalization;
using GunnyPanel.Pages.Shared;
using GunnySuite.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GunnyPanel.Pages;

/// <summary>Một thể loại trong bộ lọc (kèm số item).</summary>
public sealed class ShopCategoryOption
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Count { get; set; }
}

/// <summary>Một mức phẩm chất (Quality) trong bộ lọc (kèm số item).</summary>
public sealed class ShopQualityOption
{
    public int Quality { get; set; }
    public int Count { get; set; }
}

/// <summary>Một dòng item trong lưới kết quả (đủ dữ liệu cho card + ảnh).</summary>
public sealed class ShopItemRow
{
    public int TemplateId { get; set; }
    public string Name { get; set; } = "";
    public string Remark { get; set; } = "";
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public int Level { get; set; }
    public int Quality { get; set; }
    public int MaxCount { get; set; }
    public int NeedLevel { get; set; }
    public int NeedSex { get; set; }
    public string Pic { get; set; } = "";
    public string ImageUrl { get; set; } = "";
    public bool SupportsGold { get; set; }
    public bool SupportsStats { get; set; }
    public bool SupportsStrength { get; set; }
}

/// <summary>Chi tiết đầy đủ một template trong Shop_Goods (chỉ đọc).</summary>
public sealed class ShopItemDetail
{
    public int TemplateId { get; set; }
    public string Name { get; set; } = "";
    public string Remark { get; set; } = "";
    public string Description { get; set; } = "";
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public int Level { get; set; }
    public int Quality { get; set; }
    public int Attack { get; set; }
    public int Defence { get; set; }
    public int Agility { get; set; }
    public int Luck { get; set; }
    public int MaxCount { get; set; }
    public int NeedLevel { get; set; }
    public int NeedSex { get; set; }
    public int Valid { get; set; }
    public int Count { get; set; }
    public int SuitId { get; set; }
    public int FloorPrice { get; set; }
    public int ReclaimValue { get; set; }
    public int ReclaimType { get; set; }
    public int RefineryLevel { get; set; }
    public int BindType { get; set; }
    public int FusionType { get; set; }
    public int FusionRate { get; set; }
    public int FusionNeedRate { get; set; }
    public string Colors { get; set; } = "";
    public string Hole { get; set; } = "";
    public string AddTime { get; set; } = "";
    public string Pic { get; set; } = "";
    public string ImageUrl { get; set; } = "";
    public bool SupportsGold { get; set; }
    public bool SupportsStats { get; set; }
    public bool SupportsStrength { get; set; }

    /// <summary>Các cờ Can* hiển thị dạng badge Bật/Tắt.</summary>
    public List<(string Label, bool On)> Flags { get; } = new();

    /// <summary>Toàn bộ cột kỹ thuật của Shop_Goods (cho khối "Mở rộng").</summary>
    public List<(string Column, string Value)> RawFields { get; } = new();

    public string NeedSexText => NeedSex switch
    {
        0 => "Cả hai / không giới hạn",
        1 => "Nam",
        2 => "Nữ",
        _ => "Khác (" + NeedSex + ")"
    };
}

/// <summary>
/// Trang /Shop — Shop Explorer: duyệt 15.060 item trong Db_Tank_All.dbo.Shop_Goods.
/// Chỉ đọc (SELECT), không có form POST, không ghi DB.
/// </summary>
public sealed class ShopModel : PageModel
{
    public const int PageSize = 24;
    public const string Conn = "Db_Tank_All";

    // Đúc vàng: Nón (1), Áo/Quần áo (5), Vũ khí (7).
    private static readonly HashSet<int> GoldCategories = new() { 1, 5, 7 };
    // Cộng 4 chỉ số: Nón (1), Áo (5), Vũ khí (7), Vòng tay (8), Nhẫn (9/29), Vũ khí phụ (17).
    private static readonly HashSet<int> StatCategories = new() { 1, 5, 7, 8, 9, 17, 29 };
    // Cấp độ (StrengthenLevel 0–15): Nón (1), Áo (5), Vũ khí (7), Vũ khí phụ (17).
    private static readonly HashSet<int> StrengthCategories = new() { 1, 5, 7, 17 };

    private static readonly Dictionary<string, string> SortOrders = new(StringComparer.OrdinalIgnoreCase)
    {
        ["tid"] = "g.TemplateID",
        ["name"] = "g.Name, g.TemplateID",
        ["level"] = "g.Level DESC, g.TemplateID",
        ["quality"] = "g.Quality DESC, g.TemplateID",
        ["cat"] = "g.CategoryID, g.TemplateID"
    };

    private const string CategoryNameExpr =
        "COALESCE(NULLIF(LTRIM(RTRIM(c.Name)), N'') COLLATE DATABASE_DEFAULT, " +
        "NULLIF(LTRIM(RTRIM(s.Name)), N'') COLLATE DATABASE_DEFAULT, " +
        "N'Category ' + CAST(g.CategoryID AS nvarchar(10)))";

    private const string DetailSql =
        "SELECT TOP 1 g.TemplateID, g.Name, g.Remark, g.Description, g.CategoryID, " +
        CategoryNameExpr + " AS CategoryName, " +
        "g.Attack, g.Defence, g.Agility, g.Luck, g.Level, g.Quality, g.Pic, g.MaxCount, " +
        "g.NeedSex, g.NeedLevel, CAST(g.CanStrengthen AS int) AS CanStrengthen, CAST(g.CanCompose AS int) AS CanCompose, " +
        "CAST(g.CanDrop AS int) AS CanDrop, CAST(g.CanEquip AS int) AS CanEquip, CAST(g.CanUse AS int) AS CanUse, " +
        "CAST(g.CanDelete AS int) AS CanDelete, g.Script, g.Data, g.Colors, " +
        "g.Property1, g.Property2, g.Property3, g.Property4, g.Property5, g.Property6, g.Property7, g.Property8, " +
        "g.Valid, g.Count, CONVERT(nvarchar(19), g.AddTime, 120) AS AddTime, g.BindType, g.FusionType, g.FusionRate, " +
        "g.FusionNeedRate, g.Hole, g.RefineryLevel, g.ReclaimValue, g.ReclaimType, g.CanRecycle, g.FloorPrice, " +
        "g.SuitId, g.CanTransfer " +
        "FROM dbo.Shop_Goods g " +
        "LEFT JOIN Db_Gm.dbo.Category c ON c.CategoryID = g.CategoryID AND c.CategoryID > 0 " +
        "LEFT JOIN dbo.Shop_Goods_Categorys s ON s.ID = g.CategoryID WHERE g.TemplateID = @tpl";

    private readonly SqlDatabase _db;
    private readonly string _itemImageBase;

    public ShopModel(SqlDatabase db, IConfiguration cfg)
    {
        _db = db;
        _itemImageBase = (cfg["Vng:ItemImageBase"] ?? "https://gunny.vcdn.vn/").TrimEnd('/') + "/";
    }

    [BindProperty(SupportsGet = true, Name = "q")] public string? Q { get; set; }
    [BindProperty(SupportsGet = true, Name = "cat")] public int? Cat { get; set; }
    [BindProperty(SupportsGet = true, Name = "quality")] public int? Quality { get; set; }
    [BindProperty(SupportsGet = true, Name = "sort")] public string Sort { get; set; } = "tid";
    [BindProperty(SupportsGet = true, Name = "sel")] public int? Sel { get; set; }

    /// <summary>
    /// Trang hiện tại. Đọc thủ công từ query string qua [FromQuery] thay vì [BindProperty]:
    /// Razor Pages dùng route value tên "page" (đường dẫn trang) nên nếu bind property sẽ bị route value che mất.
    /// </summary>
    public int PageIndex { get; private set; } = 1;

    public List<ShopItemRow> Items { get; } = new();
    public List<ShopCategoryOption> Categories { get; } = new();
    public List<ShopQualityOption> Qualities { get; } = new();
    public int Total { get; private set; }
    public string? Error { get; private set; }
    public ShopItemDetail? Detail { get; private set; }
    public string? DetailError { get; private set; }
    public Pager Pager { get; private set; } = new();

    public int ShownFrom => Total == 0 ? 0 : (PageIndex - 1) * PageSize + 1;
    public int ShownTo => Math.Min(Total, PageIndex * PageSize);

    public async Task OnGetAsync([FromQuery(Name = "page")] int page = 1)
    {
        PageIndex = page > 0 ? page : 1;
        var q = (Q ?? "").Trim();
        if (q.Length > 100) q = q[..100];
        Q = q;
        Sort = SortOrders.ContainsKey(Sort ?? "") ? Sort!.ToLowerInvariant() : "tid";
        Cat = Cat.HasValue && Cat.Value > 0 ? Cat : null;
        Quality = Quality.HasValue && Quality.Value > 0 ? Quality : null;

        await LoadFilterOptionsAsync();
        await LoadDetailAsync();

        var where = "WHERE 1 = 1";
        var pars = new Dictionary<string, object?>();
        if (q.Length > 0)
        {
            where += " AND (g.Name LIKE @like OR g.Remark LIKE @like OR CAST(g.TemplateID AS nvarchar(20)) = @q)";
            pars["like"] = "%" + q + "%";
            pars["q"] = q;
        }
        if (Cat.HasValue)
        {
            where += " AND g.CategoryID = @cat";
            pars["cat"] = Cat.Value;
        }
        if (Quality.HasValue)
        {
            where += " AND g.Quality = @quality";
            pars["quality"] = Quality.Value;
        }

        var count = await _db.QueryAsync(Conn, "SELECT COUNT(*) FROM dbo.Shop_Goods g " + where, pars, 1, 20);
        if (!count.Ok)
        {
            Error = "Lỗi đếm dữ liệu Shop_Goods: " + count.Error;
            return;
        }
        Total = UserRow.ParseInt(count.FirstCell());
        PageIndex = Pager.Clamp(PageIndex, Total, PageSize);

        pars["skip"] = (PageIndex - 1) * PageSize;
        pars["take"] = PageSize;
        var list = await _db.QueryAsync(Conn,
            "SELECT g.TemplateID, g.Name, g.Remark, g.CategoryID, " + CategoryNameExpr + " AS CategoryName, " +
            "g.Level, g.Quality, g.MaxCount, g.NeedLevel, g.NeedSex, g.Pic " +
            "FROM dbo.Shop_Goods g " +
            "LEFT JOIN Db_Gm.dbo.Category c ON c.CategoryID = g.CategoryID AND c.CategoryID > 0 " +
            "LEFT JOIN dbo.Shop_Goods_Categorys s ON s.ID = g.CategoryID " +
            where + " ORDER BY " + SortOrders[Sort] + " OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY",
            pars, PageSize, 30);
        if (!list.Ok)
        {
            Error = "Lỗi tải danh sách Shop_Goods: " + list.Error;
            return;
        }

        foreach (var row in list.Rows)
        {
            var catId = UserRow.ParseInt(row.ElementAtOrDefault(3));
            var pic = row.ElementAtOrDefault(10) ?? "";
            var needSex = UserRow.ParseInt(row.ElementAtOrDefault(9));
            Items.Add(new ShopItemRow
            {
                TemplateId = UserRow.ParseInt(row.ElementAtOrDefault(0)),
                Name = row.ElementAtOrDefault(1) ?? "",
                Remark = row.ElementAtOrDefault(2) ?? "",
                CategoryId = catId,
                CategoryName = row.ElementAtOrDefault(4) ?? ("Category " + catId),
                Level = UserRow.ParseInt(row.ElementAtOrDefault(5)),
                Quality = UserRow.ParseInt(row.ElementAtOrDefault(6)),
                MaxCount = UserRow.ParseInt(row.ElementAtOrDefault(7)),
                NeedLevel = UserRow.ParseInt(row.ElementAtOrDefault(8)),
                NeedSex = needSex,
                Pic = pic,
                ImageUrl = BuildItemImageUrl(catId, pic, needSex),
                SupportsGold = GoldCategories.Contains(catId),
                SupportsStats = StatCategories.Contains(catId),
                SupportsStrength = StrengthCategories.Contains(catId)
            });
        }

        Pager = new Pager
        {
            Page = PageIndex,
            PageSize = PageSize,
            Total = Total,
            Path = "/Shop",
            PageParam = "page",
            Query = new Dictionary<string, string?>
            {
                ["q"] = q,
                ["cat"] = Cat?.ToString(),
                ["quality"] = Quality?.ToString(),
                ["sort"] = Sort,
                ["sel"] = Sel?.ToString()
            }
        };
    }

    /// <summary>Nạp 2 bộ lọc: Category (kèm số item) và Quality (kèm số item).</summary>
    private async Task LoadFilterOptionsAsync()
    {
        var cats = await _db.QueryAsync(Conn,
            "SELECT g.CategoryID, MAX(" + CategoryNameExpr + ") AS CategoryName, COUNT(*) AS N " +
            "FROM dbo.Shop_Goods g " +
            "LEFT JOIN Db_Gm.dbo.Category c ON c.CategoryID = g.CategoryID AND c.CategoryID > 0 " +
            "LEFT JOIN dbo.Shop_Goods_Categorys s ON s.ID = g.CategoryID " +
            "GROUP BY g.CategoryID ORDER BY g.CategoryID",
            null, 200, 30);
        if (cats.Ok)
        {
            foreach (var row in cats.Rows)
            {
                var id = UserRow.ParseInt(row.ElementAtOrDefault(0));
                Categories.Add(new ShopCategoryOption
                {
                    Id = id,
                    Name = row.ElementAtOrDefault(1) ?? ("Category " + id),
                    Count = UserRow.ParseInt(row.ElementAtOrDefault(2))
                });
            }
        }

        var qualities = await _db.QueryAsync(Conn,
            "SELECT Quality, COUNT(*) FROM dbo.Shop_Goods GROUP BY Quality ORDER BY Quality",
            null, 100, 20);
        if (qualities.Ok)
        {
            foreach (var row in qualities.Rows)
            {
                Qualities.Add(new ShopQualityOption
                {
                    Quality = UserRow.ParseInt(row.ElementAtOrDefault(0)),
                    Count = UserRow.ParseInt(row.ElementAtOrDefault(1))
                });
            }
        }
    }

    /// <summary>Nạp card chi tiết khi có ?sel=&lt;TemplateID&gt;.</summary>
    private async Task LoadDetailAsync()
    {
        if (!Sel.HasValue || Sel.Value == 0) return;
        var result = await _db.QueryAsync(Conn, DetailSql,
            new Dictionary<string, object?> { ["tpl"] = Sel.Value }, 1, 20);
        if (!result.Ok)
        {
            DetailError = "Lỗi tải chi tiết TemplateID " + Sel.Value + ": " + result.Error;
            return;
        }
        if (result.Rows.Count == 0)
        {
            DetailError = $"Không tìm thấy TemplateID {Sel.Value} trong Shop_Goods.";
            return;
        }
        Detail = BuildDetail(result);
    }

    private ShopItemDetail BuildDetail(SqlResult result)
    {
        var row = result.Rows[0];
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < result.Columns.Count; i++)
            values[result.Columns[i]] = row.ElementAtOrDefault(i) ?? "";

        string Get(string key) => values.TryGetValue(key, out var value) ? value : "";
        int GetInt(string key) => int.TryParse(Get(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0;

        var catId = GetInt("CategoryID");
        var detail = new ShopItemDetail
        {
            TemplateId = GetInt("TemplateID"),
            Name = Get("Name"),
            Remark = Get("Remark"),
            Description = Get("Description"),
            CategoryId = catId,
            CategoryName = Get("CategoryName"),
            Level = GetInt("Level"),
            Quality = GetInt("Quality"),
            Attack = GetInt("Attack"),
            Defence = GetInt("Defence"),
            Agility = GetInt("Agility"),
            Luck = GetInt("Luck"),
            MaxCount = GetInt("MaxCount"),
            NeedLevel = GetInt("NeedLevel"),
            NeedSex = GetInt("NeedSex"),
            Valid = GetInt("Valid"),
            Count = GetInt("Count"),
            SuitId = GetInt("SuitId"),
            FloorPrice = GetInt("FloorPrice"),
            ReclaimValue = GetInt("ReclaimValue"),
            ReclaimType = GetInt("ReclaimType"),
            RefineryLevel = GetInt("RefineryLevel"),
            BindType = GetInt("BindType"),
            FusionType = GetInt("FusionType"),
            FusionRate = GetInt("FusionRate"),
            FusionNeedRate = GetInt("FusionNeedRate"),
            Colors = Get("Colors"),
            Hole = Get("Hole"),
            AddTime = FormatAddTime(Get("AddTime")),
            Pic = Get("Pic"),
            SupportsGold = GoldCategories.Contains(catId),
            SupportsStats = StatCategories.Contains(catId),
            SupportsStrength = StrengthCategories.Contains(catId)
        };
        detail.ImageUrl = BuildItemImageUrl(catId, detail.Pic, detail.NeedSex);

        detail.Flags.Add(("Cường hoá", ParseFlag(Get("CanStrengthen"))));
        detail.Flags.Add(("Ghép / chế tạo", ParseFlag(Get("CanCompose"))));
        detail.Flags.Add(("Rơi khi đánh quái", ParseFlag(Get("CanDrop"))));
        detail.Flags.Add(("Trang bị", ParseFlag(Get("CanEquip"))));
        detail.Flags.Add(("Sử dụng", ParseFlag(Get("CanUse"))));
        detail.Flags.Add(("Xoá", ParseFlag(Get("CanDelete"))));
        detail.Flags.Add(("Tái chế", GetInt("CanRecycle") > 0));
        detail.Flags.Add(("Chuyển giao", GetInt("CanTransfer") > 0));

        for (var i = 0; i < result.Columns.Count; i++)
            detail.RawFields.Add((result.Columns[i], row.ElementAtOrDefault(i) ?? ""));
        return detail;
    }

    private static bool ParseFlag(string? value) =>
        value is not null &&
        (value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase));

    private static string FormatAddTime(string? value) =>
        DateTime.TryParseExact(value, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var dt)
            ? dt.ToString("dd/MM/yyyy HH:mm")
            : (value ?? "");

    /// <summary>Ảnh item theo đúng cấu trúc resource của Gunny (copy mapping từ UserDetail).</summary>
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

    /// <summary>URL /Shop giữ nguyên bộ lọc hiện tại; sel = null để bỏ chế độ chi tiết.</summary>
    public string ShopUrl(int? sel)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(Q)) parts.Add("q=" + Uri.EscapeDataString(Q));
        if (Cat.HasValue) parts.Add("cat=" + Cat.Value);
        if (Quality.HasValue) parts.Add("quality=" + Quality.Value);
        if (!string.IsNullOrEmpty(Sort) && Sort != "tid") parts.Add("sort=" + Uri.EscapeDataString(Sort));
        if (sel.HasValue) parts.Add("sel=" + sel.Value);
        return parts.Count == 0 ? "/Shop" : "/Shop?" + string.Join("&", parts);
    }

    public string DetailUrl(int templateId) => ShopUrl(templateId);
    public string CloseDetailUrl => ShopUrl(null);
}
