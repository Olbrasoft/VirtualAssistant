using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Olbrasoft.TextToSpeech.Providers.GoogleCloud;
using Olbrasoft.VirtualAssistant.Service.Controllers;
using Olbrasoft.VirtualAssistant.Service.Infrastructure;

namespace Olbrasoft.VirtualAssistant.Service.Tests.Controllers;

public class TtsControllerKeysUsageTests
{
    private static TtsController CreateController()
    {
        var logger = new Mock<ILogger<TtsController>>();
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        return new TtsController(logger.Object, config);
    }

    [Fact]
    public void GetKeysUsage_ReturnsOkWithMappedDtos()
    {
        var snapshot = new ApiKeyUsageSnapshot
        {
            Index = 0,
            Name = "primary",
            State = ApiKeyState.Available,
            CooldownUntilUtc = null,
            MonthlyCharacterCount = 12_345,
            MonthlyCharacterLimit = 1_000_000,
            TotalSuccesses = 100,
            TotalFailures = 2,
            ConsecutiveFailures = 0,
            LastSuccessUtc = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc),
            LastErrorUtc = null,
            LastErrorReason = null
        };
        var reporter = new Mock<IKeysUsageReporter>();
        reporter.Setup(r => r.GetKeysUsage()).Returns(new[] { snapshot });

        var controller = CreateController();

        var result = controller.GetKeysUsage(reporter.Object);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dtos = Assert.IsAssignableFrom<IReadOnlyList<TtsKeyUsageDto>>(ok.Value);
        var dto = Assert.Single(dtos);
        Assert.Equal(0, dto.Index);
        Assert.Equal("primary", dto.Name);
        Assert.Equal("Available", dto.State);
        Assert.Equal(12_345, dto.MonthlyCharacterCount);
        Assert.Equal(1_000_000, dto.MonthlyCharacterLimit);
        Assert.Equal(100, dto.TotalSuccesses);
        Assert.Equal(2, dto.TotalFailures);
        Assert.Equal(0, dto.ConsecutiveFailures);
        Assert.Equal(snapshot.LastSuccessUtc, dto.LastSuccessUtc);
        Assert.Null(dto.LastErrorUtc);
        Assert.Null(dto.LastErrorReason);
    }

    [Fact]
    public void GetKeysUsage_RendersAllStateValuesAsString()
    {
        var snapshots = new[]
        {
            new ApiKeyUsageSnapshot { Index = 0, Name = "k1", State = ApiKeyState.RateLimited },
            new ApiKeyUsageSnapshot { Index = 1, Name = "k2", State = ApiKeyState.QuotaExceeded },
            new ApiKeyUsageSnapshot { Index = 2, Name = "k3", State = ApiKeyState.Invalid },
            new ApiKeyUsageSnapshot { Index = 3, Name = "k4", State = ApiKeyState.MonthlyLimitExceeded }
        };
        var reporter = new Mock<IKeysUsageReporter>();
        reporter.Setup(r => r.GetKeysUsage()).Returns(snapshots);

        var controller = CreateController();
        var result = controller.GetKeysUsage(reporter.Object);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dtos = Assert.IsAssignableFrom<IReadOnlyList<TtsKeyUsageDto>>(ok.Value);
        Assert.Collection(dtos,
            d => Assert.Equal("RateLimited", d.State),
            d => Assert.Equal("QuotaExceeded", d.State),
            d => Assert.Equal("Invalid", d.State),
            d => Assert.Equal("MonthlyLimitExceeded", d.State));
    }

    [Fact]
    public void GetKeysUsage_EmptyKeyList_ReturnsEmptyDtoList()
    {
        var reporter = new Mock<IKeysUsageReporter>();
        reporter.Setup(r => r.GetKeysUsage()).Returns(Array.Empty<ApiKeyUsageSnapshot>());

        var controller = CreateController();
        var result = controller.GetKeysUsage(reporter.Object);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dtos = Assert.IsAssignableFrom<IReadOnlyList<TtsKeyUsageDto>>(ok.Value);
        Assert.Empty(dtos);
    }
}
