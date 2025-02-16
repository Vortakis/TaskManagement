namespace TaskManagement.Api.Common.Processing
{
    public interface IParallelProcessor<TInput, TResult>
    {
        Task<List<TResult>> ExecuteDBTransaction(IEnumerable<TInput> entries, Func<IEnumerable<TInput>, Task<List<TResult>>> batchAction);
    }
}
