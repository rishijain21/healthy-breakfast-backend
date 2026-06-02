using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Sovva.Application.Interfaces;

namespace Sovva.Infrastructure.Services;

public class CacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<CacheService> _logger;
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public CacheService(IDistributedCache cache, ILogger<CacheService> logger)
    {
        _cache = cache;
        _logger = logger;
        _jsonSerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            ReferenceHandler = ReferenceHandler.IgnoreCycles
        };
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            var cachedData = await _cache.GetStringAsync(key);

            if (string.IsNullOrEmpty(cachedData))
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(cachedData, _jsonSerializerOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get item from cache. Key: {Key}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expirationTime = null)
    {
        try
        {
            var serializedData = JsonSerializer.Serialize(value, _jsonSerializerOptions);

            var options = new DistributedCacheEntryOptions();
            
            if (expirationTime.HasValue)
            {
                options.AbsoluteExpirationRelativeToNow = expirationTime;
            }
            else
            {
                options.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1); // Default expiration
            }

            await _cache.SetStringAsync(key, serializedData, options);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set item in cache. Key: {Key}", key);
        }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            await _cache.RemoveAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove item from cache. Key: {Key}", key);
        }
    }
}
