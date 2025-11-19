using System.Text.Json;
using GeoIPIdentifier.Application.DTOs;
using GeoIPIdentifier.Application.Jobs;
using GeoIPIdentifier.Application.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Quartz;
using Quartz.Impl.Matchers;

namespace GeoIPIdentifier.Application.Tests.Services
{
    public class BatchJobSchedulerTests
    {
        private readonly Mock<ISchedulerFactory> _schedulerFactoryMock;
        private readonly Mock<IScheduler> _schedulerMock;
        private readonly Mock<ILogger<BatchJobScheduler>> _loggerMock;
        private readonly BatchJobScheduler _scheduler;

        public BatchJobSchedulerTests()
        {
            _schedulerFactoryMock = new Mock<ISchedulerFactory>();
            _schedulerMock = new Mock<IScheduler>();
            _loggerMock = new Mock<ILogger<BatchJobScheduler>>();

            // Fix for GetScheduler with optional parameter
            _schedulerFactoryMock
                .Setup(x => x.GetScheduler(It.IsAny<CancellationToken>()))
                .ReturnsAsync(_schedulerMock.Object);

            _scheduler = new BatchJobScheduler(
                _schedulerFactoryMock.Object,
                _loggerMock.Object);
        }

        [Fact]
        public async Task ScheduleBatchJobAsync_WithValidIpAddresses_ShouldScheduleJob()
        {
            // Arrange
            var ipAddresses = new List<string> { "192.168.1.1", "10.0.0.1" };

            // Fix for ScheduleJob with optional parameter
            _schedulerMock
                .Setup(x => x.ScheduleJob(It.IsAny<IJobDetail>(), It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(DateTimeOffset.UtcNow);

            // Act
            var batchId = await _scheduler.ScheduleBatchJobAsync(ipAddresses);

            // Assert
            Assert.NotNull(batchId);
            Assert.NotEmpty(batchId);

            // Fix for Verify with optional parameter
            _schedulerMock.Verify(
                x => x.ScheduleJob(
                    It.Is<IJobDetail>(job => 
                        job.Key.Name.StartsWith("batch-") && 
                        job.Key.Group == "geoip-batches" &&
                        job.JobType == typeof(BatchGeoIPJob)),
                    It.Is<ITrigger>(trigger => 
                        trigger.Key.Name.StartsWith("trigger-") && 
                        trigger.Key.Group == "geoip-batches"),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task ScheduleBatchJobAsync_ShouldSerializeCorrectJobData()
        {
            // Arrange
            var ipAddresses = new List<string> { "192.168.1.1" };
            IJobDetail? capturedJob = null;

            // Fix for ScheduleJob with optional parameter
            _schedulerMock
                .Setup(x => x.ScheduleJob(It.IsAny<IJobDetail>(), It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()))
                .Callback<IJobDetail, ITrigger, CancellationToken>((job, trigger, token) => capturedJob = job)
                .ReturnsAsync(DateTimeOffset.UtcNow);

            // Act
            var batchId = await _scheduler.ScheduleBatchJobAsync(ipAddresses);

            // Assert
            Assert.NotNull(capturedJob);
            var jobData = capturedJob.JobDataMap.GetString("batchData");
            Assert.NotNull(jobData);

            var deserializedData = JsonSerializer.Deserialize<BatchJobData>(jobData);
            Assert.Equal(batchId, deserializedData?.BatchId);
            Assert.Equal(ipAddresses, deserializedData?.IpAddresses);
            Assert.True(deserializedData?.CreatedAt <= DateTime.UtcNow);
        }

        [Fact]
        public async Task ScheduleBatchJobAsync_WithEmptyIpList_ShouldStillScheduleJob()
        {
            // Arrange
            var ipAddresses = new List<string>();

            // Fix for ScheduleJob with optional parameter
            _schedulerMock
                .Setup(x => x.ScheduleJob(It.IsAny<IJobDetail>(), It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(DateTimeOffset.UtcNow);

            // Act
            var batchId = await _scheduler.ScheduleBatchJobAsync(ipAddresses);

            // Assert
            Assert.NotNull(batchId);
            
            // Fix for Verify with optional parameter
            _schedulerMock.Verify(
                x => x.ScheduleJob(It.IsAny<IJobDetail>(), It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task CancelBatchJobAsync_WithExistingJob_ShouldReturnTrue()
        {
            // Arrange
            var batchId = "test-batch-id";
            
            // Fix for DeleteJob with optional parameter
            _schedulerMock
                .Setup(x => x.DeleteJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _scheduler.CancelBatchJobAsync(batchId);

            // Assert
            Assert.True(result);
            
            // Fix for Verify with optional parameter
            _schedulerMock.Verify(
                x => x.DeleteJob(
                    It.Is<JobKey>(k => 
                        k.Name == $"batch-{batchId}" && k.Group == "geoip-batches"),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task CancelBatchJobAsync_WithNonExistingJob_ShouldReturnFalse()
        {
            // Arrange
            var batchId = "non-existing-batch";
            
            // Fix for DeleteJob with optional parameter
            _schedulerMock
                .Setup(x => x.DeleteJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act
            var result = await _scheduler.CancelBatchJobAsync(batchId);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task CancelBatchJobAsync_WhenExceptionThrown_ShouldReturnFalse()
        {
            // Arrange
            var batchId = "error-batch";
            var exception = new Exception("Scheduler error");

            // Fix for DeleteJob with optional parameter
            _schedulerMock
                .Setup(x => x.DeleteJob(It.IsAny<JobKey>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(exception);

            // Act
            var result = await _scheduler.CancelBatchJobAsync(batchId);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task GetScheduledBatchesAsync_WithScheduledJobs_ShouldReturnBatchIds()
        {
            // Arrange
            var jobKeys = new List<JobKey>
            {
                new JobKey("batch-123", "geoip-batches"),
                new JobKey("batch-456", "geoip-batches"),
                new JobKey("batch-789", "geoip-batches")
            };

            // Fix for GetJobKeys with optional parameter
            _schedulerMock
                .Setup(x => x.GetJobKeys(It.IsAny<GroupMatcher<JobKey>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(jobKeys);

            // Act
            var result = await _scheduler.GetScheduledBatchesAsync();

            // Assert
            Assert.Equal(3, result.Count);
            Assert.Contains("123", result);
            Assert.Contains("456", result);
            Assert.Contains("789", result);
            Assert.DoesNotContain("batch-", result);
        }

        [Fact]
        public async Task GetScheduledBatchesAsync_WithNoScheduledJobs_ShouldReturnEmptyList()
        {
            // Arrange
            var jobKeys = new List<JobKey>();

            // Fix for GetJobKeys with optional parameter
            _schedulerMock
                .Setup(x => x.GetJobKeys(It.IsAny<GroupMatcher<JobKey>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(jobKeys);

            // Act
            var result = await _scheduler.GetScheduledBatchesAsync();

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetScheduledBatchesAsync_WithMixedJobGroups_ShouldOnlyReturnGeoIpBatches()
        {
            // Arrange
            var geoIpJobKeys = new List<JobKey>
            {
                new JobKey("batch-123", "geoip-batches"),
                new JobKey("batch-789", "geoip-batches")
            };

            var otherJobKeys = new List<JobKey>
            {
                new JobKey("batch-456", "other-group")
            };

            // Fix: Mock the specific group matcher for geoip-batches
            _schedulerMock
                .Setup(x => x.GetJobKeys(GroupMatcher<JobKey>.GroupEquals("geoip-batches"), It.IsAny<CancellationToken>()))
                .ReturnsAsync(geoIpJobKeys);

            _schedulerMock
                .Setup(x => x.GetJobKeys(GroupMatcher<JobKey>.GroupEquals("other-group"), It.IsAny<CancellationToken>()))
                .ReturnsAsync(otherJobKeys);

            // Act
            var result = await _scheduler.GetScheduledBatchesAsync();

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Contains("123", result);
            Assert.Contains("789", result);
            Assert.DoesNotContain("456", result);
        }

        [Fact]
        public async Task ScheduleBatchJobAsync_WhenSchedulerThrowsException_ShouldPropagateException()
        {
            // Arrange
            var ipAddresses = new List<string> { "192.168.1.1" };
            var expectedException = new Exception("Scheduler failed");

            // Fix for ScheduleJob with optional parameter
            _schedulerMock
                .Setup(x => x.ScheduleJob(It.IsAny<IJobDetail>(), It.IsAny<ITrigger>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(expectedException);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<Exception>(
                () => _scheduler.ScheduleBatchJobAsync(ipAddresses));

            Assert.Equal("Scheduler failed", exception.Message);
        }
    }
}