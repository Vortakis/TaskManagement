using Microsoft.Extensions.Caching.Memory;

namespace TaskManagement.Api.Caching
{
    public class MemoryCacheRepository : ICacheRepository
    {
        private readonly MemoryCache _cache;

        public MemoryCacheRepository() 
        {
            _cache = new MemoryCache(new MemoryCacheOptions());
        }

        public async Task<T?> GetAsync<T>(int key)
        {
            return await Task.FromResult((T?)_cache.Get(key));
        }

        public async Task SetAsync<T>(int key, T value, DateTimeOffset? expiry = null)
        {
            _cache.Set(key, value, expiry ?? DateTimeOffset.UtcNow.AddMinutes(60));
            await Task.CompletedTask;
        }

        public async Task RemoveAsync(int key)
        {
            _cache.Remove(key);
            await Task.CompletedTask;
        }

        public async Task RemoveAsync(IEnumerable<int> keys)
        {
            foreach (var key in keys)
            {
                _cache.Remove(key.ToString());
            }

            await Task.CompletedTask;
        }
    }
}
