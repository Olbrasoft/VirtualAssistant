using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Olbrasoft.VirtualAssistant.Voice.Services;

namespace VirtualAssistant.Voice.Tests.Services;

/// <summary>
/// Unit tests for LocationService VPN detection functionality.
/// </summary>
public class LocationServiceTests
{
    private readonly Mock<ILogger<LocationService>> _loggerMock;

    public LocationServiceTests()
    {
        _loggerMock = new Mock<ILogger<LocationService>>();
    }

    private LocationService CreateService(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        return new LocationService(httpClient, _loggerMock.Object);
    }

    private static Mock<HttpMessageHandler> CreateMockHandler(HttpResponseMessage response)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);
        return handlerMock;
    }

    [Fact]
    public async Task GetLocationAsync_WhenInCzechRepublic_ReturnsNoVpn()
    {
        // Arrange
        var responseContent = new
        {
            status = "success",
            countryCode = "CZ",
            city = "Prague",
            isp = "O2",
            proxy = false,
            hosting = false
        };

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(responseContent)
        };

        var handlerMock = CreateMockHandler(response);
        var sut = CreateService(handlerMock.Object);

        // Act
        var result = await sut.GetLocationAsync();

        // Assert
        Assert.Equal("CZ", result.CountryCode);
        Assert.False(result.IsVpnDetected);
        Assert.Equal("Prague", result.City);
        Assert.Equal("O2", result.Isp);
    }

    [Fact]
    public async Task GetLocationAsync_WhenInGermany_ReturnsVpnDetected()
    {
        // Arrange
        var responseContent = new
        {
            status = "success",
            countryCode = "DE",
            city = "Frankfurt",
            isp = "Windscribe",
            proxy = false,
            hosting = false
        };

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(responseContent)
        };

        var handlerMock = CreateMockHandler(response);
        var sut = CreateService(handlerMock.Object);

        // Act
        var result = await sut.GetLocationAsync();

        // Assert
        Assert.Equal("DE", result.CountryCode);
        Assert.True(result.IsVpnDetected);
    }

    [Fact]
    public async Task GetLocationAsync_WhenProxyDetected_ReturnsVpnDetected()
    {
        // Arrange
        var responseContent = new
        {
            status = "success",
            countryCode = "CZ",
            city = "Prague",
            isp = "O2",
            proxy = true,
            hosting = false
        };

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(responseContent)
        };

        var handlerMock = CreateMockHandler(response);
        var sut = CreateService(handlerMock.Object);

        // Act
        var result = await sut.GetLocationAsync();

        // Assert
        Assert.True(result.IsVpnDetected);
    }

    [Fact]
    public async Task GetLocationAsync_WhenHostingDetected_ReturnsVpnDetected()
    {
        // Arrange
        var responseContent = new
        {
            status = "success",
            countryCode = "CZ",
            city = "Prague",
            isp = "DataCenter",
            proxy = false,
            hosting = true
        };

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(responseContent)
        };

        var handlerMock = CreateMockHandler(response);
        var sut = CreateService(handlerMock.Object);

        // Act
        var result = await sut.GetLocationAsync();

        // Assert
        Assert.True(result.IsVpnDetected);
    }

    [Fact]
    public async Task GetLocationAsync_WhenApiFails_ReturnsDefaultCzLocation()
    {
        // Arrange
        var response = new HttpResponseMessage(HttpStatusCode.InternalServerError);

        var handlerMock = CreateMockHandler(response);
        var sut = CreateService(handlerMock.Object);

        // Act
        var result = await sut.GetLocationAsync();

        // Assert - fail-open: assume no VPN on error
        Assert.Equal("CZ", result.CountryCode);
        Assert.False(result.IsVpnDetected);
    }

    [Fact]
    public async Task GetLocationAsync_WhenApiReturnsInvalidStatus_ReturnsDefaultCzLocation()
    {
        // Arrange
        var responseContent = new
        {
            status = "fail",
            message = "reserved range"
        };

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(responseContent)
        };

        var handlerMock = CreateMockHandler(response);
        var sut = CreateService(handlerMock.Object);

        // Act
        var result = await sut.GetLocationAsync();

        // Assert
        Assert.Equal("CZ", result.CountryCode);
        Assert.False(result.IsVpnDetected);
    }

    [Fact]
    public async Task GetLocationAsync_CachesResult()
    {
        // Arrange
        var callCount = 0;
        var responseContent = new
        {
            status = "success",
            countryCode = "CZ",
            city = "Prague",
            isp = "O2",
            proxy = false,
            hosting = false
        };

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(responseContent)
                };
            });

        var sut = CreateService(handlerMock.Object);

        // Act - call multiple times
        await sut.GetLocationAsync();
        await sut.GetLocationAsync();
        await sut.GetLocationAsync();

        // Assert - API should only be called once (cached)
        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task IsVpnActiveAsync_WhenInCzechRepublic_ReturnsFalse()
    {
        // Arrange
        var responseContent = new
        {
            status = "success",
            countryCode = "CZ",
            city = "Prague",
            isp = "O2",
            proxy = false,
            hosting = false
        };

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(responseContent)
        };

        var handlerMock = CreateMockHandler(response);
        var sut = CreateService(handlerMock.Object);

        // Act
        var result = await sut.IsVpnActiveAsync();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task IsVpnActiveAsync_WhenNotInCzechRepublic_ReturnsTrue()
    {
        // Arrange
        var responseContent = new
        {
            status = "success",
            countryCode = "DE",
            city = "Berlin",
            isp = "VPN Provider",
            proxy = false,
            hosting = false
        };

        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(responseContent)
        };

        var handlerMock = CreateMockHandler(response);
        var sut = CreateService(handlerMock.Object);

        // Act
        var result = await sut.IsVpnActiveAsync();

        // Assert
        Assert.True(result);
    }
}
