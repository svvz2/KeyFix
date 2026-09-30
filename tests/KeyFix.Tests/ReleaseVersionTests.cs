using KeyFix.Core;

namespace KeyFix.Tests;

public sealed class ReleaseVersionTests
{
    [Theory]
    [InlineData("1.4.1", "1.4.0", true)]
    [InlineData("v2.0.0", "1.9.9", true)]
    [InlineData("1.4.0", "1.4.0", false)]
    [InlineData("1.3.9", "1.4.0", false)]
    [InlineData("1.5.0-beta.1", "1.4.0", true)]
    [InlineData("not-a-version", "1.4.0", false)]
    public void IsNewer_compares_release_versions(string candidate, string current, bool expected)
    {
        Assert.Equal(expected, ReleaseVersion.IsNewer(candidate, current));
    }
}
