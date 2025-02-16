using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NuGet.Packaging;
using System.Text.Json;
using TaskManagement.Api.Caching;
using TaskManagement.Api.Common.Configuration.Settings.Sections;
using TaskManagement.Api.Common.DTOs;
using TaskManagement.Api.Common.Exceptions;
using TaskManagement.Api.Common.Processing;
using TaskManagement.Api.Data;
using TaskManagement.Api.Models;
using TaskManagement.Api.Models.DTOs;
using TaskManagement.Api.Models.Enums;
using TaskManagement.Api.Models.Internal;
using TaskManagement.Api.Services.Handlers;
using TaskManagement.Api.Services.Helpers;

namespace TaskManagement.Api.Services
{
    public class TaskService : ITaskService
    {
        private readonly ConcurrentProcessingSection _concurrencySettings;
        private readonly TaskDbContext _dbContext;
        private readonly ICacheRepository _cacheRepo;
        private readonly ITaskPriorityHelper _priorityHandler;
        private readonly ICompletionStatusHelper _statusHandler;
        private readonly IParallelProcessor<int> _parallelProcessor;
        private readonly IBulkUpdateHandler _bulkUpdateHandler;

        public TaskService(
            IOptions<ConcurrentProcessingSection> options,
            TaskDbContext dbContext,
            ICacheRepository cacheRepo,
            ITaskPriorityHelper priorityHandler,
            ICompletionStatusHelper completionStatusHandler,
            IParallelProcessor<int> parallelProcessor,
            IBulkUpdateHandler bulkUpdateHandler)
        {
            _concurrencySettings = options.Value;
            _dbContext = dbContext;
            _cacheRepo = cacheRepo;
            _priorityHandler = priorityHandler;
            _statusHandler = completionStatusHandler;
            _parallelProcessor = parallelProcessor; 
            _bulkUpdateHandler = bulkUpdateHandler;
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
            BatchResult allResults = new BatchResult();

            await _parallelProcessor.ExecuteDBTransaction(
                bulkUpStatusDTO.Ids, 
                async (batch) => await _bulkUpdateHandler.ProcessBulkUpdateAsync(allResults, batch, bulkUpStatusDTO.Status));   

            await _cacheRepo.RemoveAsync(allResults.SuccessIds);

            BulkUpdateResponseDTO responseDTO = new()
            {
                TotalCount = bulkUpStatusDTO.Ids.Count,
                SuccessCount = allResults.SuccessIds.Count(),
                NotFoundCount = allResults.NotFoundIds.Count,
                NotFoundIds = allResults.NotFoundIds.ToList(),
                InvalidUpdateCount = allResults.InvalidIds.Count(),
                InvalidUpdateIds = allResults.InvalidIds.ToList(),
                FailedCount = allResults.FailedIds.Count(),
                FailedIds = allResults.FailedIds.ToList()
            };

            return responseDTO;
        }
    }
}
