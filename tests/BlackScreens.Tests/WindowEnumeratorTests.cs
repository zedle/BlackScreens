using Xunit;

namespace BlackScreens.Tests;

public sealed class WindowEnumeratorTests
{
    [Fact]
    public void Capture_does_not_throw()
    {
        var windows = WindowEnumerator.Capture();

        Assert.NotNull(windows);
    }
}
