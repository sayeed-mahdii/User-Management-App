using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserManagement.Data;
using UserManagement.Models;
using UserManagement.Services;

namespace UserManagement.Controllers;

public class AccountController : Controller
{
    private readonly AppDbContext _context;
    private readonly IEmailService _emailService;

    // Dependency Injection: ASP.NET injects AppDbContext and IEmailService
    public AccountController(AppDbContext context, IEmailService emailService)
    {
        _context = context;
        _emailService = emailService;
    }

    // GET: /register
    [HttpGet]
    [Route("register")]
    public IActionResult Register()
    {
        if (User.Identity != null && User.Identity.IsAuthenticated)
        {
            return Redirect("/");
        }
        return View();
    }

    // POST: /register
    [HttpPost]
    [Route("register")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = new User
        {
            Name = model.Name.Trim(),
            Email = model.Email.Trim().ToLower(),
            Status = "Unverified", // Initial status as required
            IsEmailVerified = false,
            RegistrationTime = DateTime.UtcNow,
            LastLoginTime = null
        };

        // Hash password securely (supports any non-empty password)
        var hasher = new PasswordHasher<User>();
        user.PasswordHash = hasher.HashPassword(user, model.Password);

        try
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Enforced by the Unique Index in Database
            ModelState.AddModelError("Email", "An account with this email address already exists.");
            return View(model);
        }

        // Build email confirmation link & send email asynchronously
        var confirmationLink = $"{Request.Scheme}://{Request.Host}/verify-email?id={user.Id}";
        _ = _emailService.SendConfirmationEmailAsync(user.Email, confirmationLink);

        TempData["SuccessMessage"] = "Registration successful! A confirmation email has been sent. You can now sign in.";
        return Redirect("/login");
    }

    // GET: /login
    [HttpGet]
    [Route("login")]
    public IActionResult Login([FromQuery] string? reason, [FromQuery] string? returnUrl)
    {
        if (User.Identity != null && User.Identity.IsAuthenticated)
        {
            return Redirect("/");
        }

        // Provide clear explanation for unexpected redirects (Instructor Requirement)
        if (reason == "blocked")
        {
            ViewData["ErrorMessage"] = "Your session has ended because your account was blocked by an administrator.";
        }
        else if (reason == "deleted")
        {
            ViewData["ErrorMessage"] = "Your session has ended because your account was deleted.";
        }
        else if (!string.IsNullOrEmpty(returnUrl))
        {
            ViewData["InfoMessage"] = "Please sign in to access the user management panel.";
        }

        return View();
    }

    // POST: /login
    [HttpPost]
    [Route("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // 1. Find user by email
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email.Trim().ToLower());
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        // 2. Check if user is Blocked (Task requirement: Blocked user cannot login)
        if (user.Status == "Blocked")
        {
            ModelState.AddModelError(string.Empty, "This account is blocked and cannot sign in.");
            return View(model);
        }

        // 3. Verify password
        var hasher = new PasswordHasher<User>();
        var verificationResult = hasher.VerifyHashedPassword(user, user.PasswordHash, model.Password);

        if (verificationResult == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        // 4. Update Last Login Time (only upon actual successful login)
        user.LastLoginTime = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // 5. Issue Authentication Cookie
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Email, user.Email)
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = model.RememberMe,
            ExpiresUtc = model.RememberMe ? DateTimeOffset.UtcNow.AddDays(14) : null
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);

        return Redirect("/");
    }

    // POST: /logout
    [HttpPost]
    [Route("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect("/login");
    }

    // GET: /verify-email?id=5
    // Note: Clicking the link changes status from "unverified" to "active" ("blocked" stays "blocked")
    [HttpGet]
    [Route("verify-email")]
    public async Task<IActionResult> VerifyEmail(int id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user != null)
        {
            user.IsEmailVerified = true;

            if (user.Status == "Unverified")
            {
                user.Status = "Active";
                TempData["SuccessMessage"] = "Your email has been verified! Your account is now Active.";
            }
            else if (user.Status == "Blocked")
            {
                // Nota bene: "blocked" stays "blocked"
                TempData["ErrorMessage"] = "Your email was verified, but your account is currently blocked.";
            }
            else
            {
                TempData["InfoMessage"] = "Your account is already active.";
            }

            await _context.SaveChangesAsync();
        }
        else
        {
            TempData["ErrorMessage"] = "Verification link is invalid or the user account was deleted.";
        }

        return Redirect("/login");
    }
}
