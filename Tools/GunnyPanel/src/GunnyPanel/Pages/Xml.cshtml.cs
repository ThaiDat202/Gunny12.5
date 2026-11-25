using System.Xml.Linq;
using GunnyPanel.Pages.Shared;
using GunnySuite.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GunnyPanel.Pages;

public sealed class XmlFileItem
{
    public string Name { get; set; } = "";
    public long Size { get; set; }
    public DateTime Modified { get; set; }
}

public sealed class XmlModel : PageModel
{
    public const int FilePageSize = 20;
    public const int RowPageSize = 25;

    private readonly IConfiguration _cfg;
    private readonly AuditLog _audit;

    public XmlModel(IConfiguration cfg, AuditLog audit)
    {
        _cfg = cfg;
        _audit = audit;
    }

    public List<XmlFileItem> Files { get; private set; } = new();
    public List<XmlFileItem> PageFiles { get; private set; } = new();
    public int FileTotal { get; private set; }
    public string? Error { get; private set; }

    [BindProperty(SupportsGet = true, Name = "file")] public string? SelectedFile { get; set; }
    [BindProperty(SupportsGet = true, Name = "fpage")] public int FilePage { get; set; } = 1;
    [BindProperty(SupportsGet = true, Name = "rpage")] public int RowsPage { get; set; } = 1;
    [BindProperty] public IFormFile? PackUpload { get; set; }
    public string? PackMessage { get; private set; }
    public string? PackError { get; private set; }

    public string? Selected { get; private set; }
    public string RootName { get; private set; } = "";
    public int ChildCount { get; private set; }
    public bool Decompressed { get; private set; }
    public long RawSize { get; private set; }
    public long PlainSize { get; private set; }
    public List<string> Columns { get; } = new();
    public List<string[]> Rows { get; } = new();

    private string MirrorDir => _cfg["Paths:MirrorDir"] ?? "";

    public void OnGet()
    {
        LoadFileList();

        if (string.IsNullOrWhiteSpace(SelectedFile))
            SelectedFile = PageFiles.FirstOrDefault()?.Name;
        if (string.IsNullOrWhiteSpace(SelectedFile)) return;

        var safeName = Path.GetFileName(SelectedFile);
        Selected = safeName;
        var path = Path.Combine(MirrorDir, safeName);
        if (!System.IO.File.Exists(path))
        {
            Error = "Không thấy file: " + safeName;
            return;
        }

        try
        {
            var data = System.IO.File.ReadAllBytes(path);
            RawSize = data.Length;
            if (Zlib.LooksZlib(data))
            {
                data = Zlib.Decompress(data);
                Decompressed = true;
            }
            PlainSize = data.Length;

            XDocument doc;
            try
            {
                doc = XDocument.Load(new MemoryStream(data));
            }
            catch (Exception ex)
            {
                Error = "XML không hợp lệ: " + ex.Message;
                return;
            }

            RootName = doc.Root?.Name.LocalName ?? "";
            var items = doc.Root?.Elements().ToList() ?? new List<XElement>();
            ChildCount = items.Count;

            var first = items.FirstOrDefault();
            if (first == null) return;

            foreach (var attr in first.Attributes()) Columns.Add(attr.Name.LocalName);

            RowsPage = Pager.Clamp(RowsPage, ChildCount, RowPageSize);
            foreach (var item in items.Skip((RowsPage - 1) * RowPageSize).Take(RowPageSize))
            {
                var row = new string[Columns.Count];
                for (var i = 0; i < Columns.Count; i++)
                    row[i] = GetAttr(item, Columns[i]);
                Rows.Add(row);
            }
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    private void LoadFileList()
    {
        try
        {
            if (!Directory.Exists(MirrorDir)) return;
            Files = new DirectoryInfo(MirrorDir)
                .GetFiles("*.xml")
                .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                .Take(1000)
                .Select(f => new XmlFileItem { Name = f.Name, Size = f.Length, Modified = f.LastWriteTime })
                .ToList();
            FileTotal = Files.Count;
            FilePage = Pager.Clamp(FilePage, FileTotal, FilePageSize);
            PageFiles = Files.Skip((FilePage - 1) * FilePageSize).Take(FilePageSize).ToList();
        }
        catch { }
    }

    public IActionResult OnGetDownload(string? file)
    {
        var safeName = Path.GetFileName(file ?? SelectedFile ?? "");
        if (string.IsNullOrWhiteSpace(safeName)) return NotFound();
        var path = Path.Combine(MirrorDir, safeName);
        if (!System.IO.File.Exists(path)) return NotFound();

        var data = System.IO.File.ReadAllBytes(path);
        if (Zlib.LooksZlib(data)) data = Zlib.Decompress(data);
        var downloadName = Path.GetFileNameWithoutExtension(safeName) + ".xml";
        return File(data, "application/xml; charset=utf-8", downloadName);
    }

    /// <summary>Tải bản nén zlib của file đang chọn (giống ToolRip CreateCompressXml).</summary>
    public IActionResult OnGetPackDownload(string? file)
    {
        var safeName = Path.GetFileName(file ?? SelectedFile ?? "");
        if (string.IsNullOrWhiteSpace(safeName)) return NotFound();
        var path = Path.Combine(MirrorDir, safeName);
        if (!System.IO.File.Exists(path)) return NotFound();

        var data = System.IO.File.ReadAllBytes(path);
        if (Zlib.LooksZlib(data)) data = Zlib.Decompress(data);
        var packed = Zlib.Compress(data);
        return File(packed, "application/xml", Path.GetFileNameWithoutExtension(safeName) + ".xml");
    }

    /// <summary>Upload XML (plain/zlib) -> nén zlib và trả về file .xml tương thích request server.</summary>
    public async Task<IActionResult> OnPostPackAsync()
    {
        if (PackUpload == null || PackUpload.Length == 0)
        {
            PackError = "Chưa chọn file XML.";
            OnGet();
            return Page();
        }
        if (PackUpload.Length > 32 * 1024 * 1024)
        {
            PackError = "File quá lớn (giới hạn 32 MB).";
            OnGet();
            return Page();
        }

        try
        {
            using var ms = new MemoryStream();
            await PackUpload.CopyToAsync(ms);
            var bytes = ms.ToArray();
            if (Zlib.LooksZlib(bytes)) bytes = Zlib.Decompress(bytes);
            var packed = Zlib.Compress(bytes);
            var name = Path.GetFileNameWithoutExtension(Path.GetFileName(PackUpload.FileName ?? "xml"));
            if (string.IsNullOrWhiteSpace(name)) name = "xml";
            _audit.Write(User.Identity?.Name ?? "unknown", "xml.pack", PackUpload.FileName + " -> " + packed.Length + " bytes");
            return File(packed, "application/xml", name + ".xml");
        }
        catch (Exception ex)
        {
            PackError = "Lỗi nén XML: " + ex.Message;
            OnGet();
            return Page();
        }
    }

    private static string GetAttr(XElement item, string name) =>
        item.Attributes()
            .FirstOrDefault(a => string.Equals(a.Name.LocalName, name, StringComparison.OrdinalIgnoreCase))
            ?.Value ?? "";
}
