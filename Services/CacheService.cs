using Microsoft.Extensions.Caching.Memory;

namespace StyleCast.Backend.Services;

public interface ICacheService
{
    T? GetData<T>(string key);
    void SetData<T>(string key, T data, DateTimeOffset expirationTime);
    void RemoveData(string key);
}

public class CacheService : ICacheService
{
    private readonly IMemoryCache _memoryCache;

    public CacheService(IMemoryCache memoryCache)
    {
        _memoryCache = memoryCache;
    }

    public T? GetData<T>(string key)
    {
        if (_memoryCache.TryGetValue(key, out T? data))
        {
            return data;
        }
        return default;
    }

    public void SetData<T>(string key, T data, DateTimeOffset expirationTime)
    {
        if (data != null)
        {
            var cacheOptions = new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(expirationTime)
                .SetPriority(CacheItemPriority.High);

            _memoryCache.Set(key, data, cacheOptions);
        }
    }

    public void RemoveData(string key)
    {
        if (!string.IsNullOrEmpty(key))
        {
            _memoryCache.Remove(key);
        }
    }
}