using System.Data;
using HostelManagement.Models;
using HostelManagement.Utilities;

namespace HostelManagement.Data;

public static class AdminUserRepository
{
    /// <summary>The admin with this user name (Access compares text without regard to case).</summary>
    public static AdminUser? Get(string userName) =>
        Db.Query("SELECT * FROM [AdminUser] WHERE [UserName] = ?", Map, Db.Param("@UserName", userName)).FirstOrDefault();

    /// <summary>The first admin, whose email receives a copy of every email (there is one admin).</summary>
    public static AdminUser? GetFirst() =>
        Db.Query("SELECT TOP 1 * FROM [AdminUser] ORDER BY [AdminUserId]", Map).FirstOrDefault();

    public static string? GetPasswordHash(string userName) =>
        Db.Query("SELECT [PasswordHash] FROM [AdminUser] WHERE [UserName] = ?", r => r.GetText("PasswordHash"),
            Db.Param("@UserName", userName)).FirstOrDefault();

    public static void UpdatePasswordHash(int adminUserId, string passwordHash) =>
        Db.Execute("UPDATE [AdminUser] SET [PasswordHash] = ?, [UpdatedDate] = ? WHERE [AdminUserId] = ?",
            Db.Param("@PasswordHash", passwordHash),
            Db.Param("@UpdatedDate", DateTime.Now),
            Db.Param("@AdminUserId", adminUserId));

    public static void UpdateContact(int adminUserId, string email, string phone) =>
        Db.Execute("UPDATE [AdminUser] SET [Email] = ?, [Phone] = ?, [UpdatedDate] = ? WHERE [AdminUserId] = ?",
            Db.OptionalText("@Email", email),
            Db.OptionalText("@Phone", phone),
            Db.Param("@UpdatedDate", DateTime.Now),
            Db.Param("@AdminUserId", adminUserId));

    /// <summary>Adds the default admin (admin / admin) when the table is empty, so a new database can be signed in to.</summary>
    internal static void EnsureDefaultAdmin()
    {
        if (Convert.ToInt32(Db.Scalar("SELECT COUNT(*) FROM [AdminUser]")) > 0)
        {
            return;
        }
        Db.Execute("INSERT INTO [AdminUser] ([UserName], [PasswordHash], [CreatedDate]) VALUES (?, ?, ?)",
            Db.Param("@UserName", AdminUser.DefaultUserName),
            Db.Param("@PasswordHash", PasswordHasher.Hash(AdminUser.DefaultPassword)),
            Db.Param("@CreatedDate", DateTime.Now));
        AppLogger.Info("Added the default admin login.");
    }

    private static AdminUser Map(IDataRecord record) => new()
    {
        AdminUserId = record.GetInt("AdminUserId"),
        UserName = record.GetText("UserName"),
        Email = record.GetText("Email"),
        Phone = record.GetText("Phone"),
    };
}
