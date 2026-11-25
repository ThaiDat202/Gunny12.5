using GunnyPanel.Auth;
using GunnyPanel.Jobs;
using GunnyPanel.Services;
using GunnySuite.Core;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.RazorPages;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

builder.WebHost.UseUrls(cfg["Panel:Urls"] ?? "http://127.0.0.1:90");

builder.Services.AddRazorPages()
    .AddRazorPagesOptions(o =>
    {
        o.Conventions.AuthorizeFolder("/");
        o.Conventions.AllowAnonymousToPage("/Login");
    });
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Login";
        o.AccessDeniedPath = "/Login";
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
        o.Cookie.Name = "GunnyPanel.Auth";

        // Endpoint JSON (/api/...): trả 401/403 thay vì redirect HTML về trang Login.
        o.Events.OnRedirectToLogin = ctx =>
        {
            if (IsApiRequest(ctx.Request))
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }
            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        };
        o.Events.OnRedirectToAccessDenied = ctx =>
        {
            if (IsApiRequest(ctx.Request))
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }
            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();

var dataDir = cfg["Paths:DataDir"] ?? Path.Combine(AppContext.BaseDirectory, "data");
var logDir = cfg["Paths:LogDir"] ?? Path.Combine(dataDir, "logs");
Directory.CreateDirectory(dataDir);

builder.Services.AddSingleton(new AuditLog(logDir));
builder.Services.AddSingleton<HttpFetcher>();
builder.Services.AddSingleton(new SqlDatabase(BuildConnections(cfg)));
builder.Services.AddSingleton<RoadLiveService>();
builder.Services.AddSingleton<RoadGmApiClient>();
builder.Services.AddSingleton<PanelUserStore>();
builder.Services.AddSingleton<JobStore>();
builder.Services.AddSingleton<JobQueue>();
builder.Services.AddHostedService<JobWorker>();
builder.Services.AddHostedService<SchedulerService>();
builder.Services.AddScoped<IJobHandler, SyncXmlJobHandler>();
builder.Services.AddScoped<IJobHandler, UpdateUrlTankJobHandler>();
builder.Services.AddScoped<IJobHandler, CheckConnectionsJobHandler>();
builder.Services.AddScoped<IJobHandler, RequestSqlJobHandler>();
builder.Services.AddScoped<IJobHandler, ResourceSyncJobHandler>();
builder.Services.AddScoped<IJobHandler, FlashRipJobHandler>();
builder.Services.AddScoped<IJobHandler, BombConvertJobHandler>();

var app = builder.Build();

app.UseStaticFiles();

// Không cache HTML/động: tránh trình duyệt cũ (37abc…) giữ trang/JS cũ sau khi panel cập nhật.
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
    ctx.Response.Headers["Pragma"] = "no-cache";
    ctx.Response.Headers["Expires"] = "0";
    await next();
});

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.MapGet("/api/health", () => Results.Json(new { ok = true, time = DateTime.Now, app = "GunnyPanel" }));

app.MapGet("/api/jobs", (JobStore store) => Results.Json(store.List(100)))
   .RequireAuthorization();

app.MapGet("/api/jobs/{id:guid}", (Guid id, JobStore store) =>
{
    var job = store.Get(id);
    return job == null ? Results.NotFound() : Results.Json(job);
}).RequireAuthorization();

app.Run();

static Dictionary<string, string> BuildConnections(IConfiguration cfg)
{
    var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var child in cfg.GetSection("ConnectionStrings").GetChildren())
        if (!string.IsNullOrWhiteSpace(child.Value)) dict[child.Key] = child.Value!;
    return dict;
}

static bool IsApiRequest(HttpRequest request)
{
    if (request.Path.StartsWithSegments("/api")) return true;
    return request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase);
}
