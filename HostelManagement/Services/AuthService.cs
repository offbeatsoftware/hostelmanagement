namespace HostelManagement.Services;

/// <summary>
/// Admin sign in. Temporary fixed credentials (admin / admin) as agreed with the client;
/// to be replaced by a changeable, hashed password before go-live.
/// </summary>
public static class AuthService
{
    private const string AdminUserName = "admin";
    private const string AdminPassword = "admin";

    /// <summary>The user name is not case sensitive; the password is.</summary>
    public static bool IsValidLogin(string? userName, string? password) =>
        string.Equals(userName?.Trim(), AdminUserName, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(password, AdminPassword, StringComparison.Ordinal);
}
