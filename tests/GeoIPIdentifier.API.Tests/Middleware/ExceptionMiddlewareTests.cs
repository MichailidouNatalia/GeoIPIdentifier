using System.Net;
using System.Text.Json;
using GeoIPIdentifier.API.Middleware;
using GeoIPIdentifier.Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;

namespace GeoIPIdentifier.API.Tests.Middleware
{
  public class ExceptionMiddlewareTests
  {
    private readonly Mock<RequestDelegate> _mockNext;
    private readonly Mock<ILogger<ExceptionMiddleware>> _mockLogger;
    private readonly Mock<IHostEnvironment> _mockEnvironment;
    private readonly DefaultHttpContext _httpContext;

    public ExceptionMiddlewareTests()
    {
      _mockNext = new Mock<RequestDelegate>();
      _mockLogger = new Mock<ILogger<ExceptionMiddleware>>();
      _mockEnvironment = new Mock<IHostEnvironment>();
      _httpContext = new DefaultHttpContext();
    }

    [Fact]
    public async Task InvokeAsync_NoException_CallsNextDelegate()
    {
      // Arrange
      var middleware = new ExceptionMiddleware(
          _mockNext.Object,
          _mockLogger.Object,
          _mockEnvironment.Object);

      _mockNext.Setup(x => x(It.IsAny<HttpContext>())).Returns(Task.CompletedTask);

      // Act
      await middleware.InvokeAsync(_httpContext);

      // Assert
      _mockNext.Verify(x => x(_httpContext), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_WithException_LogsErrorAndHandlesException()
    {
      // Arrange
      var exception = new Exception("Test exception");
      _mockNext.Setup(x => x(It.IsAny<HttpContext>())).ThrowsAsync(exception);

      // Create a production environment middleware
      _mockEnvironment.Setup(x => x.EnvironmentName).Returns("Production");
      var middleware = new ExceptionMiddleware(
          _mockNext.Object,
          _mockLogger.Object,
          _mockEnvironment.Object);

      // Act
      await middleware.InvokeAsync(_httpContext);

      // Assert
      _mockLogger.Verify(
          x => x.Log(
              LogLevel.Error,
              It.IsAny<EventId>(),
              It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("An unhandled exception has occurred")),
              exception,
              It.IsAny<Func<It.IsAnyType, Exception, string>>()),
          Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_DomainException_ReturnsBadRequest()
    {
      // Arrange
      var domainException = new DomainException("Domain validation failed");
      _mockNext.Setup(x => x(It.IsAny<HttpContext>())).ThrowsAsync(domainException);

      _mockEnvironment.Setup(x => x.EnvironmentName).Returns("Production");
      var middleware = new ExceptionMiddleware(
          _mockNext.Object,
          _mockLogger.Object,
          _mockEnvironment.Object);

      // Act
      await middleware.InvokeAsync(_httpContext);

      // Assert
      Assert.Equal((int)HttpStatusCode.BadRequest, _httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_KeyNotFoundException_ReturnsNotFound()
    {
      // Arrange
      var keyNotFoundException = new KeyNotFoundException("Resource not found");
      _mockNext.Setup(x => x(It.IsAny<HttpContext>())).ThrowsAsync(keyNotFoundException);

      _mockEnvironment.Setup(x => x.EnvironmentName).Returns("Production");
      var middleware = new ExceptionMiddleware(
          _mockNext.Object,
          _mockLogger.Object,
          _mockEnvironment.Object);

      // Act
      await middleware.InvokeAsync(_httpContext);

      // Assert
      Assert.Equal((int)HttpStatusCode.NotFound, _httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_UnauthorizedAccessException_ReturnsUnauthorized()
    {
      // Arrange
      var unauthorizedException = new UnauthorizedAccessException("Access denied");
      _mockNext.Setup(x => x(It.IsAny<HttpContext>())).ThrowsAsync(unauthorizedException);

      _mockEnvironment.Setup(x => x.EnvironmentName).Returns("Production");
      var middleware = new ExceptionMiddleware(
          _mockNext.Object,
          _mockLogger.Object,
          _mockEnvironment.Object);

      // Act
      await middleware.InvokeAsync(_httpContext);

      // Assert
      Assert.Equal((int)HttpStatusCode.Unauthorized, _httpContext.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_GenericException_ReturnsInternalServerError()
    {
      // Arrange
      var genericException = new Exception("Something went wrong");
      _mockNext.Setup(x => x(It.IsAny<HttpContext>())).ThrowsAsync(genericException);

      _mockEnvironment.Setup(x => x.EnvironmentName).Returns("Production");
      var middleware = new ExceptionMiddleware(
          _mockNext.Object,
          _mockLogger.Object,
          _mockEnvironment.Object);

      // Act
      await middleware.InvokeAsync(_httpContext);

      // Assert
      Assert.Equal((int)HttpStatusCode.InternalServerError, _httpContext.Response.StatusCode);
    }
    [Fact]
    public async Task InvokeAsync_DevelopmentEnvironment_IncludesStackTrace()
    {
      // Arrange
      var exception = new Exception("Test exception");
      _mockNext.Setup(x => x(It.IsAny<HttpContext>())).ThrowsAsync(exception);

      // Create a development environment middleware
      _mockEnvironment.Setup(x => x.EnvironmentName).Returns("Development");

      // Create a new HttpContext with a proper response body stream
      var context = new DefaultHttpContext();
      context.Response.Body = new MemoryStream();

      var middleware = new ExceptionMiddleware(
          _mockNext.Object,
          _mockLogger.Object,
          _mockEnvironment.Object);

      // Act
      await middleware.InvokeAsync(context);

      // Assert
      context.Response.Body.Seek(0, SeekOrigin.Begin);
      var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();

      Assert.False(string.IsNullOrEmpty(responseBody));

      var response = JsonSerializer.Deserialize<ApiExceptionResponse>(responseBody, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

      Assert.NotNull(response);
      Assert.Equal("Internal Server Error", response.Title);
      Assert.Equal((int)HttpStatusCode.InternalServerError, response.Status);
      Assert.Equal(exception.Message, response.Detail);
      Assert.NotNull(response.StackTrace);
      Assert.False(string.IsNullOrEmpty(response.ReferenceId));
    }

    [Fact]
    public async Task InvokeAsync_ProductionEnvironment_HidesStackTrace()
    {
      // Arrange
      var exception = new Exception("Test exception");
      _mockNext.Setup(x => x(It.IsAny<HttpContext>())).ThrowsAsync(exception);

      _mockEnvironment.Setup(x => x.EnvironmentName).Returns("Production");

      // Create a new HttpContext with a proper response body stream
      var context = new DefaultHttpContext();
      context.Response.Body = new MemoryStream(); // Ensure we have a writable stream

      var middleware = new ExceptionMiddleware(
          _mockNext.Object,
          _mockLogger.Object,
          _mockEnvironment.Object);

      // Act
      await middleware.InvokeAsync(context);

      // Assert
      context.Response.Body.Seek(0, SeekOrigin.Begin);
      var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();

      // Debug output to see what's actually in the response
      Console.WriteLine($"Response Body: {responseBody}");

      Assert.False(string.IsNullOrEmpty(responseBody));

      var response = JsonSerializer.Deserialize<ApiExceptionResponse>(responseBody, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

      Assert.NotNull(response);
      Assert.Equal("Internal Server Error", response.Title);
      Assert.Equal((int)HttpStatusCode.InternalServerError, response.Status);
      Assert.Equal("An internal server error occurred", response.Detail);
      Assert.Null(response.StackTrace);
      Assert.False(string.IsNullOrEmpty(response.ReferenceId));
    }

    [Fact]
    public async Task InvokeAsync_SetsCorrectContentType()
    {
      // Arrange
      var exception = new Exception("Test exception");
      _mockNext.Setup(x => x(It.IsAny<HttpContext>())).ThrowsAsync(exception);

      _mockEnvironment.Setup(x => x.EnvironmentName).Returns("Production");
      var middleware = new ExceptionMiddleware(
          _mockNext.Object,
          _mockLogger.Object,
          _mockEnvironment.Object);

      // Act
      await middleware.InvokeAsync(_httpContext);

      // Assert
      Assert.Equal("application/json", _httpContext.Response.ContentType);
    }
    [Fact]
    public async Task InvokeAsync_DomainExceptionInDevelopment_ReturnsCorrectResponse()
    {
      // Arrange
      var domainException = new DomainException("Invalid IP address");
      _mockNext.Setup(x => x(It.IsAny<HttpContext>())).ThrowsAsync(domainException);

      _mockEnvironment.Setup(x => x.EnvironmentName).Returns("Development");

      // Create a new HttpContext with a proper response body stream
      var context = new DefaultHttpContext();
      context.Response.Body = new MemoryStream();

      var middleware = new ExceptionMiddleware(
          _mockNext.Object,
          _mockLogger.Object,
          _mockEnvironment.Object);

      // Act
      await middleware.InvokeAsync(context);

      // Assert
      context.Response.Body.Seek(0, SeekOrigin.Begin);
      var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();

      Assert.False(string.IsNullOrEmpty(responseBody));

      var response = JsonSerializer.Deserialize<ApiExceptionResponse>(responseBody, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

      Assert.NotNull(response);
      Assert.Equal("Bad Request", response.Title);
      Assert.Equal((int)HttpStatusCode.BadRequest, response.Status);
      Assert.Equal(domainException.Message, response.Detail);
      Assert.NotNull(response.StackTrace);
    }

    [Fact]
    public async Task InvokeAsync_ResponseIsProperlySerialized()
    {
      // Arrange
      var exception = new Exception("Test exception");
      _mockNext.Setup(x => x(It.IsAny<HttpContext>())).ThrowsAsync(exception);

      _mockEnvironment.Setup(x => x.EnvironmentName).Returns("Production");

      // Create a new HttpContext with a proper response body stream
      var context = new DefaultHttpContext();
      context.Response.Body = new MemoryStream();

      var middleware = new ExceptionMiddleware(
          _mockNext.Object,
          _mockLogger.Object,
          _mockEnvironment.Object);

      // Act
      await middleware.InvokeAsync(context);

      // Assert
      context.Response.Body.Seek(0, SeekOrigin.Begin);
      var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();

      // Verify it's valid JSON
      Assert.False(string.IsNullOrEmpty(responseBody));

      var response = JsonSerializer.Deserialize<ApiExceptionResponse>(responseBody, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

      Assert.NotNull(response);
      Assert.Equal("Internal Server Error", response.Title);
      Assert.Equal((int)HttpStatusCode.InternalServerError, response.Status);
      Assert.Equal("An internal server error occurred", response.Detail);
      Assert.NotNull(response.ReferenceId);
      Assert.True(response.ReferenceId.Length == 8); // First 8 chars of GUID
      Assert.True(response.Timestamp <= DateTime.UtcNow && response.Timestamp > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public void GetReferenceId_ReturnsEightCharacterString()
    {
      // Arrange
      var middleware = new ExceptionMiddleware(
          _mockNext.Object,
          _mockLogger.Object,
          _mockEnvironment.Object);

      // Use reflection to call private method
      var method = typeof(ExceptionMiddleware).GetMethod("GetReferenceId",
          System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

      // Act
      var referenceId = method?.Invoke(middleware, null) as string;

      // Assert
      Assert.NotNull(referenceId);
      Assert.Equal(8, referenceId.Length);
      // The reference ID should be the first 8 characters of a GUID
      Assert.Matches("^[a-f0-9]{8}$", referenceId.ToLower());
    }
    [Fact]
    public async Task InvokeAsync_StagingEnvironment_HidesStackTrace()
    {
      // Arrange
      var exception = new Exception("Test exception");
      _mockNext.Setup(x => x(It.IsAny<HttpContext>())).ThrowsAsync(exception);

      // Test with Staging environment (should behave like Production)
      _mockEnvironment.Setup(x => x.EnvironmentName).Returns("Staging");

      // Create a new HttpContext with a proper response body stream
      var context = new DefaultHttpContext();
      context.Response.Body = new MemoryStream();

      var middleware = new ExceptionMiddleware(
          _mockNext.Object,
          _mockLogger.Object,
          _mockEnvironment.Object);

      // Act
      await middleware.InvokeAsync(context);

      // Assert
      context.Response.Body.Seek(0, SeekOrigin.Begin);
      var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();

      Assert.False(string.IsNullOrEmpty(responseBody));

      var response = JsonSerializer.Deserialize<ApiExceptionResponse>(responseBody, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

      Assert.NotNull(response);
      Assert.Null(response.StackTrace); // Should not include stack trace in staging
      Assert.Equal("An internal server error occurred", response.Detail);
    }
  }
}