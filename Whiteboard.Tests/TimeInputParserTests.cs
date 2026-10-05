using Whiteboard.Core;

namespace Whiteboard.Tests;

public class TimeInputParserTests
{
    [Theory]
    [InlineData("14:30", 14, 30)]
    [InlineData("9:05", 9, 5)]
    [InlineData("09:05", 9, 5)]
    [InlineData("1430", 14, 30)]
    [InlineData("930", 9, 30)]
    [InlineData("14", 14, 0)]
    [InlineData("0", 0, 0)]
    [InlineData("23:59", 23, 59)]
    [InlineData(" 14:30 ", 14, 30)]
    [InlineData("１４：３０", 14, 30)]
    [InlineData("１４３０", 14, 30)]
    public void TryParse_読み取れる形(string input, int hour, int minute)
    {
        Assert.True(TimeInputParser.TryParse(input, out var time));
        Assert.Equal(new TimeOnly(hour, minute), time);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("24:00")]
    [InlineData("12:60")]
    [InlineData("2400")]
    [InlineData("1260")]
    [InlineData("12:5")]
    [InlineData("123:00")]
    [InlineData("12345")]
    [InlineData("12:3a")]
    [InlineData("ab")]
    [InlineData("-1")]
    [InlineData("12:30:00")]
    public void TryParse_読み取れない形(string? input)
    {
        Assert.False(TimeInputParser.TryParse(input, out _));
    }
}
