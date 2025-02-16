using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using TaskManagement.Api.Common.Configuration.Settings.Sections;
using TaskManagement.Api.Data;

namespace TaskManagement.Api.Common.Processing
{
    public class ParallelProcessor<TInput, TResult> : IParallelProcessor<TInput, TResult>
    {
        private readonly TaskDbContext _dbContext;
        private readonly ConcurrentProcessingSection _concurrencySettings;

        public ParallelProcessor(
            TaskDbContext dbContext,
            IOptions<ConcurrentProcessingSection> options)
        {
            _dbContext = dbContext;
            _concurrencySettings = options.Value;
        }

        public async Task<List<TResult>> ExecuteDBTransaction(IEnumerable<TInput> entries, Func<IEnumerable<TInput>, Task<List<TResult>>> batchAction)
        {
            List<TResult> allResults = new List<TResult>();

            using (var transaction = await _dbContext.Database.BeginTransactionAsync())
            {
                try
                {
                    var parallelTasks = new List<Task<List<TResult>>>();

                    foreach (var batch in entries.Chunk(_concurrencySettings.BatchSize))
                    {
                        var task = batchAction(batch);
                        parallelTasks.Add(task);

                        if (parallelTasks.Count >= _concurrencySettings.ParallelismDegree)
                        {
                            var completedTask = await Task.WhenAny(parallelTasks);
                            parallelTasks.Remove(completedTask);
                        }
                    }

                    // Ensure all remaining tasks are completed before finishing
                    var batchResults = await Task.WhenAll(parallelTasks);
                    foreach (var result in batchResults)
                    {
                        allResults.AddRange(result);
                    }

                    await transaction.CommitAsync();
                    return allResults;
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    throw new InvalidOperationException("Something went wrong with the bulk update. Any updates were rolled back.", ex);
                }
            }
        }
    }
}
