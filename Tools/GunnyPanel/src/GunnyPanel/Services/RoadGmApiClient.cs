using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace GunnyPanel.Services;

/// <summary>
/// Client gọi <b>Road GM API</b> — HTTP JSON nội bộ trong process Road.Service
/// (mặc định <c>http://127.0.0.1:9601/</c>), dùng để áp thay đổi GM cho người chơi
/// <b>đang online</b> ngay lập tức (in-memory + packet), không cần đăng nhập lại.
///
/// Token cấu hình ở <c>Panel:RoadGmApiToken</c> — phải trùng <c>GMApi.Token</c> trong
/// <c>Road.Service.exe.config</c>. Token chỉ nằm phía server panel, không lộ ra browser.
/// </summary>
public sealed class RoadGmApiClient
{
    /// <summary>Các feature GmTool được áp live ngay khi user online (in-memory + packet).</summary>
    public static readonly string[] LiveFeatures =
    {
        "king", "guard", "honor", "sworn", "sworn_love", "explorer", "titles",
        "amulet_level", "amulet_wash", "potential", "phuma", "duchon",
        "avatar", "album", "battle_soul"
    };

    /// <summary>
    /// Feature port từ Form1 desktop (v0.29) — API xử lý cả online (in-memory + packet) lẫn offline (SQL).
    /// </summary>
    public static readonly string[] ExtraApiFeatures =
    {
        "training_basic", "training_second", "training_element",
        "card_level", "card_break",
        "totem_level", "totem_rank",
        "temple_bless", "temple_exalt",
        "gold_weapon", "gold_hat", "gold_cloth",
        "craft",
        "star_weapon", "star_hat", "star_cloth",
        "subweapon_evolve",
        "necklace_train", "necklace_cast",
        "pet_rebirth", "pet_evolve", "pet_strengthen", "pet_breakthrough",
        "mount_level", "mount_skill", "mount_avatar", "mount_amulet",
        "elf_nurture", "elf_train", "elf_linhbao", "elf_learn",
        "mark_train", "mark_refine"
    };

    /// <summary>
    /// Feature do Road GM API xử lý toàn bộ (cả offline): mở 6 mũi khoan + gửi mail châu báu;
    /// gửi/nâng Ma Thạch đúng 6 chỉ số; các feature port Form1 (tu luyện/thẻ bài/vật tổ/...).
    /// </summary>
    public static readonly string[] ApiManagedFeatures =
        new[] { "beads", "magicstone" }.Concat(ExtraApiFeatures).ToArray();

    public static readonly string[] SupportedFeatures = LiveFeatures.Concat(ApiManagedFeatures).ToArray();

    public static bool IsLiveFeature(string feature) =>
        feature != null && LiveFeatures.Contains(feature, StringComparer.OrdinalIgnoreCase);

    public static bool IsApiManagedFeature(string feature) =>
        feature != null && ApiManagedFeatures.Contains(feature, StringComparer.OrdinalIgnoreCase);

    public static bool IsSupportedFeature(string feature) =>
        feature != null && SupportedFeatures.Contains(feature, StringComparer.OrdinalIgnoreCase);

    private static readonly HttpClient Http = new() { Timeout = Timeout.InfiniteTimeSpan };

    private readonly string _baseUrl;
    private readonly string _token;
    private readonly int _timeoutSeconds;

    public RoadGmApiClient(IConfiguration cfg)
    {
        _baseUrl = (cfg["Panel:RoadGmApiUrl"] ?? "http://127.0.0.1:9601/").Trim();
        if (_baseUrl.Length > 0 && !_baseUrl.EndsWith('/')) _baseUrl += "/";
        _token = (cfg["Panel:RoadGmApiToken"] ?? "").Trim();
        _timeoutSeconds = int.TryParse(cfg["Panel:RoadGmApiTimeoutSeconds"], out var timeout)
            ? Math.Clamp(timeout, 2, 30)
            : 5;
    }

    public bool Enabled => _baseUrl.Length > 0 && _token.Length > 0;
    public string BaseUrl => _baseUrl;

    /// <summary>Kiểm tra API có sống không (GET /health).</summary>
    public async Task<(bool Ok, string Message)> HealthAsync(CancellationToken ct = default)
    {
        if (!Enabled) return (false, "Road GM API chưa cấu hình (Panel:RoadGmApiToken).");
        var call = await CallAsync(HttpMethod.Get, "health", null, ct);
        if (!call.Ok) return (false, call.Error);
        try
        {
            using var doc = JsonDocument.Parse(call.Json);
            var root = doc.RootElement;
            var online = GetInt(root, "onlinePlayers");
            var zone = GetString(root, "zone");
            return (true, $"Road GM API OK (zone {zone}, {online} online).");
        }
        catch
        {
            return (true, "Road GM API phản hồi.");
        }
    }

    /// <summary>
    /// Áp 1 feature GM qua Road.
    /// - feature "live": cần user online; Online=false nếu Road không thấy player.
    /// - feature "api-managed" (beads/magicstone): chạy cả khi offline.
    /// Reachable=false khi không kết nối được API (để panel fallback DB path).
    /// </summary>
    public async Task<(bool Ok, bool Online, string Message, bool Reachable)> ApplyAsync(int userId, string feature, CancellationToken ct = default)
    {
        if (!Enabled) return (false, false, "Road GM API chưa cấu hình (Panel:RoadGmApiToken).", false);
        if (!IsSupportedFeature(feature)) return (false, false, "Feature không hỗ trợ qua Road GM API: " + feature, true);

        var body = "{\"userId\":" + userId + ",\"feature\":\"" + JsonEscape(feature) + "\"}";
        var call = await CallAsync(HttpMethod.Post, "gm/apply", body, ct);
        if (call.Json.Length > 0)
        {
            try
            {
                using var doc = JsonDocument.Parse(call.Json);
                var root = doc.RootElement;
                var ok = GetBool(root, "ok");
                var online = GetBool(root, "online");
                var message = GetString(root, "message");
                if (!call.Ok && message.Length == 0) message = call.Error;
                if (message.Length == 0) message = ok ? "Đã áp dụng." : "Không áp dụng được.";
                return (call.Ok && ok, online, message, true);
            }
            catch
            {
                // body không parse được — rơi xuống dùng lỗi call
            }
        }
        return (false, false, call.Ok ? "Road GM API trả dữ liệu không hợp lệ." : call.Error, call.Reachable);
    }

    /// <summary>Snapshot trạng thái người chơi trong bộ nhớ Road (dùng để kiểm tra/chẩn đoán).</summary>
    public async Task<(bool Ok, bool Online, string Message, string Json)> GetPlayerAsync(int userId, CancellationToken ct = default)
    {
        if (!Enabled) return (false, false, "Road GM API chưa cấu hình.", "");
        var call = await CallAsync(HttpMethod.Get, "player?userId=" + userId, null, ct);
        if (!call.Ok) return (false, false, call.Error, call.Json);
        try
        {
            using var doc = JsonDocument.Parse(call.Json);
            var root = doc.RootElement;
            var ok = GetBool(root, "ok");
            var online = GetBool(root, "online");
            var message = GetString(root, "message");
            return (ok, online, message, call.Json);
        }
        catch
        {
            return (false, false, "Road GM API trả dữ liệu không hợp lệ.", call.Json);
        }
    }

    // ------------------------------------------------------------------ plumbing

    private async Task<(bool Ok, bool Reachable, string Json, string Error)> CallAsync(HttpMethod method, string path, string? body, CancellationToken ct)
    {
        var url = _baseUrl + path;
        using var request = new HttpRequestMessage(method, url);
        request.Headers.TryAddWithoutValidation("X-GM-Token", _token);
        if (body != null)
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));
        try
        {
            using var response = await Http.SendAsync(request, cts.Token);
            var text = await response.Content.ReadAsStringAsync(cts.Token);
            if (!response.IsSuccessStatusCode)
            {
                var message = ExtractMessage(text);
                if (message.Length == 0) message = $"Road GM API trả HTTP {(int)response.StatusCode}.";
                return (false, true, text, message);
            }
            return (true, true, text, "");
        }
        catch (OperationCanceledException)
        {
            return (false, false, "", $"Timeout {_timeoutSeconds}s khi gọi Road GM API ({url}).");
        }
        catch (Exception ex)
        {
            return (false, false, "", "Không kết nối được Road GM API (" + _baseUrl + "): " + ex.GetBaseException().Message);
        }
    }

    private static string ExtractMessage(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return "";
        try
        {
            using var doc = JsonDocument.Parse(json);
            return GetString(doc.RootElement, "message");
        }
        catch
        {
            return "";
        }
    }

    private static string GetString(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value)) return "";
        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : value.ToString();
    }

    private static int GetInt(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value)) return 0;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
        return int.TryParse(value.ToString(), out var parsed) ? parsed : 0;
    }

    private static bool GetBool(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value)) return false;
        return value.ValueKind == JsonValueKind.True ||
               (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var parsed) && parsed);
    }

    private static string JsonEscape(string value) => (value ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
}
