using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using TaskManagement.Api.Common.Configuration.Settings.Sections;
using TaskManagement.Api.Data;

namespace TaskManagement.Api.Common.Processing
{
    public class ParallelProcessor<TInput> : IParallelProcessor<TInput>
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

        public async Task ExecuteDBTransaction(IEnumerable<TInput> entries, Func<IEnumerable<TInput>, Task> batchAction)
        {
       //     TResult[] allResults;

            using (var transaction = await _dbContext.Database.BeginTransactionAsync())
            {
                try
                {
                    var parallelTasks = new List<Task>();

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
                    await Task.WhenAll(parallelTasks);

                    await transaction.CommitAsync();
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
