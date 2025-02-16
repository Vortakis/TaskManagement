using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TaskManagement.Api.Common.Configuration.Settings.Sections;
using TaskManagement.Api.Data;
using TaskManagement.Api.Models;
using TaskManagement.Api.Models.Enums;
using System.Text.Json;
using TaskManagement.Api.Services.Helpers;
using TaskManagement.Api.Models.Internal;
using Microsoft.Extensions.Options;

namespace TaskManagement.Api.Services.Handlers
{

    public class BulkUpdateHandler : IBulkUpdateHandler
    {
        private readonly TaskDbContext _dbContext;
        private readonly ConcurrentProcessingSection _concurrencySettings;
        private readonly ITaskPriorityHelper _priorityHandler;
        private readonly ICompletionStatusHelper _statusHandler;

        public BulkUpdateHandler(
            TaskDbContext dbContext,
            IOptions<ConcurrentProcessingSection> options,
            ITaskPriorityHelper priorityHandler,
            ICompletionStatusHelper completionStatusHandler)
        {
            _dbContext = dbContext;
            _concurrencySettings = options.Value;
            _priorityHandler = priorityHandler;
            _statusHandler = completionStatusHandler;
        }

        public async Task ProcessBulkUpdateAsync(BatchResult batchResult, IEnumerable<int> requestedIds, CompletionStatus toStatus, int retryCount = 0)
        {
            var originTasks = await FetchTasksAsync(requestedIds);
            var foundBatchIds = originTasks.Select(x => x.Id).ToList();

            await ExecuteDBUpdateAsync(originTasks, toStatus);

            var updatedTasks = await FetchTasksAsync(foundBatchIds);

            var currFailedIds = CategorizeResults(batchResult,requestedIds, originTasks, updatedTasks, toStatus);

            if (currFailedIds.Any() && retryCount < _concurrencySettings.MaxRetries)
            {
                await RetryFailedEntries(batchResult, currFailedIds, toStatus, retryCount);
            }
        }

        private async Task<List<TaskModel>> FetchTasksAsync(IEnumerable<int> requestedIds)
        {
            return await _dbContext.Tasks
                .Where(t => requestedIds.Contains(t.Id))
                .ToListAsync();
        }

        private async Task ExecuteDBUpdateAsync(List<TaskModel> originTasks, CompletionStatus toStatus)
        {
            var batchData = JsonSerializer.Serialize(originTasks);

            var prioritySQL = _priorityHandler.GetPrioritySQL();
            var validStatusSQL = _statusHandler.ValidateStatusSQL();

            var sqlParams = new List<SqliteParameter>
            {
                new SqliteParameter("@toStatus", toStatus),
                new SqliteParameter("@batchJson", batchData),
                validStatusSQL.sqlParam
            };
            sqlParams.AddRange(prioritySQL.sqlParams);

            var upateBatchQuery = @$"
                UPDATE Tasks
                SET Status = @toStatus,
                    Priority = {prioritySQL.sql},
                    UpdatedAt = strftime('%Y-%m-%d %H.%M.%f', 'now') 
                WHERE 
                    (Id, UpdatedAt) IN ( 
                        SELECT 
                            json_extract(value, '$.Id'), 
                            json_extract(value, '$.UpdatedAt') 
                        FROM json_each(@batchJson, '$'))
                    AND ({validStatusSQL.sql});";

            var tasksAffectedCount = await _dbContext.Database.ExecuteSqlRawAsync(upateBatchQuery, sqlParams);

            _dbContext.ChangeTracker.Clear();
            await _dbContext.SaveChangesAsync();
        }

        private List<int> CategorizeResults(BatchResult batchResults, IEnumerable<int> requestedIds, List<TaskModel> originTasks, List<TaskModel> updatedTasks, CompletionStatus toStatus)
        {
            var originTasksDict = originTasks.ToDictionary(x => x.Id);

            var unaffectedTasks = updatedTasks
                    .Where(t => originTasksDict.TryGetValue(t.Id, out var originTask) && t.UpdatedAt == originTask.UpdatedAt);

            // Upated & no other process changed state.
            var succeededTasks = updatedTasks
                .Except(unaffectedTasks)
                .Where(t => t.Status == toStatus);
            succeededTasks
                .ToList() 
                .ForEach(t => batchResults.AddSuccess(t.Id));

            // Not found.
            requestedIds
                .Except(originTasks.Select(t => t.Id))
                .ToList()
                .ForEach(id => batchResults.AddNotFound(id));

            // Invalid due business-logic rules.
            var invalidTasks = unaffectedTasks
               .Where(t => _statusHandler.ValidateStatus(t.Status, toStatus, t.DueDateTimeUtc) != null);
            invalidTasks
                .ToList()
                .ForEach(t => batchResults.AddInvalid(t.Id));

            // Failed for concurrent or any other reason.
            var currFailedTasks = unaffectedTasks
                .Except(invalidTasks)
                .Union(updatedTasks.Except(unaffectedTasks).Except(succeededTasks)) // Updated by some other process - retry.
                .Select(t => t.Id)
                .ToList();
            currFailedTasks.ForEach(id => batchResults.AddFailed(id));

            return currFailedTasks;
        }

        private async Task RetryFailedEntries(BatchResult batchResult, List<int> failedIds, CompletionStatus toStatus, int retryCount)
        {
            retryCount++;
            Task.Delay(TimeSpan.FromMilliseconds(_concurrencySettings.RetryDelayMS)).Wait();

            await ProcessBulkUpdateAsync(batchResult, failedIds, toStatus, retryCount);
        }

    }
}
