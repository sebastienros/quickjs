using Xunit;

namespace QuickJS.Tests;

public class EngineTests
{
    [Fact]
    public void Version_ReturnsExpectedVersion()
    {
        // Arrange & Act
        var version = Engine.Version;

        // Assert
        Assert.Equal("0.1.0", version);
    }
}
