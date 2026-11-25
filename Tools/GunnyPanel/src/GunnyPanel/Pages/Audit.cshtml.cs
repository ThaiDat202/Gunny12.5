using GunnyPanel.Pages.Shared;
using GunnySuite.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GunnyPanel.Pages;

public sealed class AuditModel : PageModel
{
    public const int PageSize = 20;
    public const int MaxEntries = 2000;

    private readonly AuditLog _audit;

    public AuditModel(AuditLog audit) => _audit = audit;

    public IReadOnlyList<AuditEntry> Entries { get; private set; } = Array.Empty<AuditEntry>();
    public int Total { get; private set; }

    public int PageIndex { get; private set; } = 1;

    public Pager Pager { get; private set; } = new();

    public void OnGet([FromQuery(Name = "page")] int page = 1)
    {
        // "page" là route value dành riêng của Razor Pages → đọc thủ công từ query string.
        PageIndex = page > 0 ? page : 1;
        var all = _audit.Tail(MaxEntries);
        Total = all.Count;
        PageIndex = Pager.Clamp(PageIndex, Total, PageSize);
        Entries = all.Skip((PageIndex - 1) * PageSize).Take(PageSize).ToList();
        Pager = new Pager
        {
            Page = PageIndex,
            PageSize = PageSize,
            Total = Total,
            Path = "/Audit",
            PageParam = "page"
        };
    }
}
