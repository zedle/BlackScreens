using BlackScreens.Ui;
using Xunit;

namespace BlackScreens.Tests;

public sealed class AppInfoTests
{
    [Theory]
    [InlineData("1.0.0+f378c090699c5a7c54095e35c26531a4e478cb25", "1.0.0")]
    [InlineData("1.2.3", "1.2.3")]
    [InlineData("  1.2.3-beta.1+abc  ", "1.2.3-beta.1")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void TrimVersion_drops_source_control_metadata(string? productVersion, string expected)
    {
        Assert.Equal(expected, AppInfo.TrimVersion(productVersion));
    }

    [Theory]
    [InlineData("CN=SignPath Foundation, O=SignPath GmbH, C=AT", "SignPath Foundation")]
    [InlineData("O=Example, CN=Example Publisher", "Example Publisher")]
    [InlineData("cn=lowercase key", "lowercase key")]
    [InlineData("O=No common name", "O=No common name")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void CommonName_pulls_cn_out_of_a_distinguished_name(string? subject, string expected)
    {
        Assert.Equal(expected, AppInfo.CommonName(subject));
    }
}
