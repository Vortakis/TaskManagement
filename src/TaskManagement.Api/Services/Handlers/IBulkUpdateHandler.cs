using TaskManagement.Api.Models.Enums;
using TaskManagement.Api.Models.Internal;

namespace TaskManagement.Api.Services.Handlers
{
    public interface IBulkUpdateHandler
    {
        Task ProcessBulkUpdateAsync(BatchResult batchResult, IEnumerable<int> requestedIds, CompletionStatus toStatus, int retryCount = 0);
    }
}
