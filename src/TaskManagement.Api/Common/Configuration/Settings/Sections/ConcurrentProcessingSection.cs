namespace TaskManagement.Api.Common.Configuration.Settings.Sections
{
    public class ConcurrentProcessingSection
    {
        public int ParallelismDegree { get; set; } = 10;

        public int BatchSize { get; set; } = 1000;
    }
}
