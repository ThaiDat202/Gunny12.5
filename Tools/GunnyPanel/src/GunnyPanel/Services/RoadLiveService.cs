using System.Globalization;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace GunnyPanel.Services;

/// <summary>
/// Gọi <b>CenterService</b> có sẵn của Gunny (WCF net.tcp 127.0.0.1:2009, đồng thời host
/// basicHttp tại http://127.0.0.1:4109/CenterServicePhP/) — KHÔNG sửa Full Source / Service.
///
/// Dùng SOAP raw qua HttpClient để không phụ thuộc package WCF client trên .NET 10.
/// Các operation đã kiểm chứng trong <c>Full Source\center.server\Center.Server\CenterService.cs</c>:
/// SystemNotice (cần chữ ký), KitoffUser, GetPlayerState, GetAllChat, ReLoadServerList,
/// Reload (cần chữ ký), Shutdown (cần chữ ký), MailNotice.
///
/// Chữ ký: MD5(nội_dung + LoginKey) lấy từ <c>Center.Service.exe.config</c> (key LoginKey).
/// </summary>
public sealed class RoadLiveService
{
    private static readonly XNamespace SoapNs = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace TempuriNs = "http://tempuri.org/";
    private static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };

    public sealed record ChatEntry(DateTime Time, string NickName, string Message, int Type, int ZoneId);

    /// <summary>Các loại Reload của game (<c>eReloadType</c>) cho phép chọn từ panel.</summary>
    public static readonly (string Type, string Label)[] ReloadOptions =
    {
        ("notice", "Thông báo hệ thống (notice)"),
        ("clearchat", "Xoá bộ đệm chat (clearchat)"),
        ("center", "Center — cấu hình + server list (center)"),
        ("server", "Server (server)"),
        ("serverconfig", "Server config (serverconfig)"),
        ("rate", "Tỉ lệ kinh nghiệm (rate)"),
        ("ball", "Ball (ball)"),
        ("ballconfig", "Ball config (ballconfig)"),
        ("map", "Map (map)"),
        ("npc", "NPC (npc)"),
        ("pve", "PVE (pve)"),
        ("prop", "Prop (prop)"),
        ("item", "Item (item)"),
        ("quest", "Quest (quest)"),
        ("fusion", "Fusion (fusion)"),
        ("fight", "Fight (fight)"),
        ("consortia", "Consortia (consortia)"),
        ("shop", "Shop (shop)"),
        ("shopbox", "Shop box (shopbox)"),
        ("drop", "Drop (drop)"),
        ("mission", "Mission (mission)"),
        ("pet", "Pet (pet)"),
        ("mount", "Mount (mount)"),
        ("rune", "Rune (rune)"),
        ("level", "Level (level)"),
        ("strengthen", "Strengthen (strengthen)"),
        ("active", "Active (active)"),
        ("language", "Language (language)"),
        ("card", "Card (card)"),
        ("newtitle", "Danh hiệu mới (newtitle)"),
        ("achievement", "Achievement (achievement)"),
        ("totem", "Totem (totem)"),
        ("suit", "Suit (suit)"),
        ("magicstone", "Ma thạch (magicstone)")
    };

    private readonly string _centerUrl;
    private readonly string _loginKey;
    private readonly int _zoneId;
    private readonly int _timeoutSeconds;

    public RoadLiveService(IConfiguration cfg)
    {
        _centerUrl = (cfg["Panel:CenterServiceUrl"] ?? "http://127.0.0.1:4109/CenterServicePhP/").Trim();
        if (!_centerUrl.EndsWith('/')) _centerUrl += "/";
        _zoneId = int.TryParse(cfg["Panel:ZoneId"], out var zone) ? zone : 1001;
        _timeoutSeconds = int.TryParse(cfg["Panel:CenterServiceTimeoutSeconds"], out var timeout)
            ? Math.Clamp(timeout, 2, 60)
            : 10;
        ShutdownEnabled = bool.TryParse(cfg["Panel:EnableShutdown"], out var shutdown) && shutdown;
        _loginKey = ResolveLoginKey(cfg);
    }

    public string CenterUrl => _centerUrl;
    public int ZoneId => _zoneId;
    public bool ShutdownEnabled { get; }
    public bool HasLoginKey => _loginKey.Length > 0;

    // ------------------------------------------------------------------ public API

    /// <summary>Kiểm tra CenterService có phản hồi (gọi AASGetState — read-only).</summary>
    public async Task<(bool Ok, string Message)> PingAsync(CancellationToken ct = default)
    {
        var (ok, payload, error) = await CallAsync("AASGetState", new XElement(TempuriNs + "AASGetState"), ct);
        if (!ok) return (false, error);
        var state = ParseIntResult(payload, "AASGetState");
        var aas = state == 1 ? "bật" : state == 0 ? "tắt" : "không xác định";
        return (true, $"CenterService phản hồi — AAS {aas}.");
    }

    /// <summary>Thông báo hệ thống toàn server (SystemNotice). type: 0 = GM notice, 3 = system notice, 4 = auto notice.</summary>
    public async Task<(bool Ok, string Message)> SendNoticeAsync(string? message, int type, CancellationToken ct = default)
    {
        var text = (message ?? "").Trim();
        if (text.Length == 0) return (false, "Nội dung thông báo trống.");
        if (text.Contains('|'))
            return (false, "Nội dung thông báo không được chứa ký tự '|' (CenterService dùng '|' để tách chữ ký).");
        if (text.Length > 500) text = text[..500];
        if (type is < 0 or > 4) type = 3;
        if (!HasLoginKey)
            return (false, "Thiếu LoginKey — cấu hình Panel:LoginKey hoặc Paths:CenterServiceConfig.");

        var payload = text + "|" + type.ToString(CultureInfo.InvariantCulture) + "|" + Md5(text + _loginKey);
        var (ok, xml, error) = await CallAsync("SystemNotice",
            new XElement(TempuriNs + "SystemNotice", Arg("msg", payload)), ct);
        if (!ok) return (false, error);
        return ParseBoolResult(xml, "SystemNotice")
            ? (true, $"Đã gửi thông báo tới toàn server: “{text}”")
            : (false, "CenterService từ chối thông báo (kiểm tra LoginKey).");
    }

    /// <summary>Kick người chơi đang online khỏi zone hiện tại (KitoffUser).</summary>
    public async Task<(bool Ok, string Message)> KickAsync(int userId, string? reason, CancellationToken ct = default)
    {
        if (userId <= 0) return (false, "UserID không hợp lệ.");
        var text = string.IsNullOrWhiteSpace(reason) ? "Bạn đã bị GM mời ra khỏi game." : reason.Trim();
        if (text.Length > 200) text = text[..200];
        var (ok, xml, error) = await CallAsync("KitoffUser", new XElement(TempuriNs + "KitoffUser",
            Arg("playerID", userId),
            Arg("zoneId", _zoneId),
            Arg("msg", text)), ct);
        if (!ok) return (false, error);
        return ParseBoolResult(xml, "KitoffUser")
            ? (true, $"Đã gửi lệnh kick user {userId} khỏi zone {_zoneId}.")
            : (false, $"Không kick được user {userId}: người chơi không online ở zone {_zoneId} (CenterService không thấy kết nối).");
    }

    /// <summary>Trạng thái online theo CenterService. State &gt; 0 = online, -1 = offline, 0 = zone chưa kết nối, -2 = lỗi.</summary>
    public async Task<(bool Ok, int State, string Message)> GetPlayerStateAsync(int userId, CancellationToken ct = default)
    {
        if (userId <= 0) return (false, -2, "UserID không hợp lệ.");
        var (ok, xml, error) = await CallAsync("GetPlayerState", new XElement(TempuriNs + "GetPlayerState",
            Arg("playerID", userId),
            Arg("zoneId", _zoneId)), ct);
        if (!ok) return (false, -2, error);

        var state = ParseIntResult(xml, "GetPlayerState");
        var message = state switch
        {
            > 0 => "Online (CenterService thấy player trong zone)",
            -1 => "Offline (CenterService không thấy player)",
            0 => "Zone login chưa kết nối tới CenterService",
            _ => "Lỗi khi hỏi CenterService"
        };
        return (true, state, message);
    }

    /// <summary>Reload manager/game data trên tất cả Road (Reload).</summary>
    public async Task<(bool Ok, string Message)> ReloadAsync(string? type, CancellationToken ct = default)
    {
        var option = ReloadOptions.FirstOrDefault(o =>
            string.Equals(o.Type, (type ?? "").Trim(), StringComparison.OrdinalIgnoreCase));
        if (option.Type == null) return (false, "Loại reload không hợp lệ.");
        if (!HasLoginKey)
            return (false, "Thiếu LoginKey — cấu hình Panel:LoginKey hoặc Paths:CenterServiceConfig.");

        var payload = option.Type + "|" + Md5(option.Type + _loginKey);
        var (ok, xml, error) = await CallAsync("Reload",
            new XElement(TempuriNs + "Reload", Arg("type", payload)), ct);
        if (!ok) return (false, error);
        return ParseBoolResult(xml, "Reload")
            ? (true, "Đã gửi lệnh reload: " + option.Label)
            : (false, "CenterService từ chối reload (kiểm tra LoginKey/loại reload).");
    }

    /// <summary>Reload danh sách server (Center + Road đọc lại server list).</summary>
    public async Task<(bool Ok, string Message)> ReloadServerListAsync(CancellationToken ct = default)
    {
        var (ok, xml, error) = await CallAsync("ReLoadServerList", new XElement(TempuriNs + "ReLoadServerList"), ct);
        if (!ok) return (false, error);
        return ParseBoolResult(xml, "ReLoadServerList")
            ? (true, "Đã reload danh sách server.")
            : (false, "CenterService không reload được danh sách server.");
    }

    /// <summary>Shutdown Road theo zone — chỉ bật khi Panel:EnableShutdown=true (mặc định tắt).</summary>
    public async Task<(bool Ok, string Message)> ShutdownAsync(int zoneId, CancellationToken ct = default)
    {
        if (!ShutdownEnabled)
            return (false, "Chức năng Shutdown đang tắt (Panel:EnableShutdown=false).");
        if (!HasLoginKey)
            return (false, "Thiếu LoginKey — cấu hình Panel:LoginKey hoặc Paths:CenterServiceConfig.");

        var zone = zoneId.ToString(CultureInfo.InvariantCulture);
        var payload = zone + "|" + Md5(zone + _loginKey);
        var (ok, xml, error) = await CallAsync("Shutdown",
            new XElement(TempuriNs + "Shutdown", Arg("type", payload)), ct);
        if (!ok) return (false, error);
        return ParseBoolResult(xml, "Shutdown")
            ? (true, $"Đã gửi lệnh shutdown tới zone {zoneId}.")
            : (false, "CenterService từ chối shutdown.");
    }

    /// <summary>Chat gần đây (CenterService giữ tối đa ~100 dòng, gồm chat world/guild/loa do Road forward về).</summary>
    public async Task<(bool Ok, string Message, List<ChatEntry> Chats)> GetChatAsync(CancellationToken ct = default)
    {
        var chats = new List<ChatEntry>();
        var (ok, xml, error) = await CallAsync("GetAllChat", new XElement(TempuriNs + "GetAllChat"), ct);
        if (!ok) return (false, error, chats);

        var base64 = ResultValue(xml, "GetAllChat");
        if (string.IsNullOrWhiteSpace(base64)) return (true, "Bộ đệm chat đang trống.", chats);

        try
        {
            var json = Decompress(base64);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object) continue;
                    var nick = GetString(item, "NickName");
                    var msg = GetString(item, "Msg");
                    if (string.IsNullOrWhiteSpace(msg)) continue;
                    DateTime.TryParse(GetString(item, "ChatTime"), CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out var time);
                    chats.Add(new ChatEntry(time,
                        string.IsNullOrWhiteSpace(nick) ? "(hệ thống)" : nick,
                        msg, GetInt(item, "Type"), GetInt(item, "ZoneId")));
                }
            }
            chats = chats.OrderByDescending(c => c.Time).ToList();
            return (true, chats.Count == 0 ? "Bộ đệm chat đang trống." : $"Đã tải {chats.Count} dòng chat.", chats);
        }
        catch (Exception ex)
        {
            return (false, "Không giải mã được dữ liệu chat: " + ex.Message, chats);
        }
    }

    // ------------------------------------------------------------------ SOAP plumbing

    /// <summary>Tham số SOAP phải cùng namespace tempuri với operation, nếu không WCF sẽ bind default.</summary>
    private static XElement Arg(string name, object? value) => new(TempuriNs + name, value ?? "");

    private async Task<(bool Ok, string Payload, string Message)> CallAsync(string operation, XElement body, CancellationToken ct)
    {
        var envelope = new XElement(SoapNs + "Envelope",
            new XElement(SoapNs + "Body", body)).ToString(SaveOptions.DisableFormatting);

        using var request = new HttpRequestMessage(HttpMethod.Post, _centerUrl)
        {
            Content = new StringContent(envelope, Encoding.UTF8, "text/xml")
        };
        request.Headers.TryAddWithoutValidation("SOAPAction", $"\"http://tempuri.org/ICenterService/{operation}\"");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));
        try
        {
            using var response = await Http.SendAsync(request, cts.Token);
            var text = await response.Content.ReadAsStringAsync(cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                var fault = ExtractFault(text);
                return (false, text,
                    $"CenterService trả HTTP {(int)response.StatusCode}" + (fault.Length > 0 ? ": " + fault : "."));
            }
            return (true, text, "");
        }
        catch (OperationCanceledException)
        {
            return (false, "", $"Timeout {_timeoutSeconds}s khi gọi CenterService ({_centerUrl}).");
        }
        catch (Exception ex)
        {
            return (false, "", "Không kết nối được CenterService (" + _centerUrl + "): " + ex.GetBaseException().Message);
        }
    }

    private static string ExtractFault(string xml)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            var fault = doc.Descendants().LastOrDefault(e => e.Name.LocalName == "faultstring");
            return fault?.Value?.Trim() ?? "";
        }
        catch
        {
            return "";
        }
    }

    private static string? ResultValue(string xml, string operation)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            var element = doc.Descendants().LastOrDefault(e => e.Name.LocalName == operation + "Result");
            return element?.Value;
        }
        catch
        {
            return null;
        }
    }

    private static bool ParseBoolResult(string xml, string operation) =>
        string.Equals(ResultValue(xml, operation)?.Trim(), "true", StringComparison.OrdinalIgnoreCase);

    private static int ParseIntResult(string xml, string operation)
    {
        var value = ResultValue(xml, operation);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : -2;
    }

    private static string Md5(string value) =>
        Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static string Decompress(string base64)
    {
        var bytes = Convert.FromBase64String(base64);
        if (bytes.Length <= 4) throw new InvalidDataException("payload quá ngắn");
        using var input = new MemoryStream(bytes, 4, bytes.Length - 4);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        gzip.CopyTo(output);
        return Encoding.UTF8.GetString(output.ToArray());
    }

    private static string GetString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value)) return "";
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? "",
            JsonValueKind.Null or JsonValueKind.Undefined => "",
            _ => value.ToString()
        };
    }

    private static int GetInt(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt32(out var parsed) ? parsed : 0;

    /// <summary>LoginKey: ưu tiên Panel:LoginKey, sau đó đọc từ config Center/Road (chỉ đọc file).</summary>
    private static string ResolveLoginKey(IConfiguration cfg)
    {
        var configured = (cfg["Panel:LoginKey"] ?? "").Trim();
        if (configured.Length > 0) return configured;

        var candidates = new[]
        {
            cfg["Paths:CenterServiceConfig"],
            Path.Combine(cfg["Paths:GameRoot"] ?? "", "Service", "Service", "Center", "Center.Service.exe.config"),
            cfg["Paths:RoadServiceConfig"]
        };
        foreach (var path in candidates)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) continue;
            try
            {
                var doc = XDocument.Load(path);
                var add = doc.Descendants("add").FirstOrDefault(a =>
                    string.Equals(a.Attribute("key")?.Value, "LoginKey", StringComparison.OrdinalIgnoreCase));
                var value = add?.Attribute("value")?.Value;
                if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
            }
            catch
            {
                // file config không đọc được — thử nguồn khác
            }
        }
        return "";
    }
}
