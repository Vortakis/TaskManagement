namespace TaskManagement.Api.Common.Processing
{
    public interface IParallelProcessor<TInput>
    {
        Task ExecuteDBTransaction(IEnumerable<TInput> entries, Func<IEnumerable<TInput>, Task> batchAction);
    }
}
