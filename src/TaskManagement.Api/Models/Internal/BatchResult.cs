using System.Collections.Concurrent;

namespace TaskManagement.Api.Models.Internal
{
    public class BatchResult
    {
        public ConcurrentBag<int> SuccessIds { get; } = new();
        public ConcurrentBag<int> NotFoundIds { get; } = new();
        public ConcurrentBag<int> InvalidIds { get; } = new();
  
        private readonly ConcurrentDictionary<int, byte> _failedIds = new();
        public IEnumerable<int> FailedIds => _failedIds.Keys;

        public void AddSuccess(int id) => SuccessIds.Add(id);
        public void AddNotFound(int id) => NotFoundIds.Add(id);
        public void AddInvalid(int id) => InvalidIds.Add(id);
        public void AddFailed(int id) => _failedIds.TryAdd(id, 0);
        public bool RemoveFailed(int id) => _failedIds.TryRemove(id, out _); 
    }
}
