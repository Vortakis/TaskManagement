using Newtonsoft.Json;
using Swashbuckle.AspNetCore.Annotations;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TaskManagement.Api.Models.Enums;

namespace TaskManagement.Api.Models
{
    public class TaskModel
    {
        [Key]
        [SwaggerSchema(Description = "Task Id.")]
        public int Id { get; set; } //Guid.NewGuid();

        [Required]
        [SwaggerSchema(Description = "Task Title.")]
        public string Title { get; set; } = string.Empty;

        [SwaggerSchema(Description = "Task Description.")]
        public string? Description { get; set; }

        [Required]
        [SwaggerSchema(Description = "Due DateTime for this Task in UTC.")]
        public DateTime DueDateTimeUtc { get; set; }

        [SwaggerSchema(Description = "Timezone Offset Minutes.")]
        public int TzOffsetMinutes { get; set; } = 0;

        [Required]
        [SwaggerSchema(Description = "Current Task Status.")]
        public CompletionStatus Status { get; set; }

        [Required]
        [SwaggerSchema(Description = "Current Task Priority.")]
        public TaskPriority Priority { get; set; }

        [SwaggerSchema(Description = "DateTime the Task was Created.")]
        public string CreatedAt { get; set; }

        [ConcurrencyCheck]
        [SwaggerSchema(Description = "DateTime the Task was Last Updated.")]
        public string UpdatedAt { get; set; }
    }
}
