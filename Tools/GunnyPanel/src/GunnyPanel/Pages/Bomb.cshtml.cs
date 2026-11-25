using GunnyPanel.Pages.Shared;
using GunnySuite.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GunnyPanel.Pages;

public sealed class BombModel : PageModel
{
    public const int PageSize = 24;

    private readonly IConfiguration _cfg;
    private readonly AuditLog _audit;

    public BombModel(IConfiguration cfg, AuditLog audit)
    {
        _cfg = cfg;
        _audit = audit;
    }

    public string SourceDir { get; private set; } = "";
    public string FightDir { get; private set; } = "";
    public string PngDir { get; private set; } = "";
    public string BombDir { get; private set; } = "";
    public string UploadDir { get; private set; } = "";

    public int SourceCount { get; private set; }
    public int FightCount { get; private set; }
    public int PngTotal { get; private set; }
    public int BombTotal { get; private set; }
    public List<string> PngFiles { get; private set; } = new();
    public List<string> BombFiles { get; private set; } = new();

    [BindProperty(SupportsGet = true, Name = "ppage")] public int PngPage { get; set; } = 1;
    [BindProperty(SupportsGet = true, Name = "bpage")] public int BombPage { get; set; } = 1;

    [BindProperty] public IFormFile? Upload { get; set; }
    [BindProperty] public int Threshold { get; set; } = 128;
    public string? Message { get; private set; }
    public string? Error { get; private set; }

    public void OnGet()
    {
        Init();
        PngTotal = ListFiles(PngDir, "*.png").Count;
        BombTotal = ListFiles(BombDir, "*.bomb").Count;
        PngPage = Pager.Clamp(PngPage, PngTotal, PageSize);
        BombPage = Pager.Clamp(BombPage, BombTotal, PageSize);
        PngFiles = ListFiles(PngDir, "*.png").Skip((PngPage - 1) * PageSize).Take(PageSize).ToList();
        BombFiles = ListFiles(BombDir, "*.bomb").Skip((BombPage - 1) * PageSize).Take(PageSize).ToList();
    }

    private void Init()
    {
        var dataDir = _cfg["Paths:DataDir"] ?? Path.Combine(AppContext.BaseDirectory, "data");
        PngDir = Path.Combine(dataDir, "out", "bomb-png");
        BombDir = Path.Combine(dataDir, "out", "bomb");
        UploadDir = Path.Combine(dataDir, "out", "uploads");
        var gameRoot = _cfg["Paths:GameRoot"] ?? "";
        SourceDir = _cfg["Paths:BombDir"] ?? Path.Combine(gameRoot, "Service", "Service", "Road", "Bomb");
        FightDir = Path.Combine(gameRoot, "Service", "Service", "Fight", "Bomb");
        Directory.CreateDirectory(PngDir);
        Directory.CreateDirectory(BombDir);
        Directory.CreateDirectory(UploadDir);
        SourceCount = ListFiles(SourceDir, "*.bomb").Count;
        FightCount = ListFiles(FightDir, "*.bomb").Count;
    }

    public async Task<IActionResult> OnPostUploadAsync()
    {
        Init();
        try
        {
            if (Upload == null || Upload.Length == 0)
            {
                Error = "Chưa chọn file.";
                return Page();
            }
            if (Upload.Length > 32 * 1024 * 1024)
            {
                Error = "File quá lớn (giới hạn 32 MB).";
                return Page();
            }

            var rawName = Path.GetFileName(Upload.FileName ?? "");
            if (string.IsNullOrWhiteSpace(rawName)) rawName = "upload" + DateTime.Now.ToString("HHmmss");
            var safeName = Safe(rawName);
            var ext = Path.GetExtension(safeName).ToLowerInvariant();
            var uploadPath = Path.Combine(UploadDir, DateTime.Now.ToString("yyyyMMdd-HHmmss-") + safeName);

            await using (var stream = System.IO.File.Create(uploadPath))
                await Upload.CopyToAsync(stream);

            var baseName = Path.GetFileNameWithoutExtension(safeName);
            if (ext == ".bomb")
            {
                var mask = Bomb.Read(uploadPath);
                var outPath = Path.Combine(PngDir, baseName + ".png");
                Bomb.ToPng(mask, outPath);
                Message = $"Đã convert {safeName} → {baseName}.png ({mask.Width}×{mask.Height}, mask={mask.Count} pixel).";
            }
            else if (ext == ".png")
            {
                var mask = Bomb.FromPng(uploadPath, Math.Clamp(Threshold, 1, 254));
                var outPath = Path.Combine(BombDir, baseName + ".bomb");
                Bomb.Write(outPath, mask);
                Message = $"Đã convert {safeName} → {baseName}.bomb ({mask.Width}×{mask.Height}, mask={mask.Count} pixel).";
            }
            else
            {
                Error = "Chỉ hỗ trợ file .png hoặc .bomb.";
                return Page();
            }

            _audit.Write(User.Identity?.Name ?? "unknown", "bomb.upload", safeName + " -> " + Message);
        }
        catch (Exception ex)
        {
            Error = "Lỗi convert: " + ex.Message;
        }
        return Page();
    }

    public IActionResult OnGetImage(string name)
    {
        Init();
        var path = SafePath(PngDir, name);
        if (path == null) return NotFound();
        return PhysicalFile(path, "image/png");
    }

    public IActionResult OnGetDownload(string name, string? kind)
    {
        Init();
        var dir = string.Equals(kind, "bomb", StringComparison.OrdinalIgnoreCase) ? BombDir : PngDir;
        var path = SafePath(dir, name);
        if (path == null) return NotFound();
        var type = Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "application/octet-stream";
        return PhysicalFile(path, type, Path.GetFileName(path));
    }

    private static List<string> ListFiles(string dir, string pattern)
    {
        try
        {
            if (!Directory.Exists(dir)) return new List<string>();
            return new DirectoryInfo(dir).GetFiles(pattern)
                .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                .Select(f => f.Name)
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    private static string Safe(string name)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var ch in name)
            sb.Append(char.IsLetterOrDigit(ch) || ch is '_' or '-' or '.' ? ch : '_');
        var result = sb.ToString();
        return string.IsNullOrWhiteSpace(result) ? "file" : result;
    }

    private static string? SafePath(string dir, string? name)
    {
        var safeName = Path.GetFileName(name ?? "");
        if (string.IsNullOrWhiteSpace(safeName)) return null;
        var path = Path.Combine(dir, safeName);
        return System.IO.File.Exists(path) ? path : null;
    }
}
