using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace GunnyPanel.Auth;

public sealed class PanelUser
{
    public string UserName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Role { get; set; } = "Moderator";
    public string Salt { get; set; } = "";
    public string PasswordHash { get; set; } = "";
}

/// <summary>User store don gian doc tu appsettings.json (giai doan test).</summary>
public sealed class PanelUserStore
{
    private readonly List<PanelUser> _users = new();

    public PanelUserStore(IConfiguration cfg)
    {
        foreach (var c in cfg.GetSection("Panel:Users").GetChildren())
        {
            _users.Add(new PanelUser
            {
                UserName = c["UserName"] ?? "",
                DisplayName = c["DisplayName"] ?? c["UserName"] ?? "",
                Role = c["Role"] ?? "Moderator",
                Salt = c["Salt"] ?? "",
                PasswordHash = (c["PasswordHash"] ?? "").ToLowerInvariant()
            });
        }
    }

    public PanelUser? Find(string userName) =>
        _users.FirstOrDefault(u => string.Equals(u.UserName, userName, StringComparison.OrdinalIgnoreCase));

    public static bool Verify(PanelUser user, string password)
    {
        if (string.IsNullOrEmpty(user.Salt)) return false;
        var hash = Hash(user.Salt, password);
        var a = Encoding.UTF8.GetBytes(hash);
        var b = Encoding.UTF8.GetBytes(user.PasswordHash);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }

    public static string Hash(string salt, string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(salt + password));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

public static class PanelAuthExtensions
{
    public static async Task SignInPanelAsync(this HttpContext ctx, PanelUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.GivenName, user.DisplayName),
            new(ClaimTypes.Role, user.Role)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await ctx.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8) });
    }
}
