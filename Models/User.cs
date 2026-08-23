namespace UserManagement.Models;

public class User
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    // Status can be: "Unverified", "Active", "Blocked"
    public string Status { get; set; } = "Unverified";

    // Tracks email verification independently so unblocking restores the exact previous state (Active vs Unverified)
    public bool IsEmailVerified { get; set; } = false;

    public DateTime RegistrationTime { get; set; } = DateTime.UtcNow;
    
    public DateTime? LastLoginTime { get; set; }
}