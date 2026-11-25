namespace GunnyPanel.Pages.Shared;

/// <summary>Model phân trang dùng chung cho các trang.</summary>
public sealed class Pager
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public int Total { get; init; }
    public string Path { get; init; } = "";
    public string PageParam { get; init; } = "page";
    public Dictionary<string, string?> Query { get; init; } = new();

    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)Math.Max(1, PageSize)));
    public int ClampPage => Math.Clamp(Page, 1, TotalPages);

    public static int Clamp(int page, int total, int pageSize)
    {
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)Math.Max(1, pageSize)));
        return Math.Clamp(page, 1, pages);
    }

    public string UrlFor(int page)
    {
        var parts = new List<string>();
        foreach (var kv in Query)
        {
            if (string.IsNullOrEmpty(kv.Value)) continue;
            if (string.Equals(kv.Key, PageParam, StringComparison.OrdinalIgnoreCase)) continue;
            parts.Add(Uri.EscapeDataString(kv.Key) + "=" + Uri.EscapeDataString(kv.Value));
        }
        parts.Add(Uri.EscapeDataString(PageParam) + "=" + page);
        var qs = string.Join("&", parts);
        return string.IsNullOrEmpty(qs) ? Path : Path + "?" + qs;
    }
}
