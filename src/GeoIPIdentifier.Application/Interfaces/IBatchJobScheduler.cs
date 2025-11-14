namespace GeoIPIdentifier.Application.Interfaces;

public interface IBatchJobScheduler
{
    Task<string> ScheduleBatchJobAsync(List<string> ipAddresses);
    Task<bool> CancelBatchJobAsync(string batchId);
    Task<IReadOnlyList<string>> GetScheduledBatchesAsync();
}