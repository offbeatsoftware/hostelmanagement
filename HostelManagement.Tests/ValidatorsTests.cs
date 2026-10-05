using HostelManagement.Utilities;
using Xunit;

namespace HostelManagement.Tests;

public sealed class ValidatorsTests
{
    [Theory]
    [InlineData("")]
    [InlineData("office@hostel.com")]
    [InlineData("first.last@college.ac.in")]
    public void Email_Valid(string email) => Assert.True(Validators.IsValidEmailOrEmpty(email));

    [Theory]
    [InlineData("office")]
    [InlineData("office@hostel")]
    [InlineData("a@b.com; c@d.com")]
    [InlineData("Office <office@hostel.com>")]
    public void Email_Invalid(string email) => Assert.False(Validators.IsValidEmailOrEmpty(email));

    [Theory]
    [InlineData("")]
    [InlineData("9876543210")]
    [InlineData("+91 98765 43210")]
    [InlineData("(0172) 270-1234")]
    public void Phone_Valid(string phone) => Assert.True(Validators.IsValidPhoneOrEmpty(phone));

    [Theory]
    [InlineData("12345")]
    [InlineData("98765abc10")]
    [InlineData("1234567890123456")]
    public void Phone_Invalid(string phone) => Assert.False(Validators.IsValidPhoneOrEmpty(phone));

    [Fact]
    public void Clean_TrimsAndHandlesNull()
    {
        Assert.Equal("Hostel", Validators.Clean("  Hostel  "));
        Assert.Equal(string.Empty, Validators.Clean(null));
    }
}
