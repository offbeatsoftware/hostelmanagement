using HostelManagement.Services;
using Xunit;

namespace HostelManagement.Tests;

public sealed class AuthServiceTests
{
    [Theory]
    [InlineData("admin", "admin")]
    [InlineData("ADMIN", "admin")]
    [InlineData(" admin ", "admin")]
    public void ValidLogin(string user, string password) => Assert.True(AuthService.IsValidLogin(user, password));

    [Theory]
    [InlineData("admin", "Admin")]
    [InlineData("admin", "")]
    [InlineData("", "admin")]
    [InlineData("administrator", "admin")]
    [InlineData(null, null)]
    public void InvalidLogin(string? user, string? password) =>
        Assert.False(AuthService.IsValidLogin(user, password));
}
