namespace HostelManagement.Models;

/// <summary>The administrator who signs in. The password is only stored as a hash.</summary>
public sealed class AdminUser
{
    /// <summary>The first sign in after a new database: admin / admin, to be changed on the Admin Account screen.</summary>
    public const string DefaultUserName = "admin";
    public const string DefaultPassword = "admin";

    public int AdminUserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
}
