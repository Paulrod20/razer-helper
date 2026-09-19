using RazerHelper.Helpers;

namespace RazerHelper.Tests.Helpers;

public class AppVersionTests
{
    [Theory]
    [InlineData(1, 0, 0, "v1.0")]
    [InlineData(1, 2, 0, "v1.2")]
    [InlineData(1, 0, 3, "v1.0.3")]
    [InlineData(2, 10, 1, "v2.10.1")]
    public void Format_ShowsPatchOnlyWhenNotZero(int major, int minor, int patch, string expected) =>
        Assert.Equal(expected, AppVersion.Format(new Version(major, minor, patch)));

    [Fact]
    public void Format_WithTwoPartVersion_HasNoPatch() =>
        Assert.Equal("v1.4", AppVersion.Format(new Version(1, 4)));

    [Fact]
    public void Format_WithoutVersion_IsEmpty() =>
        Assert.Equal(string.Empty, AppVersion.Format(null));

    [Fact]
    public void Current_IsReadFromTheAssembly() =>
        Assert.Matches(@"^v\d+\.\d+(\.\d+)?$", AppVersion.Current);
}
