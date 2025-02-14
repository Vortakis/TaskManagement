using TaskManagement.Api.Models;

namespace TaskManagement.Api.Caching
{
    public interface ICacheRepository
    {
        Task<T?> GetAsync<T>(string key);

        Task SetAsync<T>(string key, T value, DateTimeOffset? expiry);

        Task RemoveAsync(string key);
    }
}
