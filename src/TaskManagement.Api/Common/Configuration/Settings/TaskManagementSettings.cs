using TaskManagement.Api.Common.Configuration.Settings.Sections;

namespace TaskManagement.Api.Common.Configuration.Settings
{
    public class TaskManagementSettings
    {
        public TaskPrioritySection TaskPrioritySection { get; set; } = new();

        public TaskCompletionSection TaskCompletionSection { get; set; } = new();
    }
}
