using HostelManagement.Data;
using HostelManagement.Models;
using HostelManagement.Utilities;

namespace HostelManagement.Services;

/// <summary>
/// Admin sign in and account. The login is kept in the AdminUser table with the password stored as a
/// salted hash; a new database starts with admin / admin, which the admin changes on the Admin Account
/// screen. The admin's email receives a copy of every email sent to parents.
/// </summary>
public static class AuthService
{
    public const int MinimumPasswordLength = 6;

    /// <summary>The admin who signed in (the default admin until someone signs in, for example in tests).</summary>
    public static string CurrentUserName { get; private set; } = AdminUser.DefaultUserName;

    /// <summary>The user name is not case sensitive; the password is.</summary>
    public static bool IsValidLogin(string? userName, string? password)
    {
        string name = userName?.Trim() ?? string.Empty;
        if (name.Length == 0 || string.IsNullOrEmpty(password))
        {
            return false;
        }

        string? hash = AdminUserRepository.GetPasswordHash(name);
        if (hash is null || !PasswordHasher.Verify(password, hash))
        {
            AppLogger.Info($"Failed sign in for user name '{name}'.");
            return false;
        }

        CurrentUserName = AdminUserRepository.Get(name)!.UserName;
        return true;
    }

    public static AdminUser GetCurrentAdmin() =>
        AdminUserRepository.Get(CurrentUserName) ?? AdminUserRepository.GetFirst()
        ?? throw new ValidationException("No admin login exists. Restart the application.");

    /// <summary>The admin's email address for the copy of every email, or empty when not set.</summary>
    public static string GetAdminEmail() => AdminUserRepository.GetFirst()?.Email ?? string.Empty;

    public static void ChangePassword(string currentPassword, string newPassword, string confirmPassword)
    {
        AdminUser admin = GetCurrentAdmin();
        string? hash = AdminUserRepository.GetPasswordHash(admin.UserName);
        if (hash is null || !PasswordHasher.Verify(currentPassword, hash))
        {
            throw new ValidationException("The current password is not correct.");
        }
        if (newPassword.Length < MinimumPasswordLength)
        {
            throw new ValidationException($"The new password must have at least {MinimumPasswordLength} characters.");
        }
        if (newPassword.Trim().Length != newPassword.Length)
        {
            throw new ValidationException("The new password cannot start or end with a space.");
        }
        if (newPassword != confirmPassword)
        {
            throw new ValidationException("The new password and its confirmation are not the same.");
        }
        if (newPassword == currentPassword)
        {
            throw new ValidationException("The new password must be different from the current password.");
        }

        AdminUserRepository.UpdatePasswordHash(admin.AdminUserId, PasswordHasher.Hash(newPassword));
        AppLogger.Info($"Password changed for {admin.UserName}.");
    }

    public static AdminUser SaveContact(string email, string phone)
    {
        email = Validators.Clean(email);
        phone = Validators.Clean(phone);
        if (!Validators.IsValidEmailOrEmpty(email))
        {
            throw new ValidationException("Please enter a valid email address.");
        }
        if (!Validators.IsValidPhoneOrEmpty(phone))
        {
            throw new ValidationException("Please enter a valid phone number.");
        }
        Validators.CheckLength(email, 150, "Email");
        Validators.CheckLength(phone, 20, "Phone");

        AdminUser admin = GetCurrentAdmin();
        AdminUserRepository.UpdateContact(admin.AdminUserId, email, phone);
        AppLogger.Info($"Contact details saved for {admin.UserName}.");
        return GetCurrentAdmin();
    }
}
