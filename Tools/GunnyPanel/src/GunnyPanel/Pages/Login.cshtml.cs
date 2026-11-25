using GunnyPanel.Auth;
using GunnySuite.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GunnyPanel.Pages;

public sealed class LoginModel : PageModel
{
    private readonly PanelUserStore _users;
    private readonly AuditLog _audit;

    public LoginModel(PanelUserStore users, AuditLog audit)
    {
        _users = users;
        _audit = audit;
    }

    [BindProperty] public string UserName { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";
    public string? Error { get; set; }

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToPage("/Index");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = _users.Find(UserName ?? "");
        if (user == null || !PanelUserStore.Verify(user, Password ?? ""))
        {
            Error = "Sai tài khoản hoặc mật khẩu.";
            _audit.Write(UserName ?? "", "login.failed", HttpContext.Connection.RemoteIpAddress?.ToString() ?? "");
            return Page();
        }

        await HttpContext.SignInPanelAsync(user);
        _audit.Write(user.UserName, "login.ok", HttpContext.Connection.RemoteIpAddress?.ToString() ?? "");
        return RedirectToPage("/Index");
    }
}
