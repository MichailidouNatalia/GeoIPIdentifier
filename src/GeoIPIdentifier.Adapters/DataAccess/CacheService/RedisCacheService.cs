using StackExchange.Redis;
using Newtonsoft.Json;
using GeoIPIdentifier.Shared.Interfaces;
using Microsoft.Extensions.Logging;

namespace GeoIPIdentifier.Adapters.DataAccess.CacheService;

public class RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger) : ICacheService
{
  private readonly IConnectionMultiplexer _redis = redis;
  private readonly IDatabase _database = redis.GetDatabase();

  private readonly ILogger<RedisCacheService> _logger = logger;

  public async Task<T?> GetAsync<T>(string key)
  {
    var value = await _database.StringGetAsync(key);
    if (value.HasValue)
    {
      _logger.LogDebug("Cache key found: {Key}", key);
    }
    return value.HasValue ? JsonConvert.DeserializeObject<T>(value!) : default;
  }

  public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
  {
     _logger.LogInformation("Setting cache key: {Key} with expiration: {Expiration}", key, expiration);
    var serializedValue = JsonConvert.SerializeObject(value);
    await _database.StringSetAsync(key, serializedValue, expiration);
  }

  public async Task RemoveAsync(string key)
  {
    await _database.KeyDeleteAsync(key);
  }
}