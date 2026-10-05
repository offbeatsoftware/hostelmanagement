using HostelManagement.Utilities;
using Xunit;

namespace HostelManagement.Tests;

public sealed class MoneyAndSortingTests
{
    [Theory]
    [InlineData("4500", 4500)]
    [InlineData("4,500.50", 4500.50)]
    [InlineData("₹1,25,000", 125000)]
    [InlineData(" 999.99 ", 999.99)]
    public void Money_TryParse_AcceptsCommonFormats(string text, decimal expected)
    {
        Assert.True(Money.TryParse(text, out decimal amount));
        Assert.Equal(expected, amount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("12a")]
    public void Money_TryParse_RejectsText(string text) => Assert.False(Money.TryParse(text, out _));

    [Fact]
    public void Money_Format_UsesRupeeAndIndianGrouping()
    {
        string text = Money.Format(125000m);
        Assert.StartsWith("₹", text);
        Assert.EndsWith("1,25,000.00", text);
    }

    [Fact]
    public void NaturalComparer_SortsNumbersByValue() =>
        Assert.Equal(["1", "2", "9", "10", "B1", "B2", "B10"],
            new[] { "10", "B10", "2", "B2", "1", "9", "B1" }.Order(NaturalComparer.Instance));
}
