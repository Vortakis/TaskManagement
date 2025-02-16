namespace TaskManagement.Api.Common.Configuration.Settings.Sections
{
    public class ConcurrentProcessingSection
    {
        public int ParallelismDegree { get; set; } = 10;

        public int BatchSize { get; set; } = 1000;

        public int MaxRetries { get; set; } = 3;

        public int RetryDelayMS { get; set; } = 100;
    }
}
