using Microsoft.EntityFrameworkCore;
using GeoIPIdentifier.Domain.Entities;
using GeoIPIdentifier.Adapters.DataAccess;
using GeoIPIdentifier.Adapters.DataAccess.Repositories;

namespace GeoIPIdentifier.Adapters.Tests.DataAccess.Repositories
{
    public class GeoIPRepositoryTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly GeoIPRepository _repository;

        public GeoIPRepositoryTests()
        {
            // Setup in-memory database
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);
            _repository = new GeoIPRepository(_context);
        }

        public void Dispose()
        {
            _context?.Dispose();
        }

        private GeoIPData CreateTestGeoIPData(string ipAddress = "192.168.1.1", string countryCode = "US")
        {
            return new GeoIPData
            {
                Id = Guid.NewGuid(),
                IPAddress = ipAddress,
                CountryCode = countryCode,
                CountryName = countryCode == "US" ? "United States" : "Canada",
                Latitude = 37.7510m,
                Longitude = -97.8220m,
                Timezone = "America/Chicago", // Required property
                CreatedAt = DateTime.UtcNow
            };
        }

        [Fact]
        public async Task GetByIPAsync_WithExistingIP_ReturnsLatestGeoIPData()
        {
            // Arrange
            var ipAddress = "192.168.1.1";
            var olderData = CreateTestGeoIPData(ipAddress);
            olderData.CreatedAt = DateTime.UtcNow.AddHours(-1);
            
            var newerData = CreateTestGeoIPData(ipAddress);
            newerData.CreatedAt = DateTime.UtcNow;

            await _context.GeoIPData.AddRangeAsync(olderData, newerData);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetByIPAsync(ipAddress);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(newerData.Id, result.Id);
            Assert.Equal(ipAddress, result.IPAddress);
        }

        [Fact]
        public async Task GetByIPAsync_WithNonExistingIP_ReturnsNull()
        {
            // Arrange
            var ipAddress = "192.168.1.1";

            // Act
            var result = await _repository.GetByIPAsync(ipAddress);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task AddAsync_WithValidData_AddsToDatabase()
        {
            // Arrange
            var geoIPData = CreateTestGeoIPData();

            // Act
            await _repository.AddAsync(geoIPData);

            // Assert
            var result = await _context.GeoIPData.FindAsync(geoIPData.Id);
            Assert.NotNull(result);
            Assert.Equal(geoIPData.IPAddress, result.IPAddress);
            Assert.Equal(geoIPData.CountryCode, result.CountryCode);
            Assert.Equal(geoIPData.Timezone, result.Timezone); // Verify required property
        }

        [Fact]
        public async Task GetRecentAsync_WithCount_ReturnsLatestRecords()
        {
            // Arrange
            var records = new List<GeoIPData>();
            for (int i = 1; i <= 15; i++)
            {
                var record = CreateTestGeoIPData($"192.168.1.{i}");
                record.CreatedAt = DateTime.UtcNow.AddMinutes(-i);
                records.Add(record);
            }

            await _context.GeoIPData.AddRangeAsync(records);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetRecentAsync(5);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(5, result.Count());
            // Should be ordered by CreatedAt descending (most recent first)
            Assert.True(result.First().CreatedAt > result.Last().CreatedAt);
        }

        [Fact]
        public async Task GetByIdAsync_WithExistingId_ReturnsGeoIPData()
        {
            // Arrange
            var geoIPData = CreateTestGeoIPData();

            await _context.GeoIPData.AddAsync(geoIPData);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetByIdAsync(geoIPData.Id);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(geoIPData.Id, result.Id);
            Assert.Equal(geoIPData.IPAddress, result.IPAddress);
            Assert.Equal(geoIPData.Timezone, result.Timezone);
        }

        [Fact]
        public async Task GetByIdAsync_WithNonExistingId_ReturnsNull()
        {
            // Arrange
            var id = Guid.NewGuid();

            // Act
            var result = await _repository.GetByIdAsync(id);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task UpdateAsync_WithExistingData_UpdatesDatabase()
        {
            // Arrange
            var geoIPData = CreateTestGeoIPData();

            await _context.GeoIPData.AddAsync(geoIPData);
            await _context.SaveChangesAsync();

            // Modify the entity
            geoIPData.CountryCode = "CA";
            geoIPData.CountryName = "Canada";
            geoIPData.Timezone = "America/Toronto"; // Update required property

            // Act
            await _repository.UpdateAsync(geoIPData);

            // Assert
            var updated = await _context.GeoIPData.FindAsync(geoIPData.Id);
            Assert.NotNull(updated);
            Assert.Equal("CA", updated.CountryCode);
            Assert.Equal("Canada", updated.CountryName);
            Assert.Equal("America/Toronto", updated.Timezone);
        }

        [Fact]
        public async Task DeleteAsync_WithExistingId_RemovesFromDatabase()
        {
            // Arrange
            var geoIPData = CreateTestGeoIPData();

            await _context.GeoIPData.AddAsync(geoIPData);
            await _context.SaveChangesAsync();

            // Act
            await _repository.DeleteAsync(geoIPData.Id);

            // Assert
            var result = await _context.GeoIPData.FindAsync(geoIPData.Id);
            Assert.Null(result);
        }

        [Fact]
        public async Task DeleteAsync_WithNonExistingId_DoesNothing()
        {
            // Arrange
            var id = Guid.NewGuid();

            // Act & Assert (should not throw)
            await _repository.DeleteAsync(id);
        }

        [Fact]
        public async Task GetByCountryAsync_WithExistingCountryCode_ReturnsMatchingRecords()
        {
            // Arrange
            var usRecords = new List<GeoIPData>
            {
                CreateTestGeoIPData("192.168.1.1", "US"),
                CreateTestGeoIPData("192.168.1.2", "US")
            };

            var caRecord = CreateTestGeoIPData("192.168.1.3", "CA");

            await _context.GeoIPData.AddRangeAsync(usRecords);
            await _context.GeoIPData.AddAsync(caRecord);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetByCountryAsync("US");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count());
            Assert.All(result, x => Assert.Equal("US", x.CountryCode));
        }

        [Fact]
        public async Task GetByCountryAsync_WithNonExistingCountryCode_ReturnsEmptyList()
        {
            // Arrange
            var geoIPData = CreateTestGeoIPData("192.168.1.1", "US");

            await _context.GeoIPData.AddAsync(geoIPData);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetByCountryAsync("CA");

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetTotalRecordsAsync_ReturnsCorrectCount()
        {
            // Arrange
            var records = new List<GeoIPData>
            {
                CreateTestGeoIPData("192.168.1.1", "US"),
                CreateTestGeoIPData("192.168.1.2", "US"),
                CreateTestGeoIPData("192.168.1.3", "CA")
            };

            await _context.GeoIPData.AddRangeAsync(records);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.GetTotalRecordsAsync();

            // Assert
            Assert.Equal(3, result);
        }

        [Fact]
        public async Task ExistsAsync_WithExistingIP_ReturnsTrue()
        {
            // Arrange
            var geoIPData = CreateTestGeoIPData();

            await _context.GeoIPData.AddAsync(geoIPData);
            await _context.SaveChangesAsync();

            // Act
            var result = await _repository.ExistsAsync(geoIPData.IPAddress);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task ExistsAsync_WithNonExistingIP_ReturnsFalse()
        {
            // Arrange
            var ipAddress = "192.168.1.1";

            // Act
            var result = await _repository.ExistsAsync(ipAddress);

            // Assert
            Assert.False(result);
        }
    }
}