using Whiteboard.Core;

namespace Whiteboard.Tests;

public class ReleaseVersionTests
{
    [Theory]
    [InlineData("v0.2.0", "0.2.0.0")]
    [InlineData("V1.0", "1.0.0.0")]
    [InlineData("0.10.3", "0.10.3.0")]
    [InlineData("v2", "2.0.0.0")]
    [InlineData(" v1.2.3.4 ", "1.2.3.4")]
    public void タグを読む(string tag, string expected)
    {
        Assert.True(ReleaseVersion.TryParse(tag, out var version));
        Assert.Equal(Version.Parse(expected), version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("v")]
    [InlineData("v0.2.0-beta")]
    [InlineData("release-1")]
    [InlineData("1..2")]
    [InlineData("1.2.3.4.5")]
    public void 読めないタグ(string? tag)
    {
        Assert.False(ReleaseVersion.TryParse(tag, out _));
    }

    [Theory]
    [InlineData("0.2.0", "0.1.0.0", true)]
    [InlineData("0.10.0", "0.9.0.0", true)]
    [InlineData("0.1.0", "0.1.0.0", false)]
    [InlineData("0.1", "0.1.0.0", false)]
    [InlineData("0.0.9", "0.1.0.0", false)]
    [InlineData("0.1.0.1", "0.1.0.0", true)]
    public void 新しいかを桁数に関係なく比べる(string latest, string current, bool expected)
    {
        Assert.Equal(expected, ReleaseVersion.IsNewer(Version.Parse(latest), Version.Parse(current)));
    }

    [Theory]
    [InlineData("0.2.0.0", "0.2.0")]
    [InlineData("0.2", "0.2.0")]
    [InlineData("1.0.0.1", "1.0.0.1")]
    public void 表示用の文字列(string version, string expected)
    {
        Assert.Equal(expected, ReleaseVersion.ToDisplayString(Version.Parse(version)));
    }
}
