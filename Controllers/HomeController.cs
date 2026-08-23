using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UserManagement.Data;
using UserManagement.Models;

namespace UserManagement.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly AppDbContext _context;

    public HomeController(AppDbContext context)
    {
        _context = context;
    }

    // GET: / (Admin Dashboard / User Management Table)
    // Requirement #3: Data sorted by last login time
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var users = await _context.Users
            .OrderByDescending(u => u.LastLoginTime ?? u.RegistrationTime)
            .ToListAsync();

        return View(users);
    }

    public class BatchActionRequest
    {
        public List<int> UserIds { get; set; } = new();
        public string Action { get; set; } = string.Empty;
    }

    // POST: /Home/ExecuteAction
    // Handles Toolbar actions: Block, Unblock, Delete, DeleteUnverified
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExecuteAction([FromBody] BatchActionRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Action))
        {
            return Json(new { success = false, message = "Invalid request payload." });
        }

        var currentUserIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int.TryParse(currentUserIdStr, out int currentUserId);
        bool currentAffected = false;

        var action = request.Action.Trim().ToLower();

        if (action == "deleteunverified")
        {
            // Action: Delete all unverified users
            var unverifiedUsers = await _context.Users
                .Where(u => u.Status == "Unverified")
                .ToListAsync();

            if (unverifiedUsers.Count == 0)
            {
                return Json(new { success = true, message = "No unverified users found to delete." });
            }

            if (unverifiedUsers.Any(u => u.Id == currentUserId))
            {
                currentAffected = true;
            }

            _context.Users.RemoveRange(unverifiedUsers);
            await _context.SaveChangesAsync();

            return Json(new 
            { 
                success = true, 
                message = $"{unverifiedUsers.Count} unverified user(s) permanently deleted.",
                currentAffected
            });
        }

        // For Block, Unblock, Delete on selected user IDs
        if (request.UserIds == null || request.UserIds.Count == 0)
        {
            return Json(new { success = false, message = "Please select at least one user." });
        }

        var selectedUsers = await _context.Users
            .Where(u => request.UserIds.Contains(u.Id))
            .ToListAsync();

        if (selectedUsers.Count == 0)
        {
            return Json(new { success = false, message = "No matching users found." });
        }

        if (selectedUsers.Any(u => u.Id == currentUserId))
        {
            currentAffected = true;
        }

        switch (action)
        {
            case "block":
                foreach (var user in selectedUsers)
                {
                    user.Status = "Blocked";
                }
                await _context.SaveChangesAsync();
                return Json(new 
                { 
                    success = true, 
                    message = $"{selectedUsers.Count} user(s) blocked successfully.",
                    currentAffected = (action == "block" && selectedUsers.Any(u => u.Id == currentUserId))
                });

            case "unblock":
                foreach (var user in selectedUsers)
                {
                    if (user.Status == "Blocked")
                    {
                        // Critical instructor requirement: restore exact previous status
                        // If email was verified -> restore to "Active", if not verified -> restore to "Unverified"
                        user.Status = user.IsEmailVerified ? "Active" : "Unverified";
                    }
                }
                await _context.SaveChangesAsync();
                return Json(new 
                { 
                    success = true, 
                    message = $"{selectedUsers.Count} user(s) unblocked successfully (status properly restored).",
                    currentAffected = false
                });

            case "delete":
                // Task requirement: "Deleted users should be deleted, not marked"
                _context.Users.RemoveRange(selectedUsers);
                await _context.SaveChangesAsync();
                return Json(new 
                { 
                    success = true, 
                    message = $"{selectedUsers.Count} user(s) permanently deleted.",
                    currentAffected
                });

            default:
                return Json(new { success = false, message = "Unknown action specified." });
        }
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
