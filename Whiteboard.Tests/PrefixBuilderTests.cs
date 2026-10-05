using Whiteboard.Core;

namespace Whiteboard.Tests;

public class PrefixBuilderTests
{
    private static readonly DateTimeOffset At1430 = new(2026, 10, 5, 14, 30, 59, TimeSpan.FromHours(9));

    [Fact]
    public void Build_既定のテンプレートで前置きを作る()
    {
        var builder = new PrefixBuilder("{time} {how} {from}→{to} ", "HH:mm");

        Assert.Equal("14:30 tel 研究室→会議室 ", builder.Build("tel", "研究室", "会議室", At1430));
    }

    [Fact]
    public void Build_時刻の書式を設定で変えられる()
    {
        var builder = new PrefixBuilder("[{time}] {how}", "H時mm分");

        Assert.Equal("[14時30分] A", builder.Build("A", "x", "y", At1430));
    }

    [Fact]
    public void Build_午前の時刻は0埋めする()
    {
        var builder = new PrefixBuilder("{time}", "HH:mm");

        Assert.Equal("09:05", builder.Build("A", "x", "y", new DateTimeOffset(2026, 10, 5, 9, 5, 0, TimeSpan.Zero)));
    }

    [Fact]
    public void Build_値に含まれるプレースホルダーは置き換えない()
    {
        var builder = new PrefixBuilder("{how} {from}→{to}", "HH:mm");

        Assert.Equal("{from} {to}→{time}", builder.Build("{from}", "{to}", "{time}", At1430));
    }

    [Fact]
    public void Build_未知のプレースホルダーはそのまま残す()
    {
        var builder = new PrefixBuilder("{unknown} {how}", "HH:mm");

        Assert.Equal("{unknown} A", builder.Build("A", "x", "y", At1430));
    }
}
