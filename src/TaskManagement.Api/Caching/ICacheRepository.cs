using TaskManagement.Api.Models;

namespace TaskManagement.Api.Caching
{
    public interface ICacheRepository
    {
        Task<T?> GetAsync<T>(int key);

        Task SetAsync<T>(int key, T value, DateTimeOffset? expiry);

        Task RemoveAsync(int key);

        Task RemoveAsync(IEnumerable<int> keys);

    }
}
