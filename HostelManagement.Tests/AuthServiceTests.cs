using HostelManagement.Data;
using HostelManagement.Models;
using HostelManagement.Services;
using HostelManagement.Utilities;
using Xunit;

namespace HostelManagement.Tests;

public sealed class AuthServiceTests : TestDatabase
{
    [Theory]
    [InlineData("admin", "admin")]
    [InlineData("ADMIN", "admin")]
    [InlineData(" admin ", "admin")]
    public void NewDatabase_SignsInWithTheDefaultLogin(string user, string password) =>
        Assert.True(AuthService.IsValidLogin(user, password));

    [Theory]
    [InlineData("admin", "Admin")]
    [InlineData("admin", "")]
    [InlineData("", "admin")]
    [InlineData("administrator", "admin")]
    [InlineData(null, null)]
    public void InvalidLogin(string? user, string? password) =>
        Assert.False(AuthService.IsValidLogin(user, password));

    [Fact]
    public void Password_IsStoredAsASaltedHash()
    {
        string hash = Convert.ToString(Db.Scalar("SELECT [PasswordHash] FROM [AdminUser]"))!;

        Assert.StartsWith("pbkdf2-sha256$", hash);
        Assert.DoesNotContain("$admin", hash);
        Assert.NotEqual(PasswordHasher.Hash("admin"), PasswordHasher.Hash("admin"));
        Assert.True(PasswordHasher.Verify("admin", hash));
        Assert.False(PasswordHasher.Verify("admin", "not a hash"));
    }

    [Fact]
    public void DefaultAdmin_IsAddedOnlyOnce()
    {
        DatabaseInitializer.Initialize();

        Assert.Equal(1, Count("AdminUser"));
    }

    [Fact]
    public void ChangePassword_TheNewPasswordWorksAndTheOldOneDoesNot()
    {
        AuthService.IsValidLogin("admin", "admin");

        AuthService.ChangePassword("admin", "Balaji@2026", "Balaji@2026");

        Assert.True(AuthService.IsValidLogin("admin", "Balaji@2026"));
        Assert.False(AuthService.IsValidLogin("admin", "admin"));
    }

    [Fact]
    public void ChangePassword_Rules()
    {
        AuthService.IsValidLogin("admin", "admin");

        Assert.Contains("current password is not correct",
            Assert.Throws<ValidationException>(() => AuthService.ChangePassword("wrong", "Balaji@2026", "Balaji@2026")).Message);
        Assert.Contains("at least 6",
            Assert.Throws<ValidationException>(() => AuthService.ChangePassword("admin", "abc", "abc")).Message);
        Assert.Contains("not the same",
            Assert.Throws<ValidationException>(() => AuthService.ChangePassword("admin", "Balaji@2026", "Balaji@2027")).Message);
        Assert.Contains("space",
            Assert.Throws<ValidationException>(() => AuthService.ChangePassword("admin", " Balaji@2026", " Balaji@2026")).Message);
        Assert.True(AuthService.IsValidLogin("admin", "admin"));
    }

    [Fact]
    public void SaveContact_StoresEmailAndPhone()
    {
        AdminUser admin = AuthService.SaveContact(" owner@gmail.com ", "98290 12345");

        Assert.Equal("owner@gmail.com", admin.Email);
        Assert.Equal("98290 12345", admin.Phone);
        Assert.Equal("owner@gmail.com", AuthService.GetAdminEmail());

        Assert.Contains("valid email", Assert.Throws<ValidationException>(() => AuthService.SaveContact("owner", "")).Message);
        Assert.Contains("valid phone", Assert.Throws<ValidationException>(() => AuthService.SaveContact("", "12")).Message);

        AuthService.SaveContact("", "");
        Assert.Equal("", AuthService.GetAdminEmail());
    }
}
