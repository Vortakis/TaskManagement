using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;
using Microsoft.Extensions.Options;
using NuGet.Packaging;
using System.Collections.Generic;
using System.Diagnostics;
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
        private readonly ICompletionStatusHandler _completionStatusHandler;

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
            _completionStatusHandler = completionStatusHandler;
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

        public async Task<TaskModel> UpdateTaskStatusAsync(int id, UpdateStatusDTO updateStatusDTO)
        {
            var existingTask = await _dbContext.Tasks.FindAsync(id);
            if (existingTask == null)
                throw new KeyNotFoundException($"Task with ID {id} was not found.");

            string error = _completionStatusHandler.IsValid(existingTask.Status, updateStatusDTO.Status, existingTask.DueDateTimeUtc);

            if (error != null)
                throw new InvalidOperationException(error);

            existingTask.Status = updateStatusDTO.Status;
            existingTask.Priority = _priorityHandler.GetPriority(existingTask.DueDateTimeUtc);

            try
            {
                await _dbContext.SaveChangesAsync();

                // Invalidate Cache - reset after (avoid inconsistency).
                await _cacheRepo.RemoveAsync(id.ToString());
                await _cacheRepo.SetAsync(id.ToString(), existingTask, DateTimeOffset.UtcNow.AddMinutes(5));
                return existingTask;
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new ConflictException($"There was a conflict in database while updating '{id}'. No changes took place.");
            }
        }

        public async Task<int> BulkUpdateTasksAsync(BulkUpdateStatusDTO bulkUpStatusDTO)
        {
            int updatedTasksCount;

            using (var transaction = await _dbContext.Database.BeginTransactionAsync())
            {
                try
                {
                    var allIds = bulkUpStatusDTO.Ids;
                    var parallelTasks = new List<Task<int>>();

                    foreach (var batch in allIds.Chunk(_concurrencySettings.BatchSize))
                    {
                        var taskBatch = UpdateBatchAsync(batch, bulkUpStatusDTO.Status);

                        parallelTasks.Add(taskBatch);

                        if (parallelTasks.Count >= _concurrencySettings.ParallelismDegree)
                        {
                            await Task.WhenAny(parallelTasks);
                            parallelTasks.RemoveAll(task => task.IsCompleted);
                        }
                    }

                    updatedTasksCount = (await Task.WhenAll(parallelTasks)).Sum();
                    await transaction.CommitAsync();
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    throw new InvalidOperationException("Something went wrong with Bulk Update. Any updates were Rolled Back.", ex);
                }
            }

            return updatedTasksCount;
        }

        public async Task<TaskModel> GetTaskAsync(int id)
        {
            // Check cache first.
            var existingTask = await _cacheRepo.GetAsync<TaskModel>(id.ToString());
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
            await _cacheRepo.SetAsync(id.ToString(), existingTask, DateTimeOffset.UtcNow.AddMinutes(5));

            return existingTask;
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
                await _cacheRepo.RemoveAsync(id.ToString());

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

            return new PaginatedResponse<TaskModel> {TotalCount = totalCount, Count = tasks.Count, Response = tasks };
        }

        private async Task<int> UpdateBatchAsync(int[] batch, CompletionStatus toStatus)
        {
            string batchIdsSql = (batch.Length > 50) ? "SELECT Id FROM TempBatch" : string.Join(",", batch);

            var priorityQuery = _priorityHandler.GetPrioritySQL();
            var validStatusQuery = _completionStatusHandler.IsValidStatusSQL();

            var updatePropertiesSQL = @$"
                UPDATE Tasks
                SET Status = @toStatus,
                    Priority = {priorityQuery.sql},
                    UpdatedAt = datetime('now')
                WHERE Id IN ({batchIdsSql}) 
                    AND ({validStatusQuery.sql})
                    AND EXISTS (
                SELECT 1
                FROM Tasks AS t
                WHERE t.Id = Tasks.Id
                AND t.UpdatedAt = Tasks.UpdatedAt
            );";
         
            string tableUpdatePropertiesSql = @$"
                CREATE TEMP TABLE TempBatch (Id INTEGER PRIMARY KEY);
                INSERT INTO TempBatch (Id)
                SELECT value FROM json_each(@batchJson);

                {updatePropertiesSQL}

                DROP TABLE TempBatch;";

            var query = (batch.Length > 50) ? tableUpdatePropertiesSql : updatePropertiesSQL;

            var sqlParams = priorityQuery.sqlParams;
            sqlParams.Add(validStatusQuery.sqlParam);
            sqlParams.Add(new SqliteParameter("@toStatus", toStatus));
            sqlParams.Add(new SqliteParameter("@batchJson", JsonSerializer.Serialize(batch)));
 
            var tasksAffectedCount = await _dbContext.Database.ExecuteSqlRawAsync(query, sqlParams);

            return tasksAffectedCount;

            /*var tasksToUpdate = await _dbContext.Tasks
                               .Where(task => batch.Contains(task.Id))
                               .Where(_completionStatusHandler.IsValidFilter(toStatus))
                               .ToListAsync();

            foreach (var task in tasksToUpdate)
            {
                task.Status = toStatus;
                task.Priority = _priorityHandler.GetPriority(task.DueDateTimeUtc);
            }
            
            await _dbContext.SaveChangesAsync();
             lock (processedTasks)
            {
                processedTasks += tasksAffectedCount;
            }
            */
        }
    }
}
