namespace GunnySuite.Core;

/// <summary>HTTP client dung chung (tai XML/asset tu quest/resource server).</summary>
public sealed class HttpFetcher
{
    private readonly HttpClient _http;

    public HttpFetcher()
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = true };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) GunnyPanel/1.0");
    }

    /// <summary>Tra ve null neu loi/404.</summary>
    public async Task<byte[]?> TryGetAsync(string url, CancellationToken ct = default)
    {
        try
        {
            using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadAsByteArrayAsync(ct);
        }
        catch
        {
            return null;
        }
    }

    public async Task<(bool ok, string message)> CheckAsync(string url, CancellationToken ct = default)
    {
        try
        {
            using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            return (resp.IsSuccessStatusCode, $"{(int)resp.StatusCode} {resp.ReasonPhrase}");
        }
        catch (Exception ex)
        {
            return (false, ex.GetType().Name + ": " + ex.Message);
        }
    }
}
