using VirtualAssistant.Data.Entities;

namespace VirtualAssistant.Data.EntityFrameworkCore.Tests.Entities;

public class StartupTypeTests
{
    [Fact]
    public void Normal_HasValue0()
    {
        Assert.Equal(0, (int)StartupType.Normal);
    }

    [Fact]
    public void AfterCrash_HasValue1()
    {
        Assert.Equal(1, (int)StartupType.AfterCrash);
    }

    [Fact]
    public void FrequentRestart_HasValue2()
    {
        Assert.Equal(2, (int)StartupType.FrequentRestart);
    }

    [Fact]
    public void Development_HasValue3()
    {
        Assert.Equal(3, (int)StartupType.Development);
    }

    [Fact]
    public void AllValues_AreUnique()
    {
        var values = Enum.GetValues<StartupType>();
        var distinctCount = values.Select(v => (int)v).Distinct().Count();

        Assert.Equal(values.Length, distinctCount);
    }

    [Theory]
    [InlineData(StartupType.Normal, "Normal")]
    [InlineData(StartupType.AfterCrash, "AfterCrash")]
    [InlineData(StartupType.FrequentRestart, "FrequentRestart")]
    [InlineData(StartupType.Development, "Development")]
    public void ToString_ReturnsExpectedName(StartupType type, string expectedName)
    {
        Assert.Equal(expectedName, type.ToString());
    }
}
