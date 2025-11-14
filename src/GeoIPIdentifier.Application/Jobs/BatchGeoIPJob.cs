using GeoIPIdentifier.Application.DTOs;
using GeoIPIdentifier.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using System.Text.Json;

namespace GeoIPIdentifier.Application.Jobs;

public class BatchGeoIPJob : IJob
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<BatchGeoIPJob> _logger;

    public BatchGeoIPJob(IServiceProvider serviceProvider, ILogger<BatchGeoIPJob> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var geoIPService = scope.ServiceProvider.GetRequiredService<IGeoIPService>();
            
            var batchDataJson = context.MergedJobDataMap.GetString("batchData");
            if (string.IsNullOrEmpty(batchDataJson))
            {
                _logger.LogError("Batch data not found in job context");
                return;
            }

            var batchData = JsonSerializer.Deserialize<BatchJobData>(batchDataJson);
            if (batchData == null)
            {
                _logger.LogError("Failed to deserialize batch data");
                return;
            }

            _logger.LogInformation("Executing Quartz job for batch {BatchId} with {Count} IPs", 
                batchData.BatchId, batchData.IpAddresses.Count);

            await geoIPService.ProcessBatchAsync(batchData.BatchId,
                                                 batchData.IpAddresses);

            _logger.LogInformation("Successfully completed Quartz job for batch {BatchId}", batchData.BatchId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing Quartz job {JobKey}", context.JobDetail.Key);
            throw new JobExecutionException(ex, false);
        }
    }
}