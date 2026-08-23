using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using UserManagement.Data;

namespace UserManagement.Middleware;

public class UserStatusMiddleware
{
    private readonly RequestDelegate _next;

    public UserStatusMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Requirement #5: Before each request except for registration or login,
    /// server should check if user exists and isn't blocked.
    /// If user account is blocked or deleted, any next request redirects to login page with clear explanation.
    /// </summary>
    public async Task InvokeAsync(HttpContext context, AppDbContext dbContext)
    {
        var path = context.Request.Path.Value?.ToLower() ?? string.Empty;

        // Skip check for login, registration, email verification, and static assets
        if (path == "/login" || path.StartsWith("/login") ||
            path == "/register" || path.StartsWith("/register") ||
            path == "/verify-email" || path.StartsWith("/verify-email") ||
            path == "/logout" || path.StartsWith("/logout") ||
            path.StartsWith("/css") ||
            path.StartsWith("/js") ||
            path.StartsWith("/lib") ||
            path.StartsWith("/favicon.ico"))
        {
            await _next(context);
            return;
        }

        // If user is authenticated, verify their current state against the live database
        if (context.User.Identity != null && context.User.Identity.IsAuthenticated)
        {
            var userIdStr = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdStr, out int userId))
            {
                var user = await dbContext.Users.FindAsync(userId);

                // If user is deleted or blocked, force sign out and redirect to login with reason
                if (user == null || user.Status == "Blocked")
                {
                    var reason = (user == null) ? "deleted" : "blocked";
                    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

                    // If AJAX request, return 401 Unauthorized so client-side redirect happens
                    if (context.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return;
                    }

                    context.Response.Redirect($"/login?reason={reason}");
                    return;
                }
            }
        }

        await _next(context);
    }
}
