using GunnyPanel.Services;
using GunnySuite.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GunnyPanel.Pages;

public sealed class LiveUserRow
{
    public int UserID { get; set; }
    public string UserName { get; set; } = "";
    public string NickName { get; set; } = "";
    public int Grade { get; set; }
    public int State { get; set; }
    public string LastDate { get; set; } = "";

    public static LiveUserRow From(string?[] row) => new()
    {
        UserID = UserRow.ParseInt(row.ElementAtOrDefault(0)),
        UserName = row.ElementAtOrDefault(1) ?? "",
        NickName = row.ElementAtOrDefault(2) ?? "",
        Grade = UserRow.ParseInt(row.ElementAtOrDefault(3)),
        State = UserRow.ParseInt(row.ElementAtOrDefault(4)),
        LastDate = UserRow.FormatDate(row.ElementAtOrDefault(5))
    };
}

/// <summary>
/// Server Live: điều khiển live qua CenterService có sẵn (thông báo / kick / trạng thái / reload / chat).
/// Không sửa Full Source hay Service — chỉ gọi WCF endpoint basicHttp của CenterService.
/// </summary>
public sealed class ServerLiveModel : PageModel
{
    public const string Conn = "Db_Tank";

    private readonly SqlDatabase _db;
    private readonly RoadLiveService _live;
    private readonly AuditLog _audit;

    public ServerLiveModel(SqlDatabase db, RoadLiveService live, AuditLog audit)
    {
        _db = db;
        _live = live;
        _audit = audit;
    }

    public bool CanWrite => User.IsInRole("Develop") || User.IsInRole("Admin");
    public bool CanShutdown => User.IsInRole("Develop") && _live.ShutdownEnabled;
    public bool ShutdownEnabled => _live.ShutdownEnabled;
    public string CenterUrl => _live.CenterUrl;
    public int ZoneId => _live.ZoneId;
    public bool HasLoginKey => _live.HasLoginKey;
    public (string Type, string Label)[] ReloadOptions => RoadLiveService.ReloadOptions;

    public string? Error { get; private set; }
    public bool CenterOk { get; private set; }
    public string CenterMessage { get; private set; } = "";
    public List<LiveUserRow> Online { get; } = new();
    public int OnlineCount { get; private set; }
    public int TotalUsers { get; private set; }
    public List<RoadLiveService.ChatEntry> Chats { get; } = new();
    public string ChatMessage { get; private set; } = "";

    public async Task OnGetAsync()
    {
        await LoadStatsAsync();

        var pingTask = _live.PingAsync();
        var chatTask = _live.GetChatAsync();
        var ping = await pingTask;
        CenterOk = ping.Ok;
        CenterMessage = ping.Message;
        var chat = await chatTask;
        Chats.AddRange(chat.Chats);
        ChatMessage = chat.Message;
    }

    private async Task LoadStatsAsync()
    {
        var online = await _db.QueryAsync(Conn,
            "SELECT UserID, UserName, NickName, Grade, State, LastDate " +
            "FROM dbo.Sys_Users_Detail WHERE State > 0 ORDER BY Grade DESC, UserID", 200, 20);
        if (!online.Ok)
        {
            Error = online.Error;
            return;
        }
        foreach (var row in online.Rows) Online.Add(LiveUserRow.From(row));
        OnlineCount = Online.Count;

        var total = await _db.QueryAsync(Conn, "SELECT COUNT(*) FROM dbo.Sys_Users_Detail", 1, 20);
        if (total.Ok) TotalUsers = UserRow.ParseInt(total.FirstCell());
    }

    // ------------------------------------------------------------------ AJAX handlers

    private IActionResult Fail(string message, int status = 400) =>
        new JsonResult(new { ok = false, message }) { StatusCode = status };

    /// <summary>Chat gần đây (giải mã gzip từ CenterService) — read-only.</summary>
    public async Task<IActionResult> OnGetChatAsync()
    {
        var chat = await _live.GetChatAsync();
        return new JsonResult(new
        {
            ok = chat.Ok,
            message = chat.Message,
            chats = chat.Chats.Select(c => new
            {
                time = c.Time.ToString("dd/MM/yyyy HH:mm:ss"),
                nick = c.NickName,
                message = c.Message,
                type = c.Type,
                zone = c.ZoneId
            })
        });
    }

    /// <summary>Hỏi trạng thái online thật từ CenterService + DB State (read-only).</summary>
    public async Task<IActionResult> OnGetPlayerStateAsync(int userId)
    {
        if (userId <= 0) return Fail("UserID không hợp lệ.");

        var stateTask = _live.GetPlayerStateAsync(userId);
        var user = await _db.QueryAsync(Conn,
            "SELECT TOP 1 NickName, State FROM dbo.Sys_Users_Detail WHERE UserID = @id",
            new Dictionary<string, object?> { ["id"] = userId }, 1, 15);
        var nick = "";
        var dbState = 0;
        if (user.Ok && user.Rows.Count > 0)
        {
            nick = user.Rows[0].ElementAtOrDefault(0) ?? "";
            dbState = UserRow.ParseInt(user.Rows[0].ElementAtOrDefault(1));
        }
        var state = await stateTask;
        if (!state.Ok && user.Ok && user.Rows.Count == 0)
            return Fail("Không tìm thấy người dùng UserID=" + userId + ".");
        return new JsonResult(new
        {
            ok = state.Ok,
            message = state.Message,
            nick,
            dbState,
            centerState = state.State
        });
    }

    public async Task<IActionResult> OnPostNoticeAsync(string? message, int type)
    {
        if (!CanWrite) return Fail("Bạn không có quyền thao tác live.", 403);
        var result = await _live.SendNoticeAsync(message, type);
        _audit.Write(User.Identity?.Name ?? "unknown", "road.notice",
            $"type={type} ok={result.Ok} message={Safe(message)}");
        return result.Ok ? new JsonResult(new { ok = true, message = result.Message }) : Fail(result.Message);
    }

    public async Task<IActionResult> OnPostKickAsync(int userId, string? reason)
    {
        if (!CanWrite) return Fail("Bạn không có quyền thao tác live.", 403);
        if (userId <= 0) return Fail("UserID không hợp lệ.");
        var result = await _live.KickAsync(userId, reason);
        _audit.Write(User.Identity?.Name ?? "unknown", "road.kick",
            $"id={userId} ok={result.Ok} reason={Safe(reason)}");
        return result.Ok ? new JsonResult(new { ok = true, message = result.Message }) : Fail(result.Message);
    }

    public async Task<IActionResult> OnPostReloadAsync(string? type)
    {
        if (!CanWrite) return Fail("Bạn không có quyền thao tác live.", 403);
        var result = await _live.ReloadAsync(type);
        _audit.Write(User.Identity?.Name ?? "unknown", "road.reload",
            $"type={Safe(type)} ok={result.Ok}");
        return result.Ok ? new JsonResult(new { ok = true, message = result.Message }) : Fail(result.Message);
    }

    public async Task<IActionResult> OnPostReloadServerListAsync()
    {
        if (!CanWrite) return Fail("Bạn không có quyền thao tác live.", 403);
        var result = await _live.ReloadServerListAsync();
        _audit.Write(User.Identity?.Name ?? "unknown", "road.serverlist", $"ok={result.Ok}");
        return result.Ok ? new JsonResult(new { ok = true, message = result.Message }) : Fail(result.Message);
    }

    public async Task<IActionResult> OnPostShutdownAsync(int zoneId, string? confirmText)
    {
        if (!CanShutdown)
            return Fail("Shutdown yêu cầu role Develop và Panel:EnableShutdown=true.", 403);
        if (!string.Equals((confirmText ?? "").Trim(), "SHUTDOWN", StringComparison.Ordinal))
            return Fail("Gõ đúng chữ SHUTDOWN để xác nhận lớp 2.");
        var result = await _live.ShutdownAsync(zoneId);
        _audit.Write(User.Identity?.Name ?? "unknown", "road.shutdown",
            $"zone={zoneId} ok={result.Ok}");
        return result.Ok ? new JsonResult(new { ok = true, message = result.Message }) : Fail(result.Message);
    }

    private static string Safe(string? value)
    {
        var text = (value ?? "").Trim();
        if (text.Length > 300) text = text[..300];
        return text.Replace("\r", " ").Replace("\n", " ");
    }
}
