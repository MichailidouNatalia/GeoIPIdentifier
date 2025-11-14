using AutoMapper;
using GeoIPIdentifier.Application.DTOs;
using GeoIPIdentifier.Application.Interfaces;
using GeoIPIdentifier.Domain.Exceptions;
using GeoIPIdentifier.Shared.Interfaces;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace GeoIPIdentifier.Application.Services;

public class GeoIPService(
    IGeoIPRepository repository,
    ICacheService cacheService,
    IMapper mapper,
    IExternalGeoIPService externalService,
    IBatchJobScheduler jobScheduler,
    IConnectionMultiplexer redis,
    ILogger<GeoIPService> logger) : IGeoIPService
{
  private readonly IGeoIPRepository _repository = repository;
  private readonly ICacheService _cacheService = cacheService;
  private readonly IMapper _mapper = mapper;
  private readonly IExternalGeoIPService _externalService = externalService;
    private readonly IBatchJobScheduler _jobScheduler = jobScheduler;
  private readonly IConnectionMultiplexer _redis = redis;
  private readonly ILogger<GeoIPService> _logger = logger;

  // Single IP Lookup
  public async Task<GeoIPResponseDto> IdentifyIPAsync(string ipAddress)
  {
    if (string.IsNullOrWhiteSpace(ipAddress))
      throw new DomainException("IP address cannot be empty");

    if (!System.Net.IPAddress.TryParse(ipAddress, out _))
      throw new InvalidIPAddressException(ipAddress);

    try
    {
      // Check cache first
      var cacheKey = $"geoip:{ipAddress}";
      var cached = await _cacheService.GetAsync<GeoIPResponseDto>(cacheKey);
      if (cached != null)
        return cached;

      // Check database
      var existing = await _repository.GetByIPAsync(ipAddress);
      if (existing != null)
      {
        var response = _mapper.Map<GeoIPResponseDto>(existing);
        await _cacheService.SetAsync(cacheKey, response, TimeSpan.FromHours(1));
        return response;
      }

      // Call external service
      var geoIPData = await _externalService.GetGeoIPDataAsync(ipAddress);
      await _repository.AddAsync(geoIPData);

      var result = _mapper.Map<GeoIPResponseDto>(geoIPData);
      await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromHours(1));

      return result;
    }
    catch (HttpRequestException)
    {
      throw new GeoIPServiceUnavailableException();
    }
    catch (Exception ex) when (ex.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
    {
      throw new RateLimitExceededException();
    }
  }

  // Batch Processing with Quartz
  public async Task<string> StartBatchProcessingAsync(List<string> ipAddresses)
    {
        var validIps = ipAddresses.Where(IsValidIpAddress).ToList();
        
        if (!validIps.Any())
            throw new ArgumentException("No valid IP addresses provided");

        var batchId = await _jobScheduler.ScheduleBatchJobAsync(validIps);

        // Initialize progress in Redis
        await InitializeBatchInRedisAsync(batchId,
                                          validIps.Count);

        _logger.LogInformation("Started batch processing for {BatchId} with {Count} IPs", 
            batchId, validIps.Count);
        
        return batchId;
    }

  public async Task ProcessBatchAsync(string batchId, List<string> ipAddresses)
  {
    var db = _redis.GetDatabase();

    await UpdateProgressInRedisAsync(db, batchId, "Processing", 0);

    // Process IPs with limited concurrency
    var semaphore = new SemaphoreSlim(5);
    var tasks = ipAddresses.Select(async ip =>
    {
      await semaphore.WaitAsync();
      try
      {
        await ProcessSingleIpInBatchAsync(ip, batchId);
      }
      finally
      {
        semaphore.Release();
      }
    });

    await Task.WhenAll(tasks);

    await UpdateProgressInRedisAsync(db, batchId, "Completed", ipAddresses.Count);

    _logger.LogInformation("Completed batch {BatchId}", batchId);
  }
    
  public async Task<BatchProgressResponse> GetBatchProgressAsync(string batchId)
  {
    var db = _redis.GetDatabase();
    var hashEntries = await db.HashGetAllAsync($"batch:{batchId}");

    if (hashEntries.Length == 0)
      return null;

    var total = (int)hashEntries.FirstOrDefault(e => e.Name == "total").Value;
    var processed = (int)hashEntries.FirstOrDefault(e => e.Name == "processed").Value;
    var startTime = DateTime.Parse(hashEntries.FirstOrDefault(e => e.Name == "startTime").Value!);
    var status = hashEntries.FirstOrDefault(e => e.Name == "status").Value!;

    var completedTimeEntry = hashEntries.FirstOrDefault(e => e.Name == "completedTime");
    DateTime? completedTime = !completedTimeEntry.Value.IsNullOrEmpty
        ? DateTime.Parse(completedTimeEntry.Value!)
        : null;

    TimeSpan? estimatedTimeRemaining = null;
    if (status == "Processing" && processed > 0)
    {
      var elapsed = DateTime.UtcNow - startTime;
      var itemsPerSecond = processed / elapsed.TotalSeconds;

      if (itemsPerSecond > 0)
      {
        var remainingItems = total - processed;
        var estimatedSecondsRemaining = remainingItems / itemsPerSecond;
        estimatedTimeRemaining = TimeSpan.FromSeconds(estimatedSecondsRemaining);
      }
    }

    return new BatchProgressResponse(
        BatchId: batchId,
        Status: status,
        Total: total,
        Processed: processed,
        StartTime: startTime,
        CompletedTime: completedTime)
    {
      EstimatedTimeRemaining = estimatedTimeRemaining
    };
  }

  private async Task ProcessSingleIpInBatchAsync(string ip, string batchId)
  {
    try
    {
      await IdentifyIPAsync(ip);
      _logger.LogDebug("Processed IP {Ip} for batch {BatchId}", ip, batchId);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error processing IP {Ip} for batch {BatchId}", ip, batchId);
    }
    finally
    {
      var db = _redis.GetDatabase();
      var newProcessed = await db.StringIncrementAsync($"batch:{batchId}:processed");
      await UpdateProgressInRedisAsync(db, batchId, "Processing", (int)newProcessed);
    }
  }

  private bool IsValidIpAddress(string ip)
  {
    return System.Net.IPAddress.TryParse(ip, out _);
  }

  private async Task InitializeBatchInRedisAsync(string batchId, int totalIps)
  {
    var db = _redis.GetDatabase();
    var hashEntries = new HashEntry[]
    {
            new("batchId", batchId),
            new("status", "Queued"),
            new("total", totalIps),
            new("processed", 0),
            new("startTime", DateTime.UtcNow.ToString("O"))
    };
    await db.HashSetAsync($"batch:{batchId}", hashEntries);
    await db.KeyExpireAsync($"batch:{batchId}", TimeSpan.FromHours(24));
  }

  private async Task UpdateProgressInRedisAsync(IDatabase redisDb, string batchId, string status, int processed)
  {
    var hashEntries = new List<HashEntry>
        {
            new("status", status),
            new("processed", processed)
        };

    if (status == "Completed")
    {
      hashEntries.Add(new HashEntry("completedTime", DateTime.UtcNow.ToString("O")));
    }

    await redisDb.HashSetAsync($"batch:{batchId}", hashEntries.ToArray());
  }
 
}