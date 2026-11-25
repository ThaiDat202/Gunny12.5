using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using GunnyPanel.Pages.Shared;
using GunnyPanel.Services;
using GunnySuite.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GunnyPanel.Pages;

public sealed class ItemRow
{
    public int ItemID { get; set; }
    public int TemplateID { get; set; }
    public string Name { get; set; } = "";
    public int BagType { get; set; }
    public int Place { get; set; }
    public int Count { get; set; }
    public int StrengthenLevel { get; set; }
    public bool IsBinds { get; set; }
    public string ValidDate { get; set; } = "";
    public string BeginDate { get; set; } = "";

    public static ItemRow From(string?[] row) => new()
    {
        ItemID = UserRow.ParseInt(row.ElementAtOrDefault(0)),
        TemplateID = UserRow.ParseInt(row.ElementAtOrDefault(1)),
        Name = row.ElementAtOrDefault(2) ?? "",
        BagType = UserRow.ParseInt(row.ElementAtOrDefault(3)),
        Place = UserRow.ParseInt(row.ElementAtOrDefault(4)),
        Count = UserRow.ParseInt(row.ElementAtOrDefault(5)),
        StrengthenLevel = UserRow.ParseInt(row.ElementAtOrDefault(6)),
        IsBinds = UserRow.ParseBit(row.ElementAtOrDefault(7)),
        ValidDate = row.ElementAtOrDefault(8) ?? "",
        BeginDate = UserRow.FormatDate(row.ElementAtOrDefault(9))
    };
}

public sealed class CategoryOption
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool SupportsGold { get; set; }
    public bool SupportsStats { get; set; }
}

public sealed class AwardItemOption
{
    public int TemplateId { get; set; }
    public string Name { get; set; } = "";
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public bool AllowGold { get; set; }
    public bool AllowStats { get; set; }
    public bool AllowStrength { get; set; }
    public int MaxCount { get; set; } = 1;
    public string ImageUrl { get; set; } = "";
}

public sealed class LiveStatsInfo
{
    public long FightPowerDisplay { get; set; }
    public long FightPowerShow { get; set; }
    public int Attack { get; set; }
    public int Defence { get; set; }
    public int Agility { get; set; }
    public int Luck { get; set; }
    public int Hp { get; set; }
    public int MagicAttack { get; set; }
    public int MagicDefence { get; set; }
    public int Damage { get; set; }
    public int Armor { get; set; }
    public int Energy => 240 + (Agility / 30);
}

public sealed class UserDetailModel : PageModel
{
    public const int PageSize = 25;
    public const string Conn = "Db_Tank";

    // Đúc vàng: Nón (1), Áo/Quần áo (5), Vũ khí (7).
    private static readonly HashSet<int> GoldCategories = new() { 1, 5, 7 };
    // Cộng 4 chỉ số (Công/Thủ/Nhanh nhẹn/May mắn, tối đa 50):
    // Nón (1), Áo (5), Vũ khí (7), Vũ khí phụ (17), Vòng tay (8), Dây chuyền (14).
    // Cộng 4 chỉ số (Công/Thủ/Nhanh nhẹn/May mắn, tối đa 50):
    // Nón (1), Áo (5), Vũ khí (7), Vũ khí phụ (17), Vòng tay (8), Nhẫn (9), Nhẫn tạm thời (29).
    // Dây chuyền (14): KHÔNG hỗ trợ cộng chỉ số (theo yêu cầu).
    private static readonly HashSet<int> StatCategories = new() { 1, 5, 7, 8, 9, 17, 29 };
    // Cấp độ (StrengthenLevel 0–15): Nón (1), Áo (5), Vũ khí (7), Vũ khí phụ (17).
    private static readonly HashSet<int> StrengthCategories = new() { 1, 5, 7, 17 };
    public static string AwardSupportText =>
        "Đúc vàng + 4 chỉ số: Nón (Cat 1), Áo/Quần áo (Cat 5), Vũ khí (Cat 7). " +
        "4 chỉ số: Vòng tay (8), Nhẫn (9/29), Vũ khí phụ (17). Dây chuyền (14) không cộng chỉ số.";

    public const int AwardPageSize = 18;

    private readonly SqlDatabase _db;
    private readonly AuditLog _audit;
    private readonly RoadLiveService _live;
    private readonly RoadGmApiClient _roadGm;
    private readonly string _itemImageBase;
    private readonly string _mailNoticeScript;
    private readonly int _zoneId;

    public UserDetailModel(SqlDatabase db, AuditLog audit, RoadLiveService live, RoadGmApiClient roadGm, IConfiguration cfg)
    {
        _db = db;
        _audit = audit;
        _live = live;
        _roadGm = roadGm;
        _itemImageBase = (cfg["Vng:ItemImageBase"] ?? "https://gunny.vcdn.vn/").TrimEnd('/') + "/";
        _mailNoticeScript = cfg["Panel:MailNoticeScript"] ?? "";
        _zoneId = int.TryParse(cfg["Panel:ZoneId"], out var zone) ? zone : 1001;
    }

    public bool LiveShutdownEnabled => _live.ShutdownEnabled;
    public string CenterServiceUrl => _live.CenterUrl;
    public bool RoadGmApiEnabled => _roadGm.Enabled;
    public string RoadGmApiUrl => _roadGm.BaseUrl;

    [BindProperty(SupportsGet = true)] public int Id { get; set; }
    [BindProperty(SupportsGet = true, Name = "bag")] public int? Bag { get; set; }
    public int PageIndex { get; private set; } = 1;

    public UserRow? Account { get; private set; }
    public LiveStatsInfo? LiveStats { get; private set; }
    public long DisplayFightPower => (LiveStats != null && LiveStats.FightPowerDisplay > 0)
        ? LiveStats.FightPowerDisplay
        : (Account?.FightPower ?? 0);
    public string GuildName { get; private set; } = "";
    public string? Error { get; private set; }
    public Dictionary<string, string> Info { get; } = new();
    public int ItemTotal { get; private set; }
    public List<ItemRow> Items { get; } = new();
    public List<(int BagType, int Count)> Bags { get; } = new();
    public List<CategoryOption> Categories { get; } = new();
    public List<AwardItemOption> AwardItems { get; } = new();
    public AwardItemOption? SelectedAward { get; private set; }
    public string? AwardListMessage { get; private set; }
    public string? AwardSelectMessage { get; private set; }
    public int AwardTotal { get; private set; }
    public int AwardTotalPages => AwardTotal <= 0 ? 1 : (AwardTotal + AwardPageSize - 1) / AwardPageSize;
    public Pager Pager { get; private set; } = new();

    [BindProperty(SupportsGet = true, Name = "cat")] public int? AwardCat { get; set; }
    [BindProperty(SupportsGet = true, Name = "q")] public string? AwardQ { get; set; }
    [BindProperty(SupportsGet = true, Name = "sel")] public int? AwardItem { get; set; }
    [BindProperty(SupportsGet = true, Name = "apage")] public int AwardPage { get; set; } = 1;
    [BindProperty(SupportsGet = true, Name = "picker")] public int? Picker { get; set; }
    [BindProperty(SupportsGet = true, Name = "embed")] public int? Embed { get; set; }

    public bool CanWrite => User.IsInRole("Develop") || User.IsInRole("Admin");
    [TempData] public string? FlashMessage { get; set; }
    [TempData] public string? FlashError { get; set; }

    public async Task OnGetAsync([FromQuery(Name = "page")] int page = 1)
    {
        // "page" là route value dành riêng của Razor Pages → đọc thủ công từ query string.
        PageIndex = page > 0 ? page : 1;
        await LoadCategoriesAsync();
        await LoadAwardPickerAsync();

        if (Id <= 0)
        {
            Error = "Thiếu id người dùng.";
            return;
        }

        var user = await _db.QueryAsync(Conn,
            "SELECT UserID, UserName, NickName, Sex, Grade, FightPower, GP, Money, Gold, State, LastDate, ConsortiaID, Email, GiftToken, " +
            "Honor, Date, PasswordTwo, IsBanChat, BanChatEndDate, ForbidDate, ForbidReason, IsMarried, SpouseName, " +
            "AchievementPoint, VipIntegral, ChargeDate, LoginCount, OnlineTime " +
            "FROM dbo.Sys_Users_Detail WHERE UserID = @id",
            new Dictionary<string, object?> { ["id"] = Id }, 1, 30);
        if (!user.Ok)
        {
            Error = user.Error;
            return;
        }
        if (user.Rows.Count == 0)
        {
            Error = $"Không tìm thấy người dùng ID={Id}.";
            return;
        }

        var row = user.Rows[0];
        Account = UserRow.From(row);
        for (var i = 0; i < user.Columns.Count; i++)
            Info[user.Columns[i]] = row.ElementAtOrDefault(i) ?? "";

        if (Account.State > 0 && _roadGm.Enabled)
        {
            try
            {
                var live = await _roadGm.GetPlayerAsync(Account.UserID);
                if (live.Ok && live.Online && !string.IsNullOrWhiteSpace(live.Json))
                {
                    using var doc = JsonDocument.Parse(live.Json);
                    if (doc.RootElement.TryGetProperty("player", out var pl))
                    {
                        LiveStats = new LiveStatsInfo
                        {
                            FightPowerDisplay = GetLong(pl, "FightPowerDisplay"),
                            FightPowerShow = GetLong(pl, "FightPowerShow"),
                            Attack = GetInt(pl, "Attack"),
                            Defence = GetInt(pl, "Defence"),
                            Agility = GetInt(pl, "Agility"),
                            Luck = GetInt(pl, "Luck"),
                            Hp = GetInt(pl, "Hp"),
                            MagicAttack = GetInt(pl, "MagicAttack"),
                            MagicDefence = GetInt(pl, "MagicDefence"),
                            Damage = GetInt(pl, "Damage"),
                            Armor = GetInt(pl, "Armor")
                        };
                    }
                }
            }
            catch { }
        }

        if (Account.ConsortiaID > 0)
        {
            var guild = await _db.QueryAsync(Conn,
                "SELECT TOP 1 ConsortiaName FROM dbo.Consortia WHERE ConsortiaID = @cid",
                new Dictionary<string, object?> { ["cid"] = Account.ConsortiaID }, 1, 15);
            if (guild.Ok && guild.Rows.Count > 0)
                GuildName = guild.Rows[0].ElementAtOrDefault(0) ?? "";
        }

        var bags = await _db.QueryAsync(Conn,
            "SELECT BagType, COUNT(*) FROM dbo.Sys_Users_Goods WHERE UserID = @id GROUP BY BagType ORDER BY BagType",
            new Dictionary<string, object?> { ["id"] = Id }, 50, 20);
        if (bags.Ok)
        {
            foreach (var bagRow in bags.Rows)
                Bags.Add((UserRow.ParseInt(bagRow.ElementAtOrDefault(0)), UserRow.ParseInt(bagRow.ElementAtOrDefault(1))));
        }

        var pars = new Dictionary<string, object?> { ["id"] = Id, ["bag"] = Bag.HasValue ? Bag.Value : DBNull.Value };
        var count = await _db.QueryAsync(Conn,
            "SELECT COUNT(*) FROM dbo.Sys_Users_Goods WHERE UserID = @id AND (@bag IS NULL OR BagType = @bag)",
            pars, 1, 20);
        if (!count.Ok)
        {
            Error = count.Error;
            return;
        }
        ItemTotal = UserRow.ParseInt(count.FirstCell());
        PageIndex = Pager.Clamp(PageIndex, ItemTotal, PageSize);
        pars["skip"] = (PageIndex - 1) * PageSize;
        pars["take"] = PageSize;

        var items = await _db.QueryAsync(Conn,
            "SELECT g.ItemID, g.TemplateID, s.Name, g.BagType, g.Place, g.Count, g.StrengthenLevel, g.IsBinds, g.ValidDate, g.BeginDate " +
            "FROM dbo.Sys_Users_Goods g LEFT JOIN dbo.Shop_Goods s ON s.TemplateID = g.TemplateID " +
            "WHERE g.UserID = @id AND (@bag IS NULL OR g.BagType = @bag) " +
            "ORDER BY g.ItemID OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY",
            pars, PageSize, 30);
        if (!items.Ok)
        {
            Error = items.Error;
            return;
        }
        foreach (var itemRow in items.Rows) Items.Add(ItemRow.From(itemRow));

        Pager = new Pager
        {
            Page = PageIndex,
            PageSize = PageSize,
            Total = ItemTotal,
            Path = "/Users/Details",
            PageParam = "page",
            Query = new Dictionary<string, string?>
            {
                ["id"] = Id.ToString(),
                ["bag"] = Bag?.ToString()
            }
        };
    }

    public static string BagLabel(int bagType) => bagType switch
    {
        0 => "Trang bị",
        1 => "Đạo cụ",
        _ => "Bag " + bagType
    };

    // ---------------- Thao tác ghi (đã được duyệt: ban/unban + sửa tiền/level) ----------------

    private async Task LoadCategoriesAsync()
    {
        var result = await _db.QueryAsync("Db_Gm",
            "SELECT CategoryID, Name FROM dbo.Category WHERE CategoryID > 0 ORDER BY [ID]",
            200, 10);
        if (!result.Ok) return;
        foreach (var row in result.Rows)
        {
            var id = UserRow.ParseInt(row.ElementAtOrDefault(0));
            if (id <= 0) continue;
            Categories.Add(new CategoryOption
            {
                Id = id,
                Name = row.ElementAtOrDefault(1) ?? ("Category " + id),
                SupportsGold = GoldCategories.Contains(id),
                SupportsStats = StatCategories.Contains(id)
            });
        }
    }

    /// <summary>Tìm item theo tên/TemplateID (phục vụ chọn item thay vì gõ ID).</summary>
    public async Task<IActionResult> OnGetSearchItemsAsync(string? q, int? categoryId)
    {
        var text = (q ?? "").Trim();
        var where = "WHERE g.TemplateID > 0";
        var pars = new Dictionary<string, object?>();
        if (!string.IsNullOrWhiteSpace(text))
        {
            where += " AND (CAST(g.TemplateID AS nvarchar(20)) = @q OR g.Name LIKE @like)";
            pars["q"] = text;
            pars["like"] = "%" + text + "%";
        }
        if (categoryId.HasValue && categoryId.Value > 0)
        {
            where += " AND g.CategoryID = @cat";
            pars["cat"] = categoryId.Value;
        }

        var result = await _db.QueryAsync("Db_Tank_All",
            "SELECT TOP 25 g.TemplateID, g.Name, g.CategoryID, c.Name AS CategoryName, g.MaxCount " +
            "FROM dbo.Shop_Goods g LEFT JOIN Db_Gm.dbo.Category c ON c.CategoryID = g.CategoryID " +
            where + " ORDER BY g.TemplateID",
            pars, 25, 20);
        if (!result.Ok)
            return new JsonResult(new { ok = false, message = result.Error });

        var items = result.Rows.Select(row =>
        {
            var catId = UserRow.ParseInt(row.ElementAtOrDefault(2));
            return new
            {
                templateId = UserRow.ParseInt(row.ElementAtOrDefault(0)),
                name = row.ElementAtOrDefault(1) ?? "",
                categoryId = catId,
                categoryName = row.ElementAtOrDefault(3) ?? ("Category " + catId),
                maxCount = UserRow.ParseInt(row.ElementAtOrDefault(4)),
                allowGold = GoldCategories.Contains(catId),
                allowStats = StatCategories.Contains(catId)
            };
        }).ToList();
        return new JsonResult(new { ok = true, items });
    }

    /// <summary>Server-side chọn vật phẩm Award: category + danh sách item có ảnh + phân trang + item đã chọn.</summary>
    private async Task LoadAwardPickerAsync()
    {
        if (AwardItem.HasValue && AwardItem.Value > 0)
        {
            var t = await LoadTemplateAsync(AwardItem.Value);
            if (t != null)
            {
                var (name, catId, catName, maxCount, pic, needSex) = t.Value;
                SelectedAward = new AwardItemOption
                {
                    TemplateId = AwardItem.Value,
                    Name = name,
                    CategoryId = catId,
                    CategoryName = catName,
                    AllowGold = GoldCategories.Contains(catId),
                    AllowStats = StatCategories.Contains(catId),
                    AllowStrength = StrengthCategories.Contains(catId),
                    MaxCount = Math.Max(1, maxCount),
                    ImageUrl = BuildItemImageUrl(catId, pic, needSex)
                };
            }
            else
            {
                AwardSelectMessage = $"Không tìm thấy TemplateID {AwardItem.Value} trong Shop_Goods.";
            }
        }

        var hasCat = AwardCat.HasValue && AwardCat.Value > 0;
        var hasQ = !string.IsNullOrWhiteSpace(AwardQ);
        if (Picker != 1 || (!hasCat && !hasQ)) return;

        var where = "WHERE g.TemplateID > 0";
        var pars = new Dictionary<string, object?>();
        if (hasCat)
        {
            where += " AND g.CategoryID = @cat";
            pars["cat"] = AwardCat!.Value;
        }
        if (hasQ)
        {
            where += " AND (g.Name LIKE @like OR CAST(g.TemplateID AS nvarchar(20)) = @q)";
            pars["like"] = "%" + AwardQ!.Trim() + "%";
            pars["q"] = AwardQ!.Trim();
        }

        var count = await _db.QueryAsync("Db_Tank_All", "SELECT COUNT(*) FROM dbo.Shop_Goods g " + where, pars, 1, 20);
        if (!count.Ok)
        {
            AwardListMessage = "Lỗi tải danh sách: " + count.Error;
            return;
        }
        AwardTotal = UserRow.ParseInt(count.FirstCell());
        AwardPage = Pager.Clamp(AwardPage, AwardTotal, AwardPageSize);
        pars["skip"] = (AwardPage - 1) * AwardPageSize;
        pars["take"] = AwardPageSize;

        var result = await _db.QueryAsync("Db_Tank_All",
            "SELECT g.TemplateID, g.Name, g.CategoryID, c.Name AS CategoryName, g.MaxCount, g.Pic, g.NeedSex " +
            "FROM dbo.Shop_Goods g LEFT JOIN Db_Gm.dbo.Category c ON c.CategoryID = g.CategoryID " +
            where + " ORDER BY g.TemplateID OFFSET @skip ROWS FETCH NEXT @take ROWS ONLY",
            pars, AwardPageSize, 20);
        if (!result.Ok)
        {
            AwardListMessage = "Lỗi tải danh sách: " + result.Error;
            return;
        }

        foreach (var row in result.Rows)
        {
            var catId = UserRow.ParseInt(row.ElementAtOrDefault(2));
            var tpl = UserRow.ParseInt(row.ElementAtOrDefault(0));
            var pic = row.ElementAtOrDefault(5) ?? "";
            var needSex = UserRow.ParseInt(row.ElementAtOrDefault(6));
            AwardItems.Add(new AwardItemOption
            {
                TemplateId = tpl,
                Name = row.ElementAtOrDefault(1) ?? "",
                CategoryId = catId,
                CategoryName = row.ElementAtOrDefault(3) ?? ("Category " + catId),
                AllowGold = GoldCategories.Contains(catId),
                AllowStats = StatCategories.Contains(catId),
                AllowStrength = StrengthCategories.Contains(catId),
                MaxCount = Math.Max(1, UserRow.ParseInt(row.ElementAtOrDefault(4))),
                ImageUrl = BuildItemImageUrl(catId, pic, needSex)
            });
        }
        if (AwardItems.Count == 0 && AwardTotal == 0)
            AwardListMessage = "Không có item phù hợp. Thử thể loại khác hoặc từ khoá khác.";
    }

    public string AwardPageUrl(int page) =>
        $"/Users/Details?id={Id}&cat={AwardCat}&q={Uri.EscapeDataString(AwardQ ?? "")}&picker=1&apage={page}" +
        (Embed == 1 ? "&embed=1" : "");

    public string AwardSelectUrl(int templateId) =>
        $"/Users/Details?id={Id}&cat={AwardCat}&q={Uri.EscapeDataString(AwardQ ?? "")}&picker=1&apage={AwardPage}&sel={templateId}" +
        (Embed == 1 ? "&embed=1" : "");

    /// <summary>Ảnh item theo đúng cấu trúc resource của Gunny (giống Html.ItemImage của Admincp).</summary>
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

    private async Task<UserRow?> LoadUserAsync(int id)
    {
        var result = await _db.QueryAsync(Conn,
            "SELECT UserID, UserName, NickName, Sex, Grade, FightPower, GP, Money, Gold, State, LastDate, ConsortiaID, Email, GiftToken " +
            "FROM dbo.Sys_Users_Detail WHERE UserID = @id",
            new Dictionary<string, object?> { ["id"] = id }, 1, 20);
        if (!result.Ok || result.Rows.Count == 0) return null;
        return UserRow.From(result.Rows[0]);
    }

    private IActionResult BackToUser(int id) => Redirect($"/Users/Details?id={id}");

    public async Task<IActionResult> OnPostBanAsync(int id, int days, string? reason, string? confirmNick)
    {
        if (!CanWrite)
        {
            FlashError = "Bạn không có quyền thao tác ghi.";
            return BackToUser(id);
        }

        var user = await LoadUserAsync(id);
        if (user == null)
        {
            FlashError = $"Không tìm thấy người dùng ID={id}.";
            return BackToUser(id);
        }
        if (!string.Equals((confirmNick ?? "").Trim(), user.NickName, StringComparison.OrdinalIgnoreCase))
        {
            FlashError = "NickName xác nhận không khớp — không thực hiện ban.";
            return BackToUser(id);
        }

        days = Math.Clamp(days, 1, 3650);
        var until = DateTime.Now.AddDays(days);
        var reasonText = (reason ?? "").Trim();
        if (reasonText.Length > 1000) reasonText = reasonText[..1000];

        var (rows, error) = await _db.ExecuteAsync(Conn,
            "EXEC dbo.SP_Admin_ForbidUser @UserName = N'', @NickName = @nick, @UserID = @id, " +
            "@ForbidDate = @until, @IsExist = @isExist, @ForbidReason = @reason",
            new Dictionary<string, object?>
            {
                ["nick"] = user.NickName,
                ["id"] = id,
                ["until"] = until,
                ["isExist"] = false,
                ["reason"] = reasonText
            });

        if (error != null)
        {
            FlashError = "Lỗi ban: " + error;
        }
        else
        {
            FlashMessage = $"Đã ban {user.NickName} đến {until:dd/MM/yyyy HH:mm}." +
                           (user.State > 0 ? " Nhân vật đang online — ban có hiệu lực khi đăng nhập lại (chưa có API kick)." : "");
            _audit.Write(User.Identity?.Name ?? "unknown", "user.ban",
                $"id={id} nick={user.NickName} days={days} until={until:yyyy-MM-dd HH:mm} reason={reasonText} online={user.State > 0}");
        }
        return BackToUser(id);
    }

    public async Task<IActionResult> OnPostUnbanAsync(int id, string? confirmNick)
    {
        if (!CanWrite)
        {
            FlashError = "Bạn không có quyền thao tác ghi.";
            return BackToUser(id);
        }

        var user = await LoadUserAsync(id);
        if (user == null)
        {
            FlashError = $"Không tìm thấy người dùng ID={id}.";
            return BackToUser(id);
        }
        if (!string.Equals((confirmNick ?? "").Trim(), user.NickName, StringComparison.OrdinalIgnoreCase))
        {
            FlashError = "NickName xác nhận không khớp — không thực hiện unban.";
            return BackToUser(id);
        }

        var (rows, error) = await _db.ExecuteAsync(Conn,
            "EXEC dbo.SP_Admin_ForbidUser @UserName = N'', @NickName = @nick, @UserID = @id, " +
            "@ForbidDate = @now, @IsExist = @isExist, @ForbidReason = N''",
            new Dictionary<string, object?>
            {
                ["nick"] = user.NickName,
                ["id"] = id,
                ["now"] = DateTime.Now,
                ["isExist"] = true
            });

        if (error != null)
        {
            FlashError = "Lỗi unban: " + error;
        }
        else
        {
            FlashMessage = $"Đã mở ban cho {user.NickName}.";
            _audit.Write(User.Identity?.Name ?? "unknown", "user.unban", $"id={id} nick={user.NickName}");
        }
        return BackToUser(id);
    }

    /// <summary>Ban chat: cập nhật Sys_Users_Detail.IsBanChat / BanChatEndDate (có hiệu lực khi đăng nhập lại).</summary>
    public async Task<IActionResult> OnPostBanChatAsync(int id, int days, string? reason, string? confirmNick)
    {
        if (!CanWrite)
        {
            FlashError = "Bạn không có quyền thao tác ghi.";
            return BackToUser(id);
        }

        var user = await LoadUserAsync(id);
        if (user == null)
        {
            FlashError = $"Không tìm thấy người dùng ID={id}.";
            return BackToUser(id);
        }
        if (!string.Equals((confirmNick ?? "").Trim(), user.NickName, StringComparison.OrdinalIgnoreCase))
        {
            FlashError = "NickName xác nhận không khớp — không thực hiện ban chat.";
            return BackToUser(id);
        }

        days = Math.Clamp(days, 1, 3650);
        var until = DateTime.Now.AddDays(days);
        var reasonText = (reason ?? "").Trim();
        if (reasonText.Length > 500) reasonText = reasonText[..500];

        var (rows, error) = await _db.ExecuteAsync(Conn,
            "UPDATE dbo.Sys_Users_Detail SET IsBanChat = 1, BanChatEndDate = @until WHERE UserID = @id",
            new Dictionary<string, object?>
            {
                ["until"] = until,
                ["id"] = id
            });

        if (error != null)
        {
            FlashError = "Lỗi ban chat: " + error;
        }
        else if (rows != 1)
        {
            FlashError = $"Không cập nhật được (rows={rows}).";
        }
        else
        {
            FlashMessage = $"Đã cấm chat {user.NickName} đến {until:dd/MM/yyyy HH:mm}." +
                           (user.State > 0 ? " Đang online — cần đăng nhập lại để có hiệu lực (hoặc dùng Kick)." : "");
            _audit.Write(User.Identity?.Name ?? "unknown", "user.banchat",
                $"id={id} nick={user.NickName} days={days} until={until:yyyy-MM-dd HH:mm} reason={reasonText} online={user.State > 0}");
        }
        return BackToUser(id);
    }

    /// <summary>Mở ban chat.</summary>
    public async Task<IActionResult> OnPostUnbanChatAsync(int id, string? confirmNick)
    {
        if (!CanWrite)
        {
            FlashError = "Bạn không có quyền thao tác ghi.";
            return BackToUser(id);
        }

        var user = await LoadUserAsync(id);
        if (user == null)
        {
            FlashError = $"Không tìm thấy người dùng ID={id}.";
            return BackToUser(id);
        }
        if (!string.Equals((confirmNick ?? "").Trim(), user.NickName, StringComparison.OrdinalIgnoreCase))
        {
            FlashError = "NickName xác nhận không khớp — không thực hiện mở ban chat.";
            return BackToUser(id);
        }

        var (rows, error) = await _db.ExecuteAsync(Conn,
            "UPDATE dbo.Sys_Users_Detail SET IsBanChat = 0, BanChatEndDate = @now WHERE UserID = @id",
            new Dictionary<string, object?>
            {
                ["now"] = DateTime.Now,
                ["id"] = id
            });

        if (error != null)
        {
            FlashError = "Lỗi mở ban chat: " + error;
        }
        else if (rows != 1)
        {
            FlashError = $"Không cập nhật được (rows={rows}).";
        }
        else
        {
            FlashMessage = $"Đã mở ban chat cho {user.NickName}." +
                           (user.State > 0 ? " Đang online — cần đăng nhập lại để thấy hiệu lực." : "");
            _audit.Write(User.Identity?.Name ?? "unknown", "user.unbanchat", $"id={id} nick={user.NickName}");
        }
        return BackToUser(id);
    }

    /// <summary>Gửi mail tùy chỉnh (title/content + tiền tệ, không kèm item) vào User_Messages.</summary>
    public async Task<IActionResult> OnPostSendMailAsync(int id, string? title, string? content,
        int mailGold, int mailMoney, int mailGiftToken, string? confirmNick)
    {
        if (!CanWrite)
        {
            FlashError = "Bạn không có quyền thao tác ghi.";
            return BackToUser(id);
        }

        var user = await LoadUserAsync(id);
        if (user == null)
        {
            FlashError = $"Không tìm thấy người dùng ID={id}.";
            return BackToUser(id);
        }
        if (!string.Equals((confirmNick ?? "").Trim(), user.NickName, StringComparison.OrdinalIgnoreCase))
        {
            FlashError = "NickName xác nhận không khớp — không gửi mail.";
            return BackToUser(id);
        }

        var titleText = string.IsNullOrWhiteSpace(title) ? "Thư từ GM Panel" : title.Trim();
        var contentText = string.IsNullOrWhiteSpace(content) ? "Thư từ GM Panel." : content.Trim();
        if (titleText.Length > 500) titleText = titleText[..500];
        if (contentText.Length > 4000) contentText = contentText[..4000];
        if (mailGold < 0 || mailGold > int.MaxValue) return ErrorMail(id, "Vàng trong mail phải trong khoảng 0..2,147,483,647.");
        if (mailMoney < 0 || mailMoney > int.MaxValue) return ErrorMail(id, "Xu trong mail phải trong khoảng 0..2,147,483,647.");
        if (mailGiftToken < 0) return ErrorMail(id, "Xu khóa trong mail không được âm.");

        var (rows, error) = await _db.ExecuteAsync(Conn,
            "INSERT INTO dbo.User_Messages " +
            "(SenderID, Sender, ReceiverID, Receiver, Title, Content, SendTime, IsRead, IsDelR, IfDelS, IsDelete, " +
            " Annex1, Annex2, Gold, Money, IsExist, Type, Remark, ValidDate, " +
            " Annex1Name, Annex2Name, Annex3, Annex4, Annex5, Annex3Name, Annex4Name, Annex5Name, AnnexRemark, GiftToken) " +
            "VALUES (0, N'GM Panel', @uid, @receiver, @title, @content, @now, 0, 0, 0, 0, " +
            " N'', N'', @gold, @money, 1, 51, N'GM Panel mail', 240, " +
            " N'', N'', N'', N'', N'', N'', N'', N'', N'', @giftToken)",
            new Dictionary<string, object?>
            {
                ["uid"] = id,
                ["receiver"] = user.NickName,
                ["title"] = titleText,
                ["content"] = contentText,
                ["now"] = DateTime.Now,
                ["gold"] = mailGold,
                ["money"] = mailMoney,
                ["giftToken"] = mailGiftToken
            });

        if (error != null)
        {
            FlashError = "Lỗi gửi mail: " + error;
            return BackToUser(id);
        }

        bool? mailNotified = null;
        if (user.State > 0) mailNotified = await NotifyOnlineMailAsync(id);

        FlashMessage = $"Đã gửi mail “{titleText}” cho {user.NickName} " +
                       $"(Vàng {mailGold:N0}, Xu {mailMoney:N0}, Xu khóa {mailGiftToken:N0})." +
                       (mailNotified == true ? " Đã thông báo hộp thư cho client đang online." : "");
        _audit.Write(User.Identity?.Name ?? "unknown", "user.mail",
            $"id={id} nick={user.NickName} title={titleText} gold={mailGold} money={mailMoney} " +
            $"giftToken={mailGiftToken} mailNotified={mailNotified}");
        return BackToUser(id);
    }

    private IActionResult ErrorMail(int id, string message)
    {
        FlashError = message;
        return BackToUser(id);
    }

    public async Task<IActionResult> OnPostEditAsync(int id, long money, long gold, int giftToken, int grade,
        string? confirmNick)
    {
        if (!CanWrite)
        {
            FlashError = "Bạn không có quyền thao tác ghi.";
            return BackToUser(id);
        }

        var user = await LoadUserAsync(id);
        if (user == null)
        {
            FlashError = $"Không tìm thấy người dùng ID={id}.";
            return BackToUser(id);
        }
        if (!string.Equals((confirmNick ?? "").Trim(), user.NickName, StringComparison.OrdinalIgnoreCase))
        {
            FlashError = "NickName xác nhận không khớp — không thực hiện sửa.";
            return BackToUser(id);
        }
        if (user.State > 0)
        {
            FlashError = "⚠ Nhân vật đang online (State > 0). Hãy cho người chơi đăng xuất/offline trước khi sửa tiền/level.";
            return BackToUser(id);
        }
        if (money < 0 || money > int.MaxValue)
        {
            FlashError = $"Xu (Money) phải trong khoảng 0..{int.MaxValue:N0}.";
            return BackToUser(id);
        }
        if (gold < 0 || gold > int.MaxValue)
        {
            FlashError = $"Vàng (Gold) phải trong khoảng 0..{int.MaxValue:N0}.";
            return BackToUser(id);
        }
        if (giftToken < 0)
        {
            FlashError = "Xu khóa không được âm.";
            return BackToUser(id);
        }
        if (grade < 1 || grade > 70)
        {
            FlashError = "Level (Grade) chỉ được sửa trong khoảng 1..70.";
            return BackToUser(id);
        }

        var currentGiftToken = user.GiftToken;

        var (rows, error) = await _db.ExecuteAsync(Conn,
            "UPDATE dbo.Sys_Users_Detail SET Money = @money, Gold = @gold, GiftToken = @giftToken, Grade = @grade WHERE UserID = @id",
            new Dictionary<string, object?>
            {
                ["money"] = (int)money,
                ["gold"] = (int)gold,
                ["giftToken"] = giftToken,
                ["grade"] = grade,
                ["id"] = id
            });

        if (error != null)
        {
            FlashError = "Lỗi cập nhật: " + error;
        }
        else if (rows != 1)
        {
            FlashError = $"Không cập nhật được (rows={rows}) — dữ liệu có thể đã thay đổi, thử lại.";
        }
        else
        {
            FlashMessage = $"Đã cập nhật {user.NickName}: " +
                           $"Xu {user.Money:N0} → {money:N0}, Vàng {user.Gold:N0} → {gold:N0}, " +
                           $"Xu khóa {currentGiftToken:N0} → {giftToken:N0}, Level {user.Grade} → {grade}. " +
                           "Người chơi cần đăng nhập lại để thấy dữ liệu mới.";
            _audit.Write(User.Identity?.Name ?? "unknown", "user.edit",
                $"id={id} nick={user.NickName} money={user.Money}->{money} gold={user.Gold}->{gold} " +
                $"giftToken={currentGiftToken}->{giftToken} grade={user.Grade}->{grade}");
        }
        return BackToUser(id);
    }

    public async Task<IActionResult> OnPostDeleteItemAsync(int id, int itemId, string? confirmNick)
    {
        if (!CanWrite)
        {
            FlashError = "Bạn không có quyền thao tác ghi.";
            return BackToUser(id);
        }

        var user = await LoadUserAsync(id);
        if (user == null)
        {
            FlashError = $"Không tìm thấy người dùng ID={id}.";
            return BackToUser(id);
        }
        if (!string.Equals((confirmNick ?? "").Trim(), user.NickName, StringComparison.OrdinalIgnoreCase))
        {
            FlashError = "NickName xác nhận không khớp — không xoá vật phẩm.";
            return BackToUser(id);
        }
        if (user.State > 0)
        {
            FlashError = "⚠ Nhân vật đang online — hãy cho offline trước khi xoá vật phẩm.";
            return BackToUser(id);
        }

        var item = await _db.QueryAsync(Conn,
            "SELECT TOP 1 TemplateID, Count FROM dbo.Sys_Users_Goods WHERE ItemID = @item AND UserID = @uid AND IsExist = 1",
            new Dictionary<string, object?> { ["item"] = itemId, ["uid"] = id }, 1, 20);
        if (!item.Ok || item.Rows.Count == 0)
        {
            FlashError = $"Không tìm thấy vật phẩm ItemID={itemId} của {user.NickName} (hoặc đã bị xoá).";
            return BackToUser(id);
        }

        var (rows, error) = await _db.ExecuteAsync(Conn,
            "UPDATE dbo.Sys_Users_Goods SET IsExist = 0 WHERE ItemID = @item AND UserID = @uid",
            new Dictionary<string, object?> { ["item"] = itemId, ["uid"] = id });

        if (error != null)
        {
            FlashError = "Lỗi xoá vật phẩm: " + error;
        }
        else if (rows != 1)
        {
            FlashError = $"Không xoá được vật phẩm (rows={rows}).";
        }
        else
        {
            FlashMessage = $"Đã xoá (soft-delete) vật phẩm ItemID={itemId} (TemplateID={item.Rows[0].ElementAtOrDefault(0)}) của {user.NickName}.";
            _audit.Write(User.Identity?.Name ?? "unknown", "user.item.delete",
                $"id={id} nick={user.NickName} itemId={itemId} templateId={item.Rows[0].ElementAtOrDefault(0)}");
        }
        return BackToUser(id);
    }

    /// <summary>Kick người chơi đang online qua CenterService (WCF có sẵn) — AJAX, không reload trang.</summary>
    public async Task<IActionResult> OnPostKickAsync(int id, string? reason)
    {
        if (!CanWrite)
            return new JsonResult(new { ok = false, message = "Bạn không có quyền thao tác ghi." }) { StatusCode = 403 };

        var user = await LoadUserAsync(id);
        if (user == null)
            return new JsonResult(new { ok = false, message = $"Không tìm thấy người dùng ID={id}." }) { StatusCode = 404 };

        var text = (reason ?? "").Trim();
        if (text.Length > 200) text = text[..200];

        var result = await _live.KickAsync(id, string.IsNullOrWhiteSpace(text) ? null : text);
        _audit.Write(User.Identity?.Name ?? "unknown", "road.kick",
            $"id={id} nick={user.NickName} ok={result.Ok} reason={text.Replace("\r", " ").Replace("\n", " ")}");

        if (!result.Ok)
            return new JsonResult(new { ok = false, message = result.Message }) { StatusCode = 400 };

        return new JsonResult(new
        {
            ok = true,
            message = result.Message + " Đợi vài giây để server lưu, sau đó thử sửa tiền/level hoặc thao tác tiếp."
        });
    }

    /// <summary>Hỏi trạng thái online thật từ CenterService (read-only, dùng cho nút “Kiểm tra live”).</summary>
    public async Task<IActionResult> OnGetPlayerStateAsync(int id)
    {
        if (id <= 0) return new JsonResult(new { ok = false, message = "Thiếu UserID." }) { StatusCode = 400 };
        var user = await LoadUserAsync(id);
        var state = await _live.GetPlayerStateAsync(id);
        return new JsonResult(new
        {
            ok = state.Ok,
            message = state.Message,
            centerState = state.State,
            nick = user?.NickName ?? "",
            dbState = user?.State ?? 0
        });
    }

    /// <summary>
    /// Trạng thái Road GM API (dùng cho badge trong khung GM Tool).
    /// </summary>
    public async Task<IActionResult> OnGetLiveStatusAsync()
    {
        if (!_roadGm.Enabled)
            return new JsonResult(new { enabled = false, ok = false, message = "Road GM API chưa cấu hình (Panel:RoadGmApiToken)." });
        var health = await _roadGm.HealthAsync();
        return new JsonResult(new { enabled = true, ok = health.Ok, message = health.Message, url = _roadGm.BaseUrl });
    }

    /// <summary>GM Tool: mỗi feature 1 request AJAX, tránh timeout khi chạy full.</summary>
    public async Task<IActionResult> OnPostGmToolAsync(int id, string feature)
    {
        if (!CanWrite)
            return new JsonResult(new { ok = false, message = "Bạn không có quyền thao tác ghi." }) { StatusCode = 403 };
        if (id <= 0)
            return new JsonResult(new { ok = false, message = "Thiếu UserID." }) { StatusCode = 400 };

        var user = await LoadUserAsync(id);
        if (user == null)
            return new JsonResult(new { ok = false, message = $"Không tìm thấy người dùng ID={id}." }) { StatusCode = 404 };
        if (!GmToolService.FeatureOrder.Contains(feature) && !RoadGmApiClient.IsApiManagedFeature(feature))
            return new JsonResult(new { ok = false, message = "Chức năng không hợp lệ." }) { StatusCode = 400 };

        var ok = false;
        var message = "";
        var live = false;
        var liveMessage = "";
        var handledByApi = false;

        // 1) Châu Báu / Ma Thạch: Road GM API xử lý toàn bộ (mở drill, gửi mail đúng 6 chỉ số cho Ma Thạch).
        //    Chỉ fallback DB path nếu API không kết nối được (Reachable=false).
        if (_roadGm.Enabled && RoadGmApiClient.IsApiManagedFeature(feature))
        {
            var api = await _roadGm.ApplyAsync(id, feature);
            if (api.Reachable)
            {
                handledByApi = true;
                ok = api.Ok;
                message = api.Message;
                live = api.Online;
                if (!ok)
                {
                    _audit.Write(User.Identity?.Name ?? "unknown", "user.gm." + feature,
                        $"id={id} nick={user.NickName} ok=false live={onl(api.Online)} apiError={api.Message}");
                    return new JsonResult(new { ok = false, message = "Road GM API lỗi: " + api.Message, live = false })
                        { StatusCode = 400 };
                }
            }
        }

        // 2) Các feature còn lại: ghi DB trước (nguồn dữ liệu chuẩn), sau đó áp live nếu user online.
        if (!handledByApi)
        {
            var service = new GmToolService(_db);
            var run = await service.RunAsync(id, feature);
            ok = run.Item1;
            message = run.Item2;

            if (ok && _roadGm.Enabled && RoadGmApiClient.IsLiveFeature(feature))
            {
                try
                {
                    var api = await _roadGm.ApplyAsync(id, feature);
                    if (!api.Reachable)
                    {
                        liveMessage = " ⚠ Không kết nối được Road GM API — cần đăng nhập lại để thấy thay đổi.";
                    }
                    else if (api.Ok && api.Online)
                    {
                        live = true;
                        liveMessage = " ◆ Đã áp dụng LIVE cho người chơi đang online (không cần đăng nhập lại).";
                    }
                    else if (api.Online)
                    {
                        liveMessage = " ⚠ Live thất bại: " + api.Message;
                    }
                    else if (user.State > 0)
                    {
                        liveMessage = " ⚠ Không áp dụng live được: " + api.Message;
                    }
                }
                catch (Exception ex)
                {
                    liveMessage = " ⚠ Không gọi được Road GM API: " + ex.Message;
                }
            }

            // Feature dạng mail cũ (nhánh fallback khi API tắt/lỗi): notify hộp thư nếu online.
            if (ok && (feature == "beads" || feature == "magicstone"))
            {
                var onlineNow = user.State > 0;
                if (!onlineNow)
                {
                    try
                    {
                        var state = await _live.GetPlayerStateAsync(id);
                        onlineNow = state.Ok && state.State > 0;
                    }
                    catch { /* CenterService lỗi — bỏ qua notify */ }
                }
                if (onlineNow)
                {
                    try { await NotifyOnlineMailAsync(id); } catch { /* mail đã vào hộp thư; notify lỗi không chặn */ }
                }
            }
        }

        var finalMessage = message + liveMessage;
        _audit.Write(User.Identity?.Name ?? "unknown", "user.gm." + feature,
            $"id={id} nick={user.NickName} ok={ok} live={live} viaApi={handledByApi} message={finalMessage}");

        return new JsonResult(new { ok, message = finalMessage, live }) { StatusCode = ok ? 200 : 400 };
    }

    private static int GetInt(JsonElement el, string name)
    {
        if (el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i)) return i;
        return 0;
    }

    private static long GetLong(JsonElement el, string name)
    {
        if (el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var i)) return i;
        return 0;
    }

    private static string onl(bool online) => online ? "true" : "false";

    public async Task<IActionResult> OnPostSendItemAsync(int id, int templateId, int count, bool bind, int validDays,
        int attack, int defend, int agility, int luck, int goldDays, int strengthLevel,
        string? title, string? content)
    {
        var (ok, message) = await SendItemCoreAsync(id, templateId, count, bind, validDays,
            attack, defend, agility, luck, goldDays, strengthLevel, title, content);
        if (ok) FlashMessage = message; else FlashError = message;
        return BackToUser(id);
    }

    /// <summary>Gửi Award bằng AJAX: không reload trang, giữ modal/trang hiện tại.</summary>
    public async Task<IActionResult> OnPostSendItemAjaxAsync(int id, int templateId, int count, bool bind, int validDays,
        int attack, int defend, int agility, int luck, int goldDays, int strengthLevel,
        string? title, string? content)
    {
        var (ok, message) = await SendItemCoreAsync(id, templateId, count, bind, validDays,
            attack, defend, agility, luck, goldDays, strengthLevel, title, content);
        if (!ok) Response.StatusCode = 400;
        return new JsonResult(new { ok, message });
    }

    private async Task<(bool Ok, string Message)> SendItemCoreAsync(int id, int templateId, int count, bool bind,
        int validDays, int attack, int defend, int agility, int luck, int goldDays, int strengthLevel,
        string? title, string? content)
    {
        if (!CanWrite)
            return (false, "Bạn không có quyền thao tác ghi.");

        var user = await LoadUserAsync(id);
        if (user == null)
            return (false, $"Không tìm thấy người dùng ID={id}.");
        if (templateId <= 0)
            return (false, "TemplateID không hợp lệ.");

        count = Math.Clamp(count, 1, 9999);
        validDays = Math.Clamp(validDays, 0, 3650);
        goldDays = Math.Clamp(goldDays, 0, 3650);

        if (attack < 0 || attack > 50 || defend < 0 || defend > 50 ||
            agility < 0 || agility > 50 || luck < 0 || luck > 50)
            return (false, "Chỉ số cộng (Công/Thủ/Nhanh nhẹn/May mắn) phải trong khoảng 0–50.");
        if (strengthLevel < 0 || strengthLevel > 15)
            return (false, "Cấp độ (StrengthenLevel) phải trong khoảng 0–15.");

        var template = await LoadTemplateAsync(templateId);
        if (template == null)
            return (false, $"TemplateID {templateId} không tồn tại trong Shop_Goods.");

        var (itemName, categoryId, categoryName, _, _, _) = template.Value;
        var hasStats = attack > 0 || defend > 0 || agility > 0 || luck > 0;
        if (hasStats && !StatCategories.Contains(categoryId))
            return (false, $"Item '{itemName}' — Category {categoryId} ({categoryName}) không hỗ trợ cộng chỉ số. " +
                           "Chỉ hỗ trợ Nón (1), Áo (5), Vũ khí (7), Vòng tay (8), Nhẫn (9/29), Vũ khí phụ (17).");
        if (goldDays > 0 && !GoldCategories.Contains(categoryId))
            return (false, $"Item '{itemName}' — Category {categoryId} ({categoryName}) không hỗ trợ đúc vàng. " +
                           "Chỉ hỗ trợ Nón (1), Áo/Quần áo (5) và Vũ khí (7).");
        if (strengthLevel > 0 && !StrengthCategories.Contains(categoryId))
            return (false, $"Item '{itemName}' — Category {categoryId} ({categoryName}) không hỗ trợ cấp độ. " +
                           "Chỉ hỗ trợ Nón (1), Áo (5), Vũ khí (7), Vũ khí phụ (17).");

        var titleText = string.IsNullOrWhiteSpace(title) ? "Phần thưởng GM" : title.Trim();
        var contentText = string.IsNullOrWhiteSpace(content) ? "Vật phẩm từ GM Panel" : content.Trim();
        if (titleText.Length > 1000) titleText = titleText[..1000];
        if (contentText.Length > 2000) contentText = contentText[..2000];

        var before = await _db.QueryAsync(Conn, "SELECT ISNULL(MAX(ItemID), 0) FROM dbo.Sys_Users_Goods", 1, 20);
        var maxBefore = UserRow.ParseInt(before.FirstCell());

        var now = DateTime.Now;
        var (rows, error) = await _db.ExecuteAsync(Conn,
            "EXEC dbo.SP_Admin_SendUserItem " +
            "@ItemID = 0, @UserID = @uid, @TemplateID = @tpl, @Place = -1, @Count = @count, @IsJudge = 0, @Color = N'', @IsExist = 1, " +
            "@StrengthenLevel = @strength, @AttackCompose = @attack, @DefendCompose = @defend, @LuckCompose = @luck, @AgilityCompose = @agility, " +
            "@IsBinds = @bind, @BeginDate = @now, @ValidDate = @days, @BagType = 1, " +
            "@ID = 0, @SenderID = 0, @Sender = N'GM Panel', @ReceiverID = @uid, @Receiver = N'', " +
            "@Title = @title, @Content = @content, @SendTime = @now, @IsRead = 0, @IsDelR = 0, @IfDelS = 0, @IsDelete = 0, " +
            "@Annex1 = N'', @Annex2 = N'', @Gold = 0, @Money = 0",
            new Dictionary<string, object?>
            {
                ["uid"] = id,
                ["tpl"] = templateId,
                ["count"] = count,
                ["bind"] = bind,
                ["now"] = now,
                ["days"] = validDays,
                ["attack"] = attack,
                ["defend"] = defend,
                ["luck"] = luck,
                ["agility"] = agility,
                ["goldDays"] = goldDays,
                ["strength"] = strengthLevel,
                ["title"] = titleText,
                ["content"] = contentText
            });

        if (error != null)
            return (false, "Lỗi gửi vật phẩm: " + error);

        string? goldWarning = null;
        if (goldDays > 0)
        {
            var (goldRows, goldError) = await _db.ExecuteAsync(Conn,
                "UPDATE dbo.Sys_Users_Goods SET IsGold = 1, goldBeginTime = @now, goldValidDate = @days " +
                "WHERE ItemID > @before AND UserID = 0 AND TemplateID = @tpl",
                new Dictionary<string, object?>
                {
                    ["now"] = now,
                    ["days"] = goldDays,
                    ["before"] = maxBefore,
                    ["tpl"] = templateId
                });
            if (goldError != null) goldWarning = " (cảnh báo: lỗi đúc vàng — " + goldError + ")";
            else if (goldRows <= 0) goldWarning = " (cảnh báo: không tìm thấy item để set đúc vàng)";
        }

        var statText = hasStats
            ? $" · Công +{attack}, Thủ +{defend}, Nhanh nhẹn +{agility}, May mắn +{luck}"
            : "";
        var levelText = strengthLevel > 0 ? $" · Cấp độ +{strengthLevel}" : "";
        var goldText = goldDays > 0 ? $" · Đúc vàng {goldDays} ngày" : "";

        bool? mailNotified = null;
        if (user.State > 0)
            mailNotified = await NotifyOnlineMailAsync(id);
        var notifyText = mailNotified == true
            ? " Đã gửi thông báo mail tới client đang online."
            : mailNotified == false
                ? " Chưa gửi được thông báo mail online (người chơi mở hộp thư để nhận)."
                : "";

        var message = "Vật phẩm đã gửi về người dùng";
        _audit.Write(User.Identity?.Name ?? "unknown", "user.award",
            $"id={id} nick={user.NickName} templateId={templateId} count={count} bind={bind} validDays={validDays} " +
            $"attack={attack} defend={defend} agility={agility} luck={luck} strengthLevel={strengthLevel} goldDays={goldDays} " +
            $"mailNotified={mailNotified} title={titleText}");
        return (true, message);
    }

    /// <summary>Gọi CenterService MailNotice (net.tcp 127.0.0.1:2009) để client đang online cập nhật hộp thư ngay.</summary>
    private async Task<bool?> NotifyOnlineMailAsync(int userId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_mailNoticeScript) || !System.IO.File.Exists(_mailNoticeScript)) return null;
            var powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                "WindowsPowerShell", "v1.0", "powershell.exe");
            if (!System.IO.File.Exists(powershell)) return null;

            var psi = new ProcessStartInfo
            {
                FileName = powershell,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            psi.ArgumentList.Add("-NoProfile");
            psi.ArgumentList.Add("-ExecutionPolicy");
            psi.ArgumentList.Add("Bypass");
            psi.ArgumentList.Add("-File");
            psi.ArgumentList.Add(_mailNoticeScript);
            psi.ArgumentList.Add("-UserId");
            psi.ArgumentList.Add(userId.ToString());
            psi.ArgumentList.Add("-ZoneId");
            psi.ArgumentList.Add(_zoneId.ToString());

            using var process = Process.Start(psi);
            if (process == null) return null;
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(true); } catch { }
                return false;
            }
            var output = (await outputTask).Trim();
            var error = (await errorTask).Trim();
            if (string.Equals(output, "True", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(output, "False", StringComparison.OrdinalIgnoreCase)) return false;
            return error.Length > 0 ? false : null;
        }
        catch
        {
            return false;
        }
    }

    private async Task<(string Name, int CategoryId, string CategoryName, int MaxCount, string Pic, int NeedSex)?> LoadTemplateAsync(int templateId)
    {
        var t = await _db.QueryAsync("Db_Tank_All",
            "SELECT TOP 1 g.Name, g.MaxCount, g.CategoryID, c.Name AS CategoryName, g.Pic, g.NeedSex " +
            "FROM dbo.Shop_Goods g LEFT JOIN Db_Gm.dbo.Category c ON c.CategoryID = g.CategoryID " +
            "WHERE g.TemplateID = @tpl",
            new Dictionary<string, object?> { ["tpl"] = templateId }, 1, 20);
        if (!t.Ok || t.Rows.Count == 0) return null;
        var r = t.Rows[0];
        return (
            r.ElementAtOrDefault(0) ?? ("TemplateID " + templateId),
            UserRow.ParseInt(r.ElementAtOrDefault(2)),
            r.ElementAtOrDefault(3) ?? "",
            UserRow.ParseInt(r.ElementAtOrDefault(1)),
            r.ElementAtOrDefault(4) ?? "",
            UserRow.ParseInt(r.ElementAtOrDefault(5))
        );
    }

    /// <summary>AJAX: thông tin TemplateID (tên/category/hỗ trợ đúc vàng + chỉ số) để UI tự bật/tắt.</summary>
    public async Task<IActionResult> OnGetTemplateInfoAsync(int templateId)
    {
        var t = await LoadTemplateAsync(templateId);
        if (t == null) return new JsonResult(new { ok = false });
        var (name, categoryId, categoryName, maxCount, _, _) = t.Value;
        return new JsonResult(new
        {
            ok = true,
            name,
            categoryId,
            categoryName,
            maxCount,
            allowGold = GoldCategories.Contains(categoryId),
            allowStats = StatCategories.Contains(categoryId),
            allowStrength = StrengthCategories.Contains(categoryId)
        });
    }
}
