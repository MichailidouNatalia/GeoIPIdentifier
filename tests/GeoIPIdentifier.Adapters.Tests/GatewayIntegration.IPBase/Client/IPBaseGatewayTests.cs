using Newtonsoft.Json;
using GeoIPIdentifier.Domain.Entities;
using GeoIPIdentifier.Domain.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using GeoIPIdentifier.Adapters.GatewayIntegration.IPBase.DTOs;
using AutoMapper;
using Moq;
using Moq.Protected;
using GeoIPIdentifier.Adapters.GatewayIntegration.IPBase.Client;

namespace GeoIPIdentifier.Adapters.Tests.GatewayIntegration.IPBase.Client
{
  public class IPBaseGatewayTests
  {
    private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
    private readonly HttpClient _httpClient;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ILogger<IPBaseGateway>> _mockLogger;
    private readonly Mock<IMapper> _mockMapper;
    private readonly IPBaseGateway _ipBaseGateway;

    public IPBaseGatewayTests()
    {
      _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
      _httpClient = new HttpClient(_mockHttpMessageHandler.Object);
      _mockConfiguration = new Mock<IConfiguration>();
      _mockLogger = new Mock<ILogger<IPBaseGateway>>();
      _mockMapper = new Mock<IMapper>();

      _ipBaseGateway = new IPBaseGateway(
          _httpClient,
          _mockConfiguration.Object,
          _mockLogger.Object,
          _mockMapper.Object);
    }

    [Fact]
    public async Task GetGeoIPDataAsync_WithValidResponse_ReturnsMappedGeoIPData()
    {
      // Arrange
      var ipAddress = "192.168.1.1";
      var apiKey = "test-api-key";

      // Create a properly structured IPBaseDataDto with all required nested objects
      var ipBaseData = new IPBaseDataDto
      {
        IP = ipAddress,
        Location = new Location
        {
          Latitude = 37.7510m,
          Longitude = -97.8220m,
          Country = new Country
          {
            Alpha2 = "US",
            Name = "United States"
          }
        },
        Timezone = new Timezone
        {
          Code = "America/Chicago"
        }
      };

      var ipBaseResponse = new IPBaseResponseDto
      {
        Data = ipBaseData
      };

      var expectedGeoIPData = new GeoIPData();

      _mockConfiguration
          .Setup(x => x["IPBase:ApiKey"])
          .Returns(apiKey);

      var responseContent = JsonConvert.SerializeObject(ipBaseResponse);
      var httpResponse = new HttpResponseMessage
      {
        StatusCode = System.Net.HttpStatusCode.OK,
        Content = new StringContent(responseContent, System.Text.Encoding.UTF8, "application/json")
      };

      _mockHttpMessageHandler
          .Protected()
          .Setup<Task<HttpResponseMessage>>(
              "SendAsync",
              ItExpr.IsAny<HttpRequestMessage>(),
              ItExpr.IsAny<CancellationToken>())
          .ReturnsAsync(httpResponse);

      _mockMapper
          .Setup(x => x.Map<GeoIPData>(It.IsAny<IPBaseDataDto>()))
          .Returns(expectedGeoIPData);

      // Act
      var result = await _ipBaseGateway.GetGeoIPDataAsync(ipAddress);

      // Assert
      Assert.NotNull(result);
      Assert.Same(expectedGeoIPData, result);
      _mockMapper.Verify(x => x.Map<GeoIPData>(It.IsAny<IPBaseDataDto>()), Times.Once);
    }
    
    [Fact]
    public async Task GetGeoIPDataAsync_WithMissingApiKey_ThrowsDomainException()
    {
      // Arrange
      var ipAddress = "192.168.1.1";

      _mockConfiguration
          .Setup(x => x["IPBase:ApiKey"])
          .Returns((string)null);

      // Act & Assert
      await Assert.ThrowsAsync<DomainException>(() => _ipBaseGateway.GetGeoIPDataAsync(ipAddress));
    }

    [Fact]
    public async Task GetGeoIPDataAsync_WithEmptyApiKey_ThrowsDomainException()
    {
      // Arrange
      var ipAddress = "192.168.1.1";

      _mockConfiguration
          .Setup(x => x["IPBase:ApiKey"])
          .Returns("");

      // Act & Assert
      await Assert.ThrowsAsync<DomainException>(() => _ipBaseGateway.GetGeoIPDataAsync(ipAddress));
    }

    [Fact]
    public async Task GetGeoIPDataAsync_WithHttpRequestException_ThrowsGeoIPServiceUnavailableException()
    {
      // Arrange
      var ipAddress = "192.168.1.1";
      var apiKey = "test-api-key";

      _mockConfiguration
          .Setup(x => x["IPBase:ApiKey"])
          .Returns(apiKey);

      _mockHttpMessageHandler
          .Protected()
          .Setup<Task<HttpResponseMessage>>(
              "SendAsync",
              ItExpr.IsAny<HttpRequestMessage>(),
              ItExpr.IsAny<CancellationToken>())
          .ThrowsAsync(new HttpRequestException("Network error"));

      // Act & Assert
      await Assert.ThrowsAsync<GeoIPServiceUnavailableException>(() => _ipBaseGateway.GetGeoIPDataAsync(ipAddress));
    }

    [Fact]
    public async Task GetGeoIPDataAsync_WithJsonException_ThrowsDomainException()
    {
      // Arrange
      var ipAddress = "192.168.1.1";
      var apiKey = "test-api-key";

      _mockConfiguration
          .Setup(x => x["IPBase:ApiKey"])
          .Returns(apiKey);

      var httpResponse = new HttpResponseMessage
      {
        StatusCode = System.Net.HttpStatusCode.OK,
        Content = new StringContent("invalid json")
      };

      _mockHttpMessageHandler
          .Protected()
          .Setup<Task<HttpResponseMessage>>(
              "SendAsync",
              ItExpr.IsAny<HttpRequestMessage>(),
              ItExpr.IsAny<CancellationToken>())
          .ReturnsAsync(httpResponse);

      // Act & Assert
      await Assert.ThrowsAsync<DomainException>(() => _ipBaseGateway.GetGeoIPDataAsync(ipAddress));
    }

    [Fact]
    public async Task GetGeoIPDataAsync_WithNullResponseData_ThrowsDomainException()
    {
      // Arrange
      var ipAddress = "192.168.1.1";
      var apiKey = "test-api-key";
      var ipBaseResponse = new IPBaseResponseDto { Data = null };

      _mockConfiguration
          .Setup(x => x["IPBase:ApiKey"])
          .Returns(apiKey);

      var responseContent = JsonConvert.SerializeObject(ipBaseResponse);
      var httpResponse = new HttpResponseMessage
      {
        StatusCode = System.Net.HttpStatusCode.OK,
        Content = new StringContent(responseContent)
      };

      _mockHttpMessageHandler
          .Protected()
          .Setup<Task<HttpResponseMessage>>(
              "SendAsync",
              ItExpr.IsAny<HttpRequestMessage>(),
              ItExpr.IsAny<CancellationToken>())
          .ReturnsAsync(httpResponse);

      // Act & Assert
      await Assert.ThrowsAsync<DomainException>(() => _ipBaseGateway.GetGeoIPDataAsync(ipAddress));
    }

    [Fact]
    public async Task GetGeoIPDataAsync_With400StatusCode_ThrowsInvalidIPAddressException()
    {
      // Arrange
      var ipAddress = "192.168.1.1";
      var apiKey = "test-api-key";

      _mockConfiguration
          .Setup(x => x["IPBase:ApiKey"])
          .Returns(apiKey);

      var httpResponse = new HttpResponseMessage
      {
        StatusCode = System.Net.HttpStatusCode.BadRequest,
        Content = new StringContent("error")
      };

      _mockHttpMessageHandler
          .Protected()
          .Setup<Task<HttpResponseMessage>>(
              "SendAsync",
              ItExpr.IsAny<HttpRequestMessage>(),
              ItExpr.IsAny<CancellationToken>())
          .ReturnsAsync(httpResponse);

      // Act & Assert
      await Assert.ThrowsAsync<InvalidIPAddressException>(() => _ipBaseGateway.GetGeoIPDataAsync(ipAddress));
    }

    [Fact]
    public async Task GetGeoIPDataAsync_With401StatusCode_ThrowsDomainException()
    {
      // Arrange
      var ipAddress = "192.168.1.1";
      var apiKey = "test-api-key";

      _mockConfiguration
          .Setup(x => x["IPBase:ApiKey"])
          .Returns(apiKey);

      var httpResponse = new HttpResponseMessage
      {
        StatusCode = System.Net.HttpStatusCode.Unauthorized,
        Content = new StringContent("error")
      };

      _mockHttpMessageHandler
          .Protected()
          .Setup<Task<HttpResponseMessage>>(
              "SendAsync",
              ItExpr.IsAny<HttpRequestMessage>(),
              ItExpr.IsAny<CancellationToken>())
          .ReturnsAsync(httpResponse);

      // Act & Assert
      var exception = await Assert.ThrowsAsync<DomainException>(() => _ipBaseGateway.GetGeoIPDataAsync(ipAddress));
      Assert.Contains("API key", exception.Message);
    }

    [Fact]
    public async Task GetGeoIPDataAsync_With403StatusCode_ThrowsDomainException()
    {
      // Arrange
      var ipAddress = "192.168.1.1";
      var apiKey = "test-api-key";

      _mockConfiguration
          .Setup(x => x["IPBase:ApiKey"])
          .Returns(apiKey);

      var httpResponse = new HttpResponseMessage
      {
        StatusCode = System.Net.HttpStatusCode.Forbidden,
        Content = new StringContent("error")
      };

      _mockHttpMessageHandler
          .Protected()
          .Setup<Task<HttpResponseMessage>>(
              "SendAsync",
              ItExpr.IsAny<HttpRequestMessage>(),
              ItExpr.IsAny<CancellationToken>())
          .ReturnsAsync(httpResponse);

      // Act & Assert
      var exception = await Assert.ThrowsAsync<DomainException>(() => _ipBaseGateway.GetGeoIPDataAsync(ipAddress));
      Assert.Contains("API key", exception.Message);
    }

    [Fact]
    public async Task GetGeoIPDataAsync_With429StatusCode_ThrowsDomainException()
    {
      // Arrange
      var ipAddress = "192.168.1.1";
      var apiKey = "test-api-key";

      _mockConfiguration
          .Setup(x => x["IPBase:ApiKey"])
          .Returns(apiKey);

      var httpResponse = new HttpResponseMessage
      {
        StatusCode = System.Net.HttpStatusCode.TooManyRequests,
        Content = new StringContent("error")
      };

      _mockHttpMessageHandler
          .Protected()
          .Setup<Task<HttpResponseMessage>>(
              "SendAsync",
              ItExpr.IsAny<HttpRequestMessage>(),
              ItExpr.IsAny<CancellationToken>())
          .ReturnsAsync(httpResponse);

      // Act & Assert
      var exception = await Assert.ThrowsAsync<DomainException>(() => _ipBaseGateway.GetGeoIPDataAsync(ipAddress));
      Assert.Contains("Rate limit", exception.Message);
    }

    [Fact]
    public async Task GetGeoIPDataAsync_With500StatusCode_ThrowsGeoIPServiceUnavailableException()
    {
      // Arrange
      var ipAddress = "192.168.1.1";
      var apiKey = "test-api-key";

      _mockConfiguration
          .Setup(x => x["IPBase:ApiKey"])
          .Returns(apiKey);

      var httpResponse = new HttpResponseMessage
      {
        StatusCode = System.Net.HttpStatusCode.InternalServerError,
        Content = new StringContent("error")
      };

      _mockHttpMessageHandler
          .Protected()
          .Setup<Task<HttpResponseMessage>>(
              "SendAsync",
              ItExpr.IsAny<HttpRequestMessage>(),
              ItExpr.IsAny<CancellationToken>())
          .ReturnsAsync(httpResponse);

      // Act & Assert
      await Assert.ThrowsAsync<GeoIPServiceUnavailableException>(() => _ipBaseGateway.GetGeoIPDataAsync(ipAddress));
    }

    [Fact]
    public async Task GetGeoIPDataAsync_With502StatusCode_ThrowsGeoIPServiceUnavailableException()
    {
      // Arrange
      var ipAddress = "192.168.1.1";
      var apiKey = "test-api-key";

      _mockConfiguration
          .Setup(x => x["IPBase:ApiKey"])
          .Returns(apiKey);

      var httpResponse = new HttpResponseMessage
      {
        StatusCode = System.Net.HttpStatusCode.BadGateway,
        Content = new StringContent("error")
      };

      _mockHttpMessageHandler
          .Protected()
          .Setup<Task<HttpResponseMessage>>(
              "SendAsync",
              ItExpr.IsAny<HttpRequestMessage>(),
              ItExpr.IsAny<CancellationToken>())
          .ReturnsAsync(httpResponse);

      // Act & Assert
      await Assert.ThrowsAsync<GeoIPServiceUnavailableException>(() => _ipBaseGateway.GetGeoIPDataAsync(ipAddress));
    }

    [Fact]
    public async Task GetGeoIPDataAsync_With503StatusCode_ThrowsGeoIPServiceUnavailableException()
    {
      // Arrange
      var ipAddress = "192.168.1.1";
      var apiKey = "test-api-key";

      _mockConfiguration
          .Setup(x => x["IPBase:ApiKey"])
          .Returns(apiKey);

      var httpResponse = new HttpResponseMessage
      {
        StatusCode = System.Net.HttpStatusCode.ServiceUnavailable,
        Content = new StringContent("error")
      };

      _mockHttpMessageHandler
          .Protected()
          .Setup<Task<HttpResponseMessage>>(
              "SendAsync",
              ItExpr.IsAny<HttpRequestMessage>(),
              ItExpr.IsAny<CancellationToken>())
          .ReturnsAsync(httpResponse);

      // Act & Assert
      await Assert.ThrowsAsync<GeoIPServiceUnavailableException>(() => _ipBaseGateway.GetGeoIPDataAsync(ipAddress));
    }

    [Fact]
    public async Task GetGeoIPDataAsync_WithUnexpectedStatusCode_ThrowsDomainException()
    {
      // Arrange
      var ipAddress = "192.168.1.1";
      var apiKey = "test-api-key";

      _mockConfiguration
          .Setup(x => x["IPBase:ApiKey"])
          .Returns(apiKey);

      var httpResponse = new HttpResponseMessage
      {
        StatusCode = System.Net.HttpStatusCode.NotFound,
        Content = new StringContent("error")
      };

      _mockHttpMessageHandler
          .Protected()
          .Setup<Task<HttpResponseMessage>>(
              "SendAsync",
              ItExpr.IsAny<HttpRequestMessage>(),
              ItExpr.IsAny<CancellationToken>())
          .ReturnsAsync(httpResponse);

      // Act & Assert
      var exception = await Assert.ThrowsAsync<DomainException>(() => _ipBaseGateway.GetGeoIPDataAsync(ipAddress));
      Assert.Contains("IPBase API error", exception.Message);
    }

    [Fact]
    public async Task GetGeoIPDataAsync_WithGenericException_ThrowsOriginalException()
    {
      // Arrange
      var ipAddress = "192.168.1.1";
      var apiKey = "test-api-key";
      var expectedException = new InvalidOperationException("Generic error");

      _mockConfiguration
          .Setup(x => x["IPBase:ApiKey"])
          .Returns(apiKey);

      _mockHttpMessageHandler
          .Protected()
          .Setup<Task<HttpResponseMessage>>(
              "SendAsync",
              ItExpr.IsAny<HttpRequestMessage>(),
              ItExpr.IsAny<CancellationToken>())
          .ThrowsAsync(expectedException);

      // Act & Assert
      var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _ipBaseGateway.GetGeoIPDataAsync(ipAddress));
      Assert.Same(expectedException, exception);
    }
  }
}