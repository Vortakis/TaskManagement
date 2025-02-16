using Humanizer;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NuGet.Packaging;
using NuGet.Protocol;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.Json;
using TaskManagement.Api.Caching;
using TaskManagement.Api.Common.Configuration.Settings.Sections;
using TaskManagement.Api.Common.DTOs;
using TaskManagement.Api.Common.Exceptions;
using TaskManagement.Api.Data;
using TaskManagement.Api.Models;
using TaskManagement.Api.Models.DTOs;
using TaskManagement.Api.Models.Enums;
using TaskManagement.Api.Services.Handlers;

namespace TaskManagement.Api.Services
{
    public class TaskService : ITaskService
    {
        private readonly ConcurrentProcessingSection _concurrencySettings;
        private readonly TaskDbContext _dbContext;
        private readonly ICacheRepository _cacheRepo;
        private readonly ITaskPriorityHandler _priorityHandler;
        private readonly ICompletionStatusHandler _statusHandler;

        public TaskService(
            IOptions<ConcurrentProcessingSection> options,
            TaskDbContext dbContext,
            ICacheRepository cacheRepo,
            ITaskPriorityHandler priorityHandler,
            ICompletionStatusHandler completionStatusHandler)
        {
            _concurrencySettings = options.Value;
            _dbContext = dbContext;
            _cacheRepo = cacheRepo;
            _priorityHandler = priorityHandler;
            _statusHandler = completionStatusHandler;
        }

        public async Task<TaskModel> CreateTaskAsync(CreateTaskDTO taskDTO)
        {
            var newTask = new TaskModel
            {
                Title = taskDTO.Title,
                Description = taskDTO.Description,
                DueDateTimeUtc = taskDTO.DueDateTime.UtcDateTime,
                TzOffsetMinutes = (int)taskDTO.DueDateTime.Offset.TotalMinutes,
                Status = CompletionStatus.Pending,
                Priority = _priorityHandler.GetPriority(taskDTO.DueDateTime.UtcDateTime)
            };

            _dbContext.Tasks.Add(newTask);
            await _dbContext.SaveChangesAsync();
            return newTask;
        }
        public async Task<TaskModel> GetTaskAsync(int id)
        {
            // Check cache first.
            var existingTask = await _cacheRepo.GetAsync<TaskModel>(id);
            if (existingTask != null)
            {
                return existingTask;
            }

            // Then database.
            existingTask = await _dbContext.Tasks.FindAsync(id);
            if (existingTask == null)
                throw new KeyNotFoundException($"Task with ID {id} was not found.");

            // Set Priority for GET display only - not saved in db.
            existingTask.Priority = _priorityHandler.GetPriority(existingTask.DueDateTimeUtc);

            // Save to cache.
            await _cacheRepo.SetAsync(id, existingTask, DateTimeOffset.UtcNow.AddMinutes(5));

            return existingTask;
        }

        public async Task<TaskModel> UpdateTaskStatusAsync(int id, UpdateStatusDTO updateStatusDTO)
        {
            var existingTask = await _dbContext.Tasks.FindAsync(id);
            if (existingTask == null)
                throw new KeyNotFoundException($"Task with ID {id} was not found.");

            string error = _statusHandler.ValidateStatus(existingTask.Status, updateStatusDTO.Status, existingTask.DueDateTimeUtc);

            if (error != null)
                throw new InvalidOperationException(error);

            existingTask.Status = updateStatusDTO.Status;
            existingTask.Priority = _priorityHandler.GetPriority(existingTask.DueDateTimeUtc);

            try
            {
                await _dbContext.SaveChangesAsync();

                // Invalidate Cache - reset after (avoid inconsistency).
                await _cacheRepo.RemoveAsync(id);
                await _cacheRepo.SetAsync(id, existingTask, DateTimeOffset.UtcNow.AddMinutes(5));
                return existingTask;
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new ConflictException($"There was a conflict in database while updating '{id}'. No changes took place.");
            }
        }

        public async Task<bool> DeleteTaskAsync(int id)
        {
            var existingTask = await _dbContext.Tasks.FindAsync(id);
            if (existingTask == null)
                throw new KeyNotFoundException($"Task with ID {id} was not found.");

            _dbContext.Tasks.Remove(existingTask);

            var affectedRows = await _dbContext.SaveChangesAsync();
            bool result = affectedRows > 0;

            // Invalidate cache - remove if entry exists.
            if (result)
                await _cacheRepo.RemoveAsync(id);

            return result;
        }

        public async Task<PaginatedResponse<TaskModel>> QueryTasksAsync(int page, int pageSize)
        {
            var priorityQuery = _priorityHandler.GetPrioritySQL("t");

            var query = @$"
                SELECT 
                    t.Id,
                    t.Title,
                    t.Description,
                    t.DueDateTimeUtc,
                    t.TzOffsetMinutes,
                    t.Status,
                    t.CreatedAt,
                    t.UpdatedAt, 
                {priorityQuery.sql} AS Priority
                FROM Tasks t
                ORDER BY Priority";

            var tasks = await _dbContext.Tasks
                .FromSqlRaw(query, priorityQuery.sqlParams.ToArray())
                .AsNoTracking()
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var totalCount = await _dbContext.Tasks.CountAsync();

            return new PaginatedResponse<TaskModel> { TotalCount = totalCount, Count = tasks.Count, Response = tasks };
        }

        public async Task<BulkUpdateResponseDTO> BulkUpdateTasksAsync(BulkUpdateStatusDTO bulkUpStatusDTO)
        {
            (List<int> SuccessIds, List<int> NotFoundIds, List<int> InvalidIds, List<int> FailedIds)[] batchResult;
            List<int> successIds = new List<int>();
            List<int> notFoundIds = new List<int>();
            List<int> invalidIds = new List<int>();
            List<int> failedIds = new List<int>();

            using (var transaction = await _dbContext.Database.BeginTransactionAsync())
            {
                try
                {
                    var allIds = bulkUpStatusDTO.Ids;
                    var parallelTasks = new List<Task<(List<int>, List<int>, List< int>, List<int>)>>();

                    foreach (var batchIds in allIds.Chunk(_concurrencySettings.BatchSize))
                    {
                        var taskBatch = UpdateBatchAsync(batchIds.ToList(), bulkUpStatusDTO.Status);

                        parallelTasks.Add(taskBatch);

                        if (parallelTasks.Count >= _concurrencySettings.ParallelismDegree)
                        {
                            await Task.WhenAny(parallelTasks);
                            parallelTasks.RemoveAll(task => task.IsCompleted);
                        }
                    }

                    // Accumulate parallel tasks completion and results.
                    batchResult = await Task.WhenAll(parallelTasks);

                    // Commit all changes
                    await transaction.CommitAsync();
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    throw new InvalidOperationException("Something went wrong with Bulk Update. Any updates were Rolled Back.", ex);
                }
            }

            successIds = batchResult.SelectMany(t => t.SuccessIds).ToList();
            notFoundIds = batchResult.SelectMany(t => t.NotFoundIds).ToList();
            invalidIds = batchResult.SelectMany(t => t.InvalidIds).ToList();
            failedIds = batchResult.SelectMany(t => t.FailedIds).ToList();

            await _cacheRepo.RemoveAsync(successIds);

            BulkUpdateResponseDTO responseDTO = new()
            {
                TotalCount = bulkUpStatusDTO.Ids.Count,
                SuccessCount = successIds.Count(),
                NotFoundCount = notFoundIds.Count,
                NotFoundIds = notFoundIds,
                InvalidUpdateCount = invalidIds.Count(),
                InvalidUpdateIds = invalidIds,
                FailedCount = failedIds.Count(),
                FailedIds = failedIds
            };

            return responseDTO;
        }

        private async Task<(List<int> SuccessIds, List<int> NotFoundIds, List<int> InvalidIds, List<int> FailedIds)> UpdateBatchAsync(List<int> requestedIds, CompletionStatus toStatus, int retryCount = 0)
        {
            var prioritySQL = _priorityHandler.GetPrioritySQL();
            var validStatusSQL = _statusHandler.ValidateStatusSQL();

            var originTasks = await _dbContext.Tasks
                .AsNoTracking()
                .Where(t => requestedIds.Contains(t.Id))
                .Select(t => new
                {
                    Id = t.Id,
                    UpdatedAt = t.UpdatedAt
                })
                .ToListAsync();
            var foundBatchIds = originTasks.Select(x => x.Id).ToList();        
            var batchData = JsonSerializer.Serialize(originTasks);

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

            await _dbContext.SaveChangesAsync();

            var originTasksDict = originTasks.ToDictionary(x => x.Id);
            var updatedTasks = await _dbContext.Tasks
                .AsNoTracking()
                .Where(t => foundBatchIds.Contains(t.Id))
                .Select(t => new { t.Id, t.Status, t.DueDateTimeUtc, t.UpdatedAt })
                .ToListAsync();

            var unaffectedTasks = updatedTasks
                    .Where(t => originTasksDict.TryGetValue(t.Id, out var originTask) && t.UpdatedAt == originTask.UpdatedAt);

            // Upated & no other process changed state.
            var succeededTasks = updatedTasks
                .Except(unaffectedTasks)
                .Where(t => t.Status == toStatus);
            var successIds = succeededTasks.Select(t => t.Id).ToList();

            // Not found.
            var notFoundIds = requestedIds.Except(foundBatchIds).ToList();

            // Invalid due business-logic rules.
            var invalidTasks = unaffectedTasks
                .Where(t => _statusHandler.ValidateStatus(t.Status, toStatus, t.DueDateTimeUtc) != null);
            var invalidIds = invalidTasks.Select(t => t.Id).ToList();

            // Failed for concurrent or any other reason.
            var failedTasks = unaffectedTasks
                .Except(invalidTasks)
                .Union(updatedTasks.Except(unaffectedTasks).Except(succeededTasks));  // Updated by some other process - retry.
            var failedIds = failedTasks.Select(t => t.Id).ToList();

            // Retry logic for failed ones.
            if (failedTasks.Any() && retryCount < _concurrencySettings.MaxRetries)
            {
                retryCount++;
                var retryDelay = TimeSpan.FromMilliseconds(_concurrencySettings.RetryDelayMS);
                await Task.Delay(retryDelay);

                var subResults = await UpdateBatchAsync(failedIds, toStatus, retryCount);
                successIds.AddRange(subResults.SuccessIds);
                notFoundIds.AddRange(subResults.NotFoundIds);
                invalidIds.AddRange(subResults.InvalidIds);
                failedIds = subResults.FailedIds;
            }

            return (successIds, notFoundIds, invalidIds, failedIds);
        }
    }
}
