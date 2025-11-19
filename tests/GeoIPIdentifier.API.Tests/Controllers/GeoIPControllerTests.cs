using Microsoft.AspNetCore.Mvc;
using GeoIPIdentifier.Application.DTOs;
using GeoIPIdentifier.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using GeoIPIdentifier.API.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Http;

namespace GeoIPIdentifier.API.Tests.Controllers
{
  public class GeoIPControllerTests
  {
    private readonly Mock<IGeoIPService> _mockGeoIPService;
    private readonly Mock<ILogger<GeoIPController>> _mockLogger;
    private readonly GeoIPController _controller;

    public GeoIPControllerTests()
    {
      _mockGeoIPService = new Mock<IGeoIPService>();
      _mockLogger = new Mock<ILogger<GeoIPController>>();
      _controller = new GeoIPController(_mockGeoIPService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task IdentifyIP_WithValidIP_ReturnsOkResult()
    {
      // Arrange
      var ipAddress = "192.168.1.1";
      var expectedResponse = new GeoIPResponseDto(
          Guid.NewGuid(),
          ipAddress,
          "US",
          "United States",
          37.7510m,
          -97.8220m,
          "America/Chicago");

      _mockGeoIPService
          .Setup(x => x.IdentifyIPAsync(ipAddress))
          .ReturnsAsync(expectedResponse);

      // Act
      var result = await _controller.IdentifyIP(ipAddress);

      // Assert
      var okResult = Assert.IsType<OkObjectResult>(result.Result);
      var returnValue = Assert.IsType<GeoIPResponseDto>(okResult.Value);
      Assert.Same(expectedResponse, returnValue);
      _mockGeoIPService.Verify(x => x.IdentifyIPAsync(ipAddress), Times.Once);
    }

    [Fact]
    public async Task BatchGeolocate_WithValidRequest_ReturnsSuccessWithBatchResponse()
    {
      // Arrange
      var request = new BatchGeoIPRequestDto(new List<string> { "192.168.1.1", "10.0.0.1" });
      var batchId = "test-batch-id";
      var expectedProgressUrl = "https://localhost/api/batch/test-batch-id";

      _mockGeoIPService
          .Setup(x => x.StartBatchProcessingAsync(request.IPAddresses))
          .ReturnsAsync(batchId);

      var mockUrlHelper = new Mock<IUrlHelper>();
      mockUrlHelper
          .Setup(x => x.RouteUrl(It.IsAny<UrlRouteContext>()))
          .Returns(expectedProgressUrl);
      _controller.Url = mockUrlHelper.Object;
      _controller.ControllerContext = new ControllerContext
      {
        HttpContext = new DefaultHttpContext()
      };

      // Act
      var result = await _controller.BatchGeolocate(request);

      // Assert
      Assert.NotNull(result.Result);
      Assert.Equal(200, (result.Result as ObjectResult)?.StatusCode);

      var returnValue = Assert.IsType<BatchGeoIPResponseDto>((result.Result as ObjectResult)?.Value);
      Assert.Equal(batchId, returnValue.BatchId);
      Assert.Equal(expectedProgressUrl, returnValue.ProgressUrl);
      _mockGeoIPService.Verify(x => x.StartBatchProcessingAsync(request.IPAddresses), Times.Once);
    }

    [Fact]
    public async Task BatchGeolocate_WithNullIPAddresses_ReturnsBadRequest()
    {
      // Arrange
      var request = new BatchGeoIPRequestDto(null);

      // Act
      var result = await _controller.BatchGeolocate(request);

      // Assert
      var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
      Assert.Equal("At least one IP address is required.", badRequestResult.Value);
      _mockGeoIPService.Verify(x => x.StartBatchProcessingAsync(It.IsAny<List<string>>()), Times.Never);
    }

    [Fact]
    public async Task BatchGeolocate_WithEmptyIPAddresses_ReturnsBadRequest()
    {
      // Arrange
      var request = new BatchGeoIPRequestDto(new List<string>());

      // Act
      var result = await _controller.BatchGeolocate(request);

      // Assert
      var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
      Assert.Equal("At least one IP address is required.", badRequestResult.Value);
      _mockGeoIPService.Verify(x => x.StartBatchProcessingAsync(It.IsAny<List<string>>()), Times.Never);
    }

    [Fact]
    public async Task BatchGeolocate_WithTooManyIPAddresses_ReturnsBadRequest()
    {
      // Arrange
      var ipAddresses = Enumerable.Range(1, 1001).Select(i => $"192.168.1.{i}").ToList();
      var request = new BatchGeoIPRequestDto(ipAddresses);

      // Act
      var result = await _controller.BatchGeolocate(request);

      // Assert
      var badRequestResult = Assert.IsType<BadRequestObjectResult>(result.Result);
      Assert.Equal("Maximum 1000 IP addresses allowed per batch.", badRequestResult.Value);
      _mockGeoIPService.Verify(x => x.StartBatchProcessingAsync(It.IsAny<List<string>>()), Times.Never);
    }

    [Fact]
    public async Task BatchGeolocate_WhenServiceThrowsException_ReturnsInternalServerError()
    {
      // Arrange
      var request = new BatchGeoIPRequestDto(new List<string> { "192.168.1.1" });
      var exception = new Exception("Batch processing failed");

      _mockGeoIPService
          .Setup(x => x.StartBatchProcessingAsync(request.IPAddresses))
          .ThrowsAsync(exception);

      // Act
      var result = await _controller.BatchGeolocate(request);

      // Assert
      var statusCodeResult = Assert.IsType<ObjectResult>(result.Result);
      Assert.Equal(500, statusCodeResult.StatusCode);
      Assert.Equal("An error occurred while starting batch processing.", statusCodeResult.Value);
      _mockLogger.Verify(
          x => x.Log(
              LogLevel.Error,
              It.IsAny<EventId>(),
              It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("Error starting batch processing")),
              exception,
              It.IsAny<Func<It.IsAnyType, Exception, string>>()),
          Times.Once);
    }

    [Fact]
    public async Task GetProgress_WithExistingBatch_ReturnsOkResult()
    {
      // Arrange
      var batchId = "test-batch-id";
      var expectedProgress = new BatchProgressResponse(
          batchId,
          "Processing",
          10,
          5,
          DateTime.UtcNow,
          null)
      {
        EstimatedTimeRemaining = TimeSpan.FromMinutes(5)
      };

      _mockGeoIPService
          .Setup(x => x.GetBatchProgressAsync(batchId))
          .ReturnsAsync(expectedProgress);

      // Act
      var result = await _controller.GetProgress(batchId);

      // Assert
      var okResult = Assert.IsType<OkObjectResult>(result.Result);
      var returnValue = Assert.IsType<BatchProgressResponse>(okResult.Value);
      Assert.Same(expectedProgress, returnValue);
      _mockGeoIPService.Verify(x => x.GetBatchProgressAsync(batchId), Times.Once);
    }

    [Fact]
    public async Task GetProgress_WithNonExistingBatch_ReturnsNotFound()
    {
      // Arrange
      var batchId = "non-existing-batch";

      _mockGeoIPService
          .Setup(x => x.GetBatchProgressAsync(batchId))
          .ReturnsAsync((BatchProgressResponse)null);

      // Act
      var result = await _controller.GetProgress(batchId);

      // Assert
      var notFoundResult = Assert.IsType<NotFoundObjectResult>(result.Result);
      Assert.Equal($"Batch ID {batchId} not found or expired.", notFoundResult.Value);
      _mockGeoIPService.Verify(x => x.GetBatchProgressAsync(batchId), Times.Once);
    }

    [Fact]
    public async Task GetProgress_WhenServiceThrowsException_ReturnsInternalServerError()
    {
      // Arrange
      var batchId = "test-batch-id";
      var exception = new Exception("Progress retrieval failed");

      _mockGeoIPService
          .Setup(x => x.GetBatchProgressAsync(batchId))
          .ThrowsAsync(exception);

      // Act
      var result = await _controller.GetProgress(batchId);

      // Assert
      var statusCodeResult = Assert.IsType<ObjectResult>(result.Result);
      Assert.Equal(500, statusCodeResult.StatusCode);
      Assert.Equal("An error occurred while retrieving progress.", statusCodeResult.Value);
      _mockLogger.Verify(
          x => x.Log(
              LogLevel.Error,
              It.IsAny<EventId>(),
              It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("Error getting progress for batch")),
              exception,
              It.IsAny<Func<It.IsAnyType, Exception, string>>()),
          Times.Once);
    }

    [Fact]
    public void Controller_HasCorrectRouteAttribute()
    {
      // Arrange & Act
      var controller = new GeoIPController(_mockGeoIPService.Object, _mockLogger.Object);

      // Assert
      var routeAttribute = controller.GetType().GetCustomAttributes(typeof(RouteAttribute), true)
          .FirstOrDefault() as RouteAttribute;
      Assert.NotNull(routeAttribute);
      Assert.Equal("api/geoip", routeAttribute.Template);
    }

    [Fact]
    public void Controller_HasApiControllerAttribute()
    {
      // Arrange & Act
      var controller = new GeoIPController(_mockGeoIPService.Object, _mockLogger.Object);

      // Assert
      var apiControllerAttribute = controller.GetType().GetCustomAttributes(typeof(ApiControllerAttribute), true)
          .FirstOrDefault();
      Assert.NotNull(apiControllerAttribute);
    }

    [Fact]
    public void BatchGeolocate_HasCorrectRouteName()
    {
      // Arrange & Act
      var method = typeof(GeoIPController).GetMethod("BatchGeolocate");

      // Assert
      var httpPostAttribute = method.GetCustomAttributes(typeof(HttpPostAttribute), true)
          .FirstOrDefault() as HttpPostAttribute;
      Assert.NotNull(httpPostAttribute);
      Assert.Equal("BatchGeolocate", httpPostAttribute.Name);
    }

    [Fact]
    public void GetProgress_HasCorrectRoute()
    {
      // Arrange & Act
      var method = typeof(GeoIPController).GetMethod("GetProgress");

      // Assert
      var httpGetAttribute = method.GetCustomAttributes(typeof(HttpGetAttribute), true)
          .FirstOrDefault() as HttpGetAttribute;
      Assert.NotNull(httpGetAttribute);
      Assert.Equal("/batch/{id}", httpGetAttribute.Template);
      Assert.Equal("GetProgress", httpGetAttribute.Name);
    }
  }
}