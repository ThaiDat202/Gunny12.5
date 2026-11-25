using GunnySuite.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GunnyPanel.Pages;

public sealed class ItemDetailModel : PageModel
{
    public const string Conn = "Db_Tank";

    private readonly SqlDatabase _db;

    public ItemDetailModel(SqlDatabase db) => _db = db;

    [BindProperty(SupportsGet = true)] public int Id { get; set; }
    [BindProperty(SupportsGet = true)] public int? Uid { get; set; }

    public string? Error { get; private set; }
    public string OwnerNick { get; private set; } = "";
    public int OwnerId { get; private set; }
    public List<(string Column, string Value)> Goods { get; } = new();
    public List<(string Column, string Value)> Template { get; } = new();

    private static readonly string[] TemplateColumns =
    {
        "Name", "CategoryID", "NeedLevel", "Quality", "Attack", "Defence", "Agility", "Luck",
        "CanStrengthen", "CanCompose", "CanEquip", "CanUse", "Description"
    };

    public async Task OnGetAsync()
    {
        if (Id <= 0)
        {
            Error = "Thiếu ItemID.";
            return;
        }

        var item = await _db.QueryAsync(Conn,
            "SELECT g.*, s.Name AS TplName, s.CategoryID, s.Level AS NeedLevel, s.Quality, s.Attack, s.Defence, s.Agility, s.Luck, " +
            "s.CanStrengthen, s.CanCompose, s.CanEquip, s.CanUse, s.Description " +
            "FROM dbo.Sys_Users_Goods g LEFT JOIN dbo.Shop_Goods s ON s.TemplateID = g.TemplateID " +
            "WHERE g.ItemID = @id",
            new Dictionary<string, object?> { ["id"] = Id }, 1, 20);
        if (!item.Ok)
        {
            Error = item.Error;
            return;
        }
        if (item.Rows.Count == 0)
        {
            Error = $"Không tìm thấy vật phẩm ItemID={Id}.";
            return;
        }

        var row = item.Rows[0];
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < item.Columns.Count; i++)
            values[item.Columns[i]] = row.ElementAtOrDefault(i) ?? "";

        foreach (var col in item.Columns)
        {
            if (col.Equals("TplName", StringComparison.OrdinalIgnoreCase) ||
                TemplateColumns.Contains(col, StringComparer.OrdinalIgnoreCase)) continue;
            Goods.Add((col, values[col]));
        }

        foreach (var col in TemplateColumns)
        {
            var key = col switch
            {
                "Name" => "TplName",
                _ => col
            };
            if (values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                Template.Add((col, value));
        }

        OwnerId = UserRow.ParseInt(values.GetValueOrDefault("UserID"));
        if (OwnerId > 0)
        {
            var owner = await _db.QueryAsync(Conn,
                "SELECT TOP 1 NickName FROM dbo.Sys_Users_Detail WHERE UserID = @uid",
                new Dictionary<string, object?> { ["uid"] = OwnerId }, 1, 15);
            if (owner.Ok && owner.Rows.Count > 0)
                OwnerNick = owner.Rows[0].ElementAtOrDefault(0) ?? "";
        }
        if (Uid.HasValue && Uid.Value > 0) OwnerId = Uid.Value;
    }
}
