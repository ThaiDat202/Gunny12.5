using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using Game.Server.Managers;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Game.Service.GMService;

/// <summary>
/// Road GM API — HTTP JSON nội bộ (chỉ bind 127.0.0.1), host ngay trong process Road.Service
/// để cập nhật người chơi đang online in-memory + gửi packet cho client (không cần relogin).
///
/// Cấu hình trong Road.Service.exe.config:
///   &lt;add key="GMApi.Enabled" value="true" /&gt;
///   &lt;add key="GMApi.Prefix" value="http://127.0.0.1:9601/" /&gt;
///   &lt;add key="GMApi.Token" value="..." /&gt;
///
/// Gọi từ GunnyPanel (phía server, không lộ token ra browser):
///   POST /gm/apply  {"userId":1,"feature":"king"}   header X-GM-Token
///   GET  /player?userId=1
///   GET  /health
///   GET  /features
/// </summary>
public static class GmApiServer
{
    private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
    private static readonly object SyncRoot = new object();

    private static HttpListener _listener;
    private static Thread _thread;
    private static string _token = "";
    private static bool _running;

    public static bool IsRunning
    {
        get { lock (SyncRoot) { return _running; } }
    }

    public static string Prefix { get; private set; } = "";

    public static string ZoneId
    {
        get { return ConfigurationManager.AppSettings["AreaID"] ?? ""; }
    }

    /// <summary>Khởi động API nếu GMApi.Enabled=true. Trả về true nếu đang chạy.</summary>
    public static bool TryStart()
    {
        lock (SyncRoot)
        {
            if (_running) return true;

            var enabledText = ConfigurationManager.AppSettings["GMApi.Enabled"];
            var enabled = string.Equals(enabledText, "true", StringComparison.OrdinalIgnoreCase);
            if (!enabled)
            {
                log.Info("[GMApi] disabled (GMApi.Enabled != true)");
                Console.WriteLine("[GMApi] disabled (GMApi.Enabled != true)");
                return false;
            }

            var prefix = ConfigurationManager.AppSettings["GMApi.Prefix"];
            if (string.IsNullOrWhiteSpace(prefix)) prefix = "http://127.0.0.1:9601/";
            if (!prefix.EndsWith("/")) prefix += "/";

            _token = (ConfigurationManager.AppSettings["GMApi.Token"] ?? "").Trim();
            if (_token.Length < 8)
            {
                log.Error("[GMApi] thiếu GMApi.Token (>=8 ký tự) — không khởi động API");
                Console.WriteLine("[GMApi] missing/invalid GMApi.Token — not started");
                return false;
            }

            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add(prefix);
                _listener.Start();

                Prefix = prefix;
                _running = true;
                _thread = new Thread(ListenLoop)
                {
                    IsBackground = true,
                    Name = "GMApi"
                };
                _thread.Start();

                var message = "[GMApi] listening at " + prefix + " (zone " + ZoneId + ")";
                log.Info(message);
                Console.WriteLine(message);
                return true;
            }
            catch (Exception ex)
            {
                log.Error("[GMApi] start failed: " + ex.Message, ex);
                Console.WriteLine("[GMApi] start failed: " + ex.Message);
                SafeStop();
                return false;
            }
        }
    }

    public static void Stop()
    {
        lock (SyncRoot)
        {
            if (!_running) return;
            _running = false;
            try { _listener?.Stop(); } catch { }
            try { _listener?.Close(); } catch { }
            _listener = null;
            log.Info("[GMApi] stopped");
            Console.WriteLine("[GMApi] stopped");
        }
    }

    private static void SafeStop()
    {
        _running = false;
        try { _listener?.Close(); } catch { }
        _listener = null;
    }

    private static void ListenLoop()
    {
        while (IsRunning)
        {
            HttpListenerContext context;
            try
            {
                context = _listener.GetContext();
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                log.Warn("[GMApi] accept error: " + ex.Message);
                Thread.Sleep(100);
                continue;
            }

            ThreadPool.QueueUserWorkItem(_ => Handle(context));
        }
    }

    private static void Handle(HttpListenerContext context)
    {
        var request = context.Request;
        try
        {
            if (!IsAuthorized(request))
            {
                WriteJson(context, 401, new { ok = false, message = "Unauthorized (sai hoặc thiếu X-GM-Token)." });
                return;
            }

            var path = (request.Url.AbsolutePath ?? "/").ToLowerInvariant().TrimEnd('/');
            if (path.Length == 0) path = "/";

            if (path == "/health" || path == "/")
            {
                WriteHealth(context);
                return;
            }
            if (path == "/features")
            {
                WriteJson(context, 200, new { ok = true, features = GmApiActions.SupportedFeatures });
                return;
            }
            if (path == "/player" && request.HttpMethod == "GET")
            {
                HandlePlayer(context);
                return;
            }
            if (path == "/gm/apply" && request.HttpMethod == "POST")
            {
                HandleApply(context);
                return;
            }

            WriteJson(context, 404, new { ok = false, message = "Not found: " + path });
        }
        catch (Exception ex)
        {
            log.Error("[GMApi] handler error: " + ex.Message, ex);
            try { WriteJson(context, 500, new { ok = false, message = "Lỗi API: " + ex.Message }); } catch { }
        }
        finally
        {
            try { context.Response.Close(); } catch { }
        }
    }

    private static bool IsAuthorized(HttpListenerRequest request)
    {
        var provided = request.Headers["X-GM-Token"] ?? "";
        if (provided.Length == 0 || _token.Length == 0) return false;
        return FixedTimeEquals(provided, _token);
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        if (a == null || b == null) return false;
        var ba = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        var diff = ba.Length ^ bb.Length;
        var max = Math.Max(ba.Length, bb.Length);
        for (var i = 0; i < max; i++)
        {
            var x = i < ba.Length ? ba[i] : (byte)0;
            var y = i < bb.Length ? bb[i] : (byte)0;
            diff |= x ^ y;
        }
        return diff == 0;
    }

    private static void WriteHealth(HttpListenerContext context)
    {
        var players = 0;
        try
        {
            var all = WorldMgr.GetAllPlayers();
            if (all != null) players = all.Length;
        }
        catch
        {
            // ignore
        }

        WriteJson(context, 200, new
        {
            ok = true,
            api = "RoadGMApi",
            version = "1.0",
            zone = ZoneId,
            onlinePlayers = players,
            time = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
        });
    }

    private static void HandlePlayer(HttpListenerContext context)
    {
        var query = ParseQuery(context.Request.Url.Query);
        var userId = ToInt(Get(query, "userId", "playerId"));
        if (userId <= 0)
        {
            WriteJson(context, 400, new { ok = false, message = "Thiếu userId hợp lệ." });
            return;
        }

        var player = GmApiActions.FindOnlinePlayer(userId);
        if (player == null || player.PlayerCharacter == null)
        {
            WriteJson(context, 200, new { ok = true, online = false, userId, message = "Người chơi không online." });
            return;
        }

        WriteJson(context, 200, new
        {
            ok = true,
            online = true,
            userId,
            player = GmApiActions.Snapshot(player)
        });
    }

    private static void HandleApply(HttpListenerContext context)
    {
        var body = ReadBody(context.Request, 64 * 1024);
        if (body.Length == 0)
        {
            WriteJson(context, 400, new { ok = false, message = "Body JSON trống." });
            return;
        }

        JObject payload;
        try
        {
            payload = JObject.Parse(body);
        }
        catch (Exception ex)
        {
            WriteJson(context, 400, new { ok = false, message = "JSON không hợp lệ: " + ex.Message });
            return;
        }

        var userId = ToInt(payload.Value<string>("userId") ?? payload.Value<string>("playerId"));
        var feature = (payload.Value<string>("feature") ?? "").Trim();

        if (userId <= 0 || feature.Length == 0)
        {
            WriteJson(context, 400, new { ok = false, message = "Cần userId và feature." });
            return;
        }

        var result = GmApiActions.Apply(userId, feature);
        var status = result.Ok ? 200 : (result.Online ? 400 : 404);
        WriteJson(context, status, new
        {
            ok = result.Ok,
            online = result.Online,
            userId = result.UserId,
            feature = result.Feature,
            message = result.Message,
            player = result.Snapshot
        });

        log.Info("[GMApi] apply feature=" + feature + " user=" + userId +
                 " online=" + result.Online + " ok=" + result.Ok + " msg=" + result.Message);
    }

    // ------------------------------------------------------------------ helpers

    private static void WriteJson(HttpListenerContext context, int status, object payload)
    {
        var json = JsonConvert.SerializeObject(payload, Formatting.None);
        var bytes = Encoding.UTF8.GetBytes(json);
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.ContentEncoding = Encoding.UTF8;
        context.Response.ContentLength64 = bytes.Length;
        context.Response.OutputStream.Write(bytes, 0, bytes.Length);
        context.Response.OutputStream.Flush();
    }

    private static string ReadBody(HttpListenerRequest request, int maxBytes)
    {
        if (!request.HasEntityBody) return "";
        using (var stream = request.InputStream)
        using (var reader = new StreamReader(stream, request.ContentEncoding ?? Encoding.UTF8))
        {
            var buffer = new char[Math.Min(maxBytes, 8192)];
            var builder = new StringBuilder();
            int read;
            while ((read = reader.Read(buffer, 0, buffer.Length)) > 0 && builder.Length < maxBytes)
            {
                builder.Append(buffer, 0, read);
            }
            return builder.ToString().Trim();
        }
    }

    private static Dictionary<string, string> ParseQuery(string raw)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(raw)) return result;
        var text = raw.StartsWith("?") ? raw.Substring(1) : raw;
        foreach (var pair in text.Split('&'))
        {
            if (pair.Length == 0) continue;
            var idx = pair.IndexOf('=');
            try
            {
                if (idx < 0)
                {
                    result[Uri.UnescapeDataString(pair)] = "";
                }
                else
                {
                    var key = Uri.UnescapeDataString(pair.Substring(0, idx));
                    var value = Uri.UnescapeDataString(pair.Substring(idx + 1).Replace("+", "%20"));
                    result[key] = value;
                }
            }
            catch
            {
                // ignore malformed pair
            }
        }
        return result;
    }

    private static string Get(Dictionary<string, string> query, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (query.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)) return value;
        }
        return "";
    }

    private static int ToInt(string text)
    {
        return int.TryParse((text ?? "").Trim(), out var value) ? value : 0;
    }
}
