using VirtualAssistant.Data.Entities;

namespace VirtualAssistant.Data.EntityFrameworkCore.Tests.Entities;

public class ShutdownTypeTests
{
    [Fact]
    public void Unknown_HasValue0()
    {
        Assert.Equal(0, (int)ShutdownType.Unknown);
    }

    [Fact]
    public void Clean_HasValue1()
    {
        Assert.Equal(1, (int)ShutdownType.Clean);
    }

    [Fact]
    public void Crash_HasValue2()
    {
        Assert.Equal(2, (int)ShutdownType.Crash);
    }

    [Fact]
    public void AllValues_AreUnique()
    {
        var values = Enum.GetValues<ShutdownType>();
        var distinctCount = values.Select(v => (int)v).Distinct().Count();

        Assert.Equal(values.Length, distinctCount);
    }

    [Theory]
    [InlineData(ShutdownType.Unknown, "Unknown")]
    [InlineData(ShutdownType.Clean, "Clean")]
    [InlineData(ShutdownType.Crash, "Crash")]
    public void ToString_ReturnsExpectedName(ShutdownType type, string expectedName)
    {
        Assert.Equal(expectedName, type.ToString());
    }
}
