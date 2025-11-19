using AutoMapper;
using GeoIPIdentifier.Application.DTOs;
using GeoIPIdentifier.Application.Interfaces;
using GeoIPIdentifier.Application.Services;
using GeoIPIdentifier.Domain.Entities;
using GeoIPIdentifier.Domain.Exceptions;
using GeoIPIdentifier.Shared.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;

namespace GeoIPIdentifier.Application.Tests.Services
{
    public class GeoIPServiceTests
    {
        private readonly Mock<IGeoIPRepository> _mockRepository;
        private readonly Mock<ICacheService> _mockCacheService;
        private readonly Mock<IMapper> _mockMapper;
        private readonly Mock<IIPBaseClient> _mockExternalService;
        private readonly Mock<IBatchJobScheduler> _mockJobScheduler;
        private readonly Mock<IConnectionMultiplexer> _mockRedis;
        private readonly Mock<ILogger<GeoIPService>> _mockLogger;
        private readonly GeoIPService _geoIPService;

        public GeoIPServiceTests()
        {
            _mockRepository = new Mock<IGeoIPRepository>();
            _mockCacheService = new Mock<ICacheService>();
            _mockMapper = new Mock<IMapper>();
            _mockExternalService = new Mock<IIPBaseClient>();
            _mockJobScheduler = new Mock<IBatchJobScheduler>();
            _mockRedis = new Mock<IConnectionMultiplexer>();
            _mockLogger = new Mock<ILogger<GeoIPService>>();

            _geoIPService = new GeoIPService(
                _mockRepository.Object,
                _mockCacheService.Object,
                _mockMapper.Object,
                _mockExternalService.Object,
                _mockJobScheduler.Object,
                _mockRedis.Object,
                _mockLogger.Object);
        }

        [Fact]
        public async Task IdentifyIPAsync_WithValidIP_ReturnsCachedResult()
        {
            // Arrange
            var ipAddress = "192.168.1.1";
            var cachedResponse = new GeoIPResponseDto(
                Id: Guid.NewGuid(),
                IPAddress: ipAddress,
                CountryCode: "US",
                CountryName: "United States",
                Latitude: 37.7510m,
                Longitude: -97.8220m,
                Timezone: "America/Chicago");
            var cacheKey = $"geoip:{ipAddress}";

            _mockCacheService
                .Setup(x => x.GetAsync<GeoIPResponseDto>(cacheKey))
                .ReturnsAsync(cachedResponse);

            // Act
            var result = await _geoIPService.IdentifyIPAsync(ipAddress);

            // Assert
            Assert.Same(cachedResponse, result);
            _mockCacheService.Verify(x => x.GetAsync<GeoIPResponseDto>(cacheKey), Times.Once);
            _mockRepository.Verify(x => x.GetByIPAsync(It.IsAny<string>()), Times.Never);
            _mockExternalService.Verify(x => x.GetGeoIPDataAsync(It.IsAny<string>()), Times.Never);
            _mockMapper.Verify(x => x.Map<GeoIPResponseDto>(It.IsAny<object>()), Times.Never);
        }

        [Fact]
        public async Task IdentifyIPAsync_WithValidIP_ReturnsDatabaseResultAndCaches()
        {
            // Arrange
            var ipAddress = "192.168.1.1";
            var cacheKey = $"geoip:{ipAddress}";
            var domainEntity = new GeoIPData();
            var responseDto = new GeoIPResponseDto(
                Id: Guid.NewGuid(),
                IPAddress: ipAddress,
                CountryCode: "US",
                CountryName: "United States",
                Latitude: 37.7510m,
                Longitude: -97.8220m,
                Timezone: "America/Chicago");

            _mockCacheService
                .Setup(x => x.GetAsync<GeoIPResponseDto>(cacheKey))
                .ReturnsAsync((GeoIPResponseDto)null);

            _mockRepository
                .Setup(x => x.GetByIPAsync(ipAddress))
                .ReturnsAsync(domainEntity);

            _mockMapper
                .Setup(x => x.Map<GeoIPResponseDto>(domainEntity))
                .Returns(responseDto);

            _mockCacheService
                .Setup(x => x.SetAsync(cacheKey, responseDto, TimeSpan.FromHours(1)))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _geoIPService.IdentifyIPAsync(ipAddress);

            // Assert
            Assert.Same(responseDto, result);
            _mockMapper.Verify(x => x.Map<GeoIPResponseDto>(domainEntity), Times.Once);
            _mockCacheService.Verify(x => x.SetAsync(cacheKey, responseDto, TimeSpan.FromHours(1)), Times.Once);
        }

        [Fact]
        public async Task IdentifyIPAsync_WithValidIP_CallsExternalServiceWhenNotInCacheOrDatabase()
        {
            // Arrange
            var ipAddress = "192.168.1.1";
            var cacheKey = $"geoip:{ipAddress}";
            var externalData = new GeoIPData();
            var responseDto = new GeoIPResponseDto(
                Id: Guid.NewGuid(),
                IPAddress: ipAddress,
                CountryCode: "US",
                CountryName: "United States",
                Latitude: 37.7510m,
                Longitude: -97.8220m,
                Timezone: "America/Chicago");

            _mockCacheService
                .Setup(x => x.GetAsync<GeoIPResponseDto>(cacheKey))
                .ReturnsAsync((GeoIPResponseDto)null);

            _mockRepository
                .Setup(x => x.GetByIPAsync(ipAddress))
                .ReturnsAsync((GeoIPData)null);

            _mockExternalService
                .Setup(x => x.GetGeoIPDataAsync(ipAddress))
                .ReturnsAsync(externalData);

            _mockRepository
                .Setup(x => x.AddAsync(externalData))
                .Returns(Task.CompletedTask);

            _mockMapper
                .Setup(x => x.Map<GeoIPResponseDto>(externalData))
                .Returns(responseDto);

            _mockCacheService
                .Setup(x => x.SetAsync(cacheKey, responseDto, TimeSpan.FromHours(1)))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _geoIPService.IdentifyIPAsync(ipAddress);

            // Assert
            Assert.Same(responseDto, result);
            _mockExternalService.Verify(x => x.GetGeoIPDataAsync(ipAddress), Times.Once);
            _mockRepository.Verify(x => x.AddAsync(externalData), Times.Once);
            _mockMapper.Verify(x => x.Map<GeoIPResponseDto>(externalData), Times.Once);
            _mockCacheService.Verify(x => x.SetAsync(cacheKey, responseDto, TimeSpan.FromHours(1)), Times.Once);
        }

        [Fact]
        public async Task IdentifyIPAsync_WithEmptyIP_ThrowsDomainException()
        {
            // Arrange
            var ipAddress = "";

            // Act & Assert
            await Assert.ThrowsAsync<DomainException>(() => _geoIPService.IdentifyIPAsync(ipAddress));
        }

        [Fact]
        public async Task IdentifyIPAsync_WithNullIP_ThrowsDomainException()
        {
            // Arrange
            string ipAddress = null;

            // Act & Assert
            await Assert.ThrowsAsync<DomainException>(() => _geoIPService.IdentifyIPAsync(ipAddress));
        }

        [Fact]
        public async Task IdentifyIPAsync_WithInvalidIP_ThrowsInvalidIPAddressException()
        {
            // Arrange
            var ipAddress = "invalid-ip";

            // Act & Assert
            await Assert.ThrowsAsync<InvalidIPAddressException>(() => _geoIPService.IdentifyIPAsync(ipAddress));
        }

        [Fact]
        public async Task IdentifyIPAsync_WhenExternalServiceThrowsHttpRequestException_ThrowsGeoIPServiceUnavailableException()
        {
            // Arrange
            var ipAddress = "192.168.1.1";

            _mockCacheService
                .Setup(x => x.GetAsync<GeoIPResponseDto>(It.IsAny<string>()))
                .ReturnsAsync((GeoIPResponseDto)null);

            _mockRepository
                .Setup(x => x.GetByIPAsync(ipAddress))
                .ReturnsAsync((GeoIPData)null);

            _mockExternalService
                .Setup(x => x.GetGeoIPDataAsync(ipAddress))
                .ThrowsAsync(new HttpRequestException());

            // Act & Assert
            await Assert.ThrowsAsync<GeoIPServiceUnavailableException>(() => _geoIPService.IdentifyIPAsync(ipAddress));
        }

        [Fact]
        public async Task IdentifyIPAsync_WhenExternalServiceThrowsRateLimitException_ThrowsRateLimitExceededException()
        {
            // Arrange
            var ipAddress = "192.168.1.1";
            var rateLimitException = new Exception("rate limit exceeded");

            _mockCacheService
                .Setup(x => x.GetAsync<GeoIPResponseDto>(It.IsAny<string>()))
                .ReturnsAsync((GeoIPResponseDto)null);

            _mockRepository
                .Setup(x => x.GetByIPAsync(ipAddress))
                .ReturnsAsync((GeoIPData)null);

            _mockExternalService
                .Setup(x => x.GetGeoIPDataAsync(ipAddress))
                .ThrowsAsync(rateLimitException);

            // Act & Assert
            await Assert.ThrowsAsync<RateLimitExceededException>(() => _geoIPService.IdentifyIPAsync(ipAddress));
        }

        [Fact]
        public async Task StartBatchProcessingAsync_WithValidIPs_ReturnsBatchId()
        {
            // Arrange
            var ipAddresses = new List<string> { "192.168.1.1", "10.0.0.1" };
            var expectedBatchId = "test-batch-id";
            var mockDb = new Mock<IDatabase>();

            _mockJobScheduler
                .Setup(x => x.ScheduleBatchJobAsync(ipAddresses))
                .ReturnsAsync(expectedBatchId);

            _mockRedis
                .Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(mockDb.Object);

            mockDb.Setup(x => x.HashSetAsync(It.IsAny<RedisKey>(), It.IsAny<HashEntry[]>(), It.IsAny<CommandFlags>()))
                  .Returns(Task.CompletedTask);

            mockDb.Setup(x => x.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan>(), It.IsAny<CommandFlags>()))
                  .ReturnsAsync(true);

            // Act
            var result = await _geoIPService.StartBatchProcessingAsync(ipAddresses);

            // Assert
            Assert.Equal(expectedBatchId, result);
            _mockJobScheduler.Verify(x => x.ScheduleBatchJobAsync(ipAddresses), Times.Once);
        }

        [Fact]
        public async Task StartBatchProcessingAsync_WithInvalidIPs_FiltersOutInvalidIPs()
        {
            // Arrange
            var ipAddresses = new List<string> { "192.168.1.1", "invalid-ip", "10.0.0.1" };
            var validIps = new List<string> { "192.168.1.1", "10.0.0.1" };
            var expectedBatchId = "test-batch-id";
            var mockDb = new Mock<IDatabase>();

            _mockJobScheduler
                .Setup(x => x.ScheduleBatchJobAsync(validIps))
                .ReturnsAsync(expectedBatchId);

            _mockRedis
                .Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(mockDb.Object);

            mockDb.Setup(x => x.HashSetAsync(It.IsAny<RedisKey>(), It.IsAny<HashEntry[]>(), It.IsAny<CommandFlags>()))
                  .Returns(Task.CompletedTask);

            mockDb.Setup(x => x.KeyExpireAsync(It.IsAny<RedisKey>(), It.IsAny<TimeSpan>(), It.IsAny<CommandFlags>()))
                  .ReturnsAsync(true);

            // Act
            var result = await _geoIPService.StartBatchProcessingAsync(ipAddresses);

            // Assert
            Assert.Equal(expectedBatchId, result);
            _mockJobScheduler.Verify(x => x.ScheduleBatchJobAsync(validIps), Times.Once);
        }

        [Fact]
        public async Task StartBatchProcessingAsync_WithNoValidIPs_ThrowsArgumentException()
        {
            // Arrange
            var ipAddresses = new List<string> { "invalid-ip-1", "invalid-ip-2" };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _geoIPService.StartBatchProcessingAsync(ipAddresses));
        }

        [Fact]
        public async Task ProcessBatchAsync_WithValidBatch_CallsIdentifyIPForEachIP()
        {
            // Arrange
            var batchId = "test-batch";
            var ipAddresses = new List<string> { "192.168.1.1", "10.0.0.1" };
            var mockDb = new Mock<IDatabase>();

            _mockRedis
                .Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(mockDb.Object);

            mockDb.Setup(x => x.HashSetAsync(It.IsAny<RedisKey>(), It.IsAny<HashEntry[]>(), It.IsAny<CommandFlags>()))
                  .Returns(Task.CompletedTask);

            mockDb.Setup(x => x.StringIncrementAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
                  .ReturnsAsync(1);

            // Mock the IdentifyIPAsync calls to use AutoMapper
            _mockCacheService
                .Setup(x => x.GetAsync<GeoIPResponseDto>(It.IsAny<string>()))
                .ReturnsAsync((GeoIPResponseDto)null);

            _mockRepository
                .Setup(x => x.GetByIPAsync(It.IsAny<string>()))
                .ReturnsAsync((GeoIPData)null);

            _mockExternalService
                .Setup(x => x.GetGeoIPDataAsync(It.IsAny<string>()))
                .ReturnsAsync(new GeoIPData());

            _mockMapper
                .Setup(x => x.Map<GeoIPResponseDto>(It.IsAny<GeoIPData>()))
                .Returns(new GeoIPResponseDto(
                    Guid.NewGuid(),
                    "192.168.1.1",
                    "US",
                    "United States",
                    37.7510m,
                    -97.8220m,
                    "America/Chicago"));

            // Act
            await _geoIPService.ProcessBatchAsync(batchId, ipAddresses);

            // Assert
            // Verify that AutoMapper was called for each IP address
            _mockMapper.Verify(x => x.Map<GeoIPResponseDto>(It.IsAny<GeoIPData>()), Times.Exactly(2));
        }

        [Fact]
        public async Task GetBatchProgressAsync_WithExistingBatch_ReturnsProgressResponse()
        {
            // Arrange
            var batchId = "test-batch";
            var mockDb = new Mock<IDatabase>();
            var hashEntries = new[]
            {
                new HashEntry("batchId", batchId),
                new HashEntry("status", "Processing"),
                new HashEntry("total", 10),
                new HashEntry("processed", 5),
                new HashEntry("startTime", DateTime.UtcNow.ToString("O"))
            };

            _mockRedis
                .Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(mockDb.Object);

            mockDb.Setup(x => x.HashGetAllAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                  .ReturnsAsync(hashEntries);

            // Act
            var result = await _geoIPService.GetBatchProgressAsync(batchId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(batchId, result.BatchId);
            Assert.Equal("Processing", result.Status);
            Assert.Equal(10, result.Total);
            Assert.Equal(5, result.Processed);
        }

        [Fact]
        public async Task GetBatchProgressAsync_WithNonExistingBatch_ReturnsNull()
        {
            // Arrange
            var batchId = "non-existing-batch";
            var mockDb = new Mock<IDatabase>();
            var emptyHashEntries = new HashEntry[0];

            _mockRedis
                .Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(mockDb.Object);

            mockDb.Setup(x => x.HashGetAllAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                  .ReturnsAsync(emptyHashEntries);

            // Act
            var result = await _geoIPService.GetBatchProgressAsync(batchId);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetBatchProgressAsync_WithCompletedBatch_IncludesCompletedTime()
        {
            // Arrange
            var batchId = "completed-batch";
            var mockDb = new Mock<IDatabase>();
            var completedTime = DateTime.UtcNow;
            var hashEntries = new[]
            {
                new HashEntry("batchId", batchId),
                new HashEntry("status", "Completed"),
                new HashEntry("total", 10),
                new HashEntry("processed", 10),
                new HashEntry("startTime", DateTime.UtcNow.AddMinutes(-5).ToString("O")),
                new HashEntry("completedTime", completedTime.ToString("O"))
            };

            _mockRedis
                .Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(mockDb.Object);

            mockDb.Setup(x => x.HashGetAllAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                  .ReturnsAsync(hashEntries);

            // Act
            var result = await _geoIPService.GetBatchProgressAsync(batchId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Completed", result.Status);
            Assert.NotNull(result.CompletedTime);
        }

        [Fact]
        public async Task IdentifyIPAsync_MapperConfiguration_ShouldMapAllPropertiesCorrectly()
        {
            // Arrange
            var ipAddress = "192.168.1.1";
            var domainEntity = new GeoIPData
            {
                IPAddress = ipAddress,
                CountryCode = "US",
                CountryName = "United States",
                Latitude = 37.7510m,
                Longitude = -97.8220m,
                Timezone = "America/Chicago"
            };

            var expectedDto = new GeoIPResponseDto(
                domainEntity.Id,
                domainEntity.IPAddress,
                domainEntity.CountryCode,
                domainEntity.CountryName,
                domainEntity.Latitude,
                domainEntity.Longitude,
                domainEntity.Timezone);

            _mockCacheService
                .Setup(x => x.GetAsync<GeoIPResponseDto>(It.IsAny<string>()))
                .ReturnsAsync((GeoIPResponseDto)null);

            _mockRepository
                .Setup(x => x.GetByIPAsync(ipAddress))
                .ReturnsAsync(domainEntity);

            _mockMapper
                .Setup(x => x.Map<GeoIPResponseDto>(domainEntity))
                .Returns(expectedDto);

            // Act
            var result = await _geoIPService.IdentifyIPAsync(ipAddress);

            // Assert
            Assert.Equal(expectedDto.Id, result.Id);
            Assert.Equal(expectedDto.IPAddress, result.IPAddress);
            Assert.Equal(expectedDto.CountryCode, result.CountryCode);
            Assert.Equal(expectedDto.CountryName, result.CountryName);
            Assert.Equal(expectedDto.Latitude, result.Latitude);
            Assert.Equal(expectedDto.Longitude, result.Longitude);
            Assert.Equal(expectedDto.Timezone, result.Timezone);
            
            _mockMapper.Verify(x => x.Map<GeoIPResponseDto>(domainEntity), Times.Once);
        }

        [Fact]
        public void IsValidIpAddress_WithValidIPs_ReturnsTrue()
        {
            // Arrange
            var validIps = new[] { "192.168.1.1", "10.0.0.1", "::1", "2001:0db8:85a3:0000:0000:8a2e:0370:7334" };

            foreach (var ip in validIps)
            {
                // Act
                var result = _geoIPService.GetType().GetMethod("IsValidIpAddress", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .Invoke(_geoIPService, new object[] { ip });

                // Assert
                Assert.True((bool)result);
            }
        }

        [Fact]
        public void IsValidIpAddress_WithInvalidIPs_ReturnsFalse()
        {
            // Arrange
            var invalidIps = new[] { "invalid", "192.168.1.256", "10.0.0.", "not-an-ip" };

            foreach (var ip in invalidIps)
            {
                // Act
                var result = _geoIPService.GetType().GetMethod("IsValidIpAddress", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .Invoke(_geoIPService, new object[] { ip });

                // Assert
                Assert.False((bool)result);
            }
        }
    }
}