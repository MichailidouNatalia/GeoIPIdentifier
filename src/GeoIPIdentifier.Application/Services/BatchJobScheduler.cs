using System.Text.Json;
using GeoIPIdentifier.Application.DTOs;
using GeoIPIdentifier.Application.Jobs;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl.Matchers;

namespace GeoIPIdentifier.Application.Services;

public class BatchJobScheduler : Interfaces.IBatchJobScheduler
{
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ILogger<BatchJobScheduler> _logger;

    public BatchJobScheduler(ISchedulerFactory schedulerFactory, ILogger<BatchJobScheduler> logger)
    {
        _schedulerFactory = schedulerFactory;
        _logger = logger;
    }

    public async Task<string> ScheduleBatchJobAsync(List<string> ipAddresses)
    {
        var batchId = Guid.NewGuid().ToString();
        var scheduler = await _schedulerFactory.GetScheduler();

        var jobData = new BatchJobData(batchId, ipAddresses, DateTime.UtcNow);
        var jobDataJson = JsonSerializer.Serialize(jobData);

        var job = JobBuilder.Create<BatchGeoIPJob>()
            .WithIdentity($"batch-{batchId}", "geoip-batches")
            .UsingJobData("batchData", jobDataJson)
            .StoreDurably()
            .Build();

        var trigger = TriggerBuilder.Create()
            .WithIdentity($"trigger-{batchId}", "geoip-batches")
            .ForJob(job.Key)
            .StartNow()
            .Build();

        await scheduler.ScheduleJob(job, trigger);

        _logger.LogInformation("Scheduled Quartz job for batch {BatchId} with {Count} IPs", 
            batchId, ipAddresses.Count);

        return batchId;
    }

    public async Task<bool> CancelBatchJobAsync(string batchId)
    {
        try
        {
            var scheduler = await _schedulerFactory.GetScheduler();
            var jobKey = new JobKey($"batch-{batchId}", "geoip-batches");
            return await scheduler.DeleteJob(jobKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel batch job {BatchId}", batchId);
            return false;
        }
    }

    public async Task<IReadOnlyList<string>> GetScheduledBatchesAsync()
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        var jobKeys = await scheduler.GetJobKeys(GroupMatcher<JobKey>.GroupEquals("geoip-batches"));
        
        return jobKeys.Select(jk => jk.Name.Replace("batch-", "")).ToList();
    }
}