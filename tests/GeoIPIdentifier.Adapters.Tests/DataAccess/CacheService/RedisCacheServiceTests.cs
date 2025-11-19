using StackExchange.Redis;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using Moq;
using GeoIPIdentifier.Adapters.DataAccess.CacheService;

namespace GeoIPIdentifier.Adapters.Tests.DataAccess.CacheService
{
    public class RedisCacheServiceTests
    {
        private readonly Mock<IConnectionMultiplexer> _mockRedis;
        private readonly Mock<IDatabase> _mockDatabase;
        private readonly Mock<ILogger<RedisCacheService>> _mockLogger;
        private readonly RedisCacheService _cacheService;

        public RedisCacheServiceTests()
        {
            _mockRedis = new Mock<IConnectionMultiplexer>();
            _mockDatabase = new Mock<IDatabase>();
            _mockLogger = new Mock<ILogger<RedisCacheService>>();

            _mockRedis
                .Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
                .Returns(_mockDatabase.Object);

            _cacheService = new RedisCacheService(_mockRedis.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task GetAsync_WithExistingKey_ReturnsDeserializedValue()
        {
            // Arrange
            var key = "test-key";
            var expectedValue = new TestObject { Id = 1, Name = "Test" };
            var serializedValue = JsonConvert.SerializeObject(expectedValue);

            _mockDatabase
                .Setup(x => x.StringGetAsync(key, It.IsAny<CommandFlags>()))
                .ReturnsAsync(serializedValue);

            // Act
            var result = await _cacheService.GetAsync<TestObject>(key);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedValue.Id, result.Id);
            Assert.Equal(expectedValue.Name, result.Name);
            _mockDatabase.Verify(x => x.StringGetAsync(key, It.IsAny<CommandFlags>()), Times.Once);
        }

        [Fact]
        public async Task GetAsync_WithNonExistingKey_ReturnsDefault()
        {
            // Arrange
            var key = "non-existing-key";

            _mockDatabase
                .Setup(x => x.StringGetAsync(key, It.IsAny<CommandFlags>()))
                .ReturnsAsync(RedisValue.Null);

            // Act
            var result = await _cacheService.GetAsync<TestObject>(key);

            // Assert
            Assert.Null(result);
            _mockDatabase.Verify(x => x.StringGetAsync(key, It.IsAny<CommandFlags>()), Times.Once);
        }

        [Fact]
        public async Task GetAsync_WithEmptyKey_ReturnsDefault()
        {
            // Arrange
            var key = "";

            _mockDatabase
                .Setup(x => x.StringGetAsync(key, It.IsAny<CommandFlags>()))
                .ReturnsAsync(RedisValue.Null);

            // Act
            var result = await _cacheService.GetAsync<TestObject>(key);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetAsync_WithPrimitiveType_ReturnsDeserializedValue()
        {
            // Arrange
            var key = "int-key";
            var expectedValue = 42;
            var serializedValue = JsonConvert.SerializeObject(expectedValue);

            _mockDatabase
                .Setup(x => x.StringGetAsync(key, It.IsAny<CommandFlags>()))
                .ReturnsAsync(serializedValue);

            // Act
            var result = await _cacheService.GetAsync<int>(key);

            // Assert
            Assert.Equal(expectedValue, result);
        }

        [Fact]
        public async Task GetAsync_WithStringType_ReturnsDeserializedValue()
        {
            // Arrange
            var key = "string-key";
            var expectedValue = "test string";
            var serializedValue = JsonConvert.SerializeObject(expectedValue);

            _mockDatabase
                .Setup(x => x.StringGetAsync(key, It.IsAny<CommandFlags>()))
                .ReturnsAsync(serializedValue);

            // Act
            var result = await _cacheService.GetAsync<string>(key);

            // Assert
            Assert.Equal(expectedValue, result);
        }

        [Fact]
        public async Task SetAsync_WithValueAndExpiration_SetsValueWithExpiration()
        {
            // Arrange
            var key = "test-key";
            var value = new TestObject { Id = 1, Name = "Test" };
            var expiration = TimeSpan.FromMinutes(30);

            _mockDatabase
                .Setup(x => x.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), expiration, It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            // Act
            await _cacheService.SetAsync(key, value, expiration);

            // Assert
            _mockDatabase.Verify(x => x.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                expiration,
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()),
            Times.Once);
        }

        [Fact]
        public async Task SetAsync_WithValueAndNoExpiration_SetsValueWithoutExpiration()
        {
            // Arrange
            var key = "test-key";
            var value = new TestObject { Id = 1, Name = "Test" };

            _mockDatabase
                .Setup(x => x.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            // Act
            await _cacheService.SetAsync(key, value);

            // Assert
            _mockDatabase.Verify(x => x.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                null,
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()),
            Times.Once);
        }

        [Fact]
        public async Task SetAsync_WithPrimitiveType_SetsSerializedValue()
        {
            // Arrange
            var key = "int-key";
            var value = 123;

            _mockDatabase
                .Setup(x => x.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            // Act
            await _cacheService.SetAsync(key, value);

            // Assert
            _mockDatabase.Verify(x => x.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()),
            Times.Once);
        }

        [Fact]
        public async Task SetAsync_WithNullValue_SetsNullValue()
        {
            // Arrange
            var key = "null-key";

            _mockDatabase
                .Setup(x => x.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            // Act
            await _cacheService.SetAsync<TestObject>(key, null);

            // Assert
            _mockDatabase.Verify(x => x.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()),
            Times.Once);
        }

        [Fact]
        public async Task RemoveAsync_WithExistingKey_DeletesKey()
        {
            // Arrange
            var key = "test-key";

            _mockDatabase
                .Setup(x => x.KeyDeleteAsync(key, It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            // Act
            await _cacheService.RemoveAsync(key);

            // Assert
            _mockDatabase.Verify(x => x.KeyDeleteAsync(key, It.IsAny<CommandFlags>()), Times.Once);
        }

        [Fact]
        public async Task RemoveAsync_WithNonExistingKey_DeletesKey()
        {
            // Arrange
            var key = "non-existing-key";

            _mockDatabase
                .Setup(x => x.KeyDeleteAsync(key, It.IsAny<CommandFlags>()))
                .ReturnsAsync(false);

            // Act
            await _cacheService.RemoveAsync(key);

            // Assert
            _mockDatabase.Verify(x => x.KeyDeleteAsync(key, It.IsAny<CommandFlags>()), Times.Once);
        }

        [Fact]
        public async Task RemoveAsync_WithEmptyKey_DeletesKey()
        {
            // Arrange
            var key = "";

            _mockDatabase
                .Setup(x => x.KeyDeleteAsync(key, It.IsAny<CommandFlags>()))
                .ReturnsAsync(false);

            // Act
            await _cacheService.RemoveAsync(key);

            // Assert
            _mockDatabase.Verify(x => x.KeyDeleteAsync(key, It.IsAny<CommandFlags>()), Times.Once);
        }

        [Fact]
        public void Constructor_InitializesDatabase()
        {
            // Arrange & Act (done in constructor)
            // Assert
            _mockRedis.Verify(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()), Times.Once);
        }

        [Fact]
        public async Task GetAsync_LogsDebugWhenKeyFound()
        {
            // Arrange
            var key = "test-key";
            var value = new TestObject { Id = 1, Name = "Test" };
            var serializedValue = JsonConvert.SerializeObject(value);

            _mockDatabase
                .Setup(x => x.StringGetAsync(key, It.IsAny<CommandFlags>()))
                .ReturnsAsync(serializedValue);

            // Act
            await _cacheService.GetAsync<TestObject>(key);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("Cache key found") && o.ToString().Contains(key)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task SetAsync_LogsInformationWithKeyAndExpiration()
        {
            // Arrange
            var key = "test-key";
            var value = new TestObject { Id = 1, Name = "Test" };
            var expiration = TimeSpan.FromMinutes(30);

            _mockDatabase
                .Setup(x => x.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), expiration, It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            // Act
            await _cacheService.SetAsync(key, value, expiration);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("Setting cache key") && o.ToString().Contains(key) && o.ToString().Contains(expiration.ToString())),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task SetAsync_WithNullExpiration_LogsInformationWithoutExpiration()
        {
            // Arrange
            var key = "test-key";
            var value = new TestObject { Id = 1, Name = "Test" };

            _mockDatabase
                .Setup(x => x.StringSetAsync(It.IsAny<RedisKey>(), It.IsAny<RedisValue>(), It.IsAny<TimeSpan?>(), It.IsAny<bool>(), It.IsAny<When>(), It.IsAny<CommandFlags>()))
                .ReturnsAsync(true);

            // Act
            await _cacheService.SetAsync(key, value);

            // Assert
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((o, t) => o.ToString().Contains("Setting cache key") && o.ToString().Contains(key)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        // Helper class for testing
        private class TestObject
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }
    }
}