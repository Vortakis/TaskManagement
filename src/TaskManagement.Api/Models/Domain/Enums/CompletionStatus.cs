using System.ComponentModel;

namespace TaskManagement.Api.Models.Enums
{
    public enum CompletionStatus
    {
        [Description("Task is Pending")]
        Pending,

        [Description("Task is In Progress")]
        InProgress,

        [Description("Task is In Completed")]
        Completed
    }
}
