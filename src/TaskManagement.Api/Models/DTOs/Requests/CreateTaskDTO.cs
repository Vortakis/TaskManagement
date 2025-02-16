using Swashbuckle.AspNetCore.Annotations;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using TaskManagement.Api.Common.DataAnnotations.Validations;

namespace TaskManagement.Api.Models.DTOs
{
    public class CreateTaskDTO
    {
        [Required]
        [StringLength(200, MinimumLength = 1)]
        [SwaggerSchema(Description = "Task Title.")]
        public string Title { get; set; } = string.Empty;

        [StringLength(5000)]
        [SwaggerSchema(Description = "Task Description.")]
        public string Description { get; set; } = string.Empty;

        [Required]
        [FutureDateTime]
        [DefaultValue("2025-12-31T12:00+02:00")]
        [SwaggerSchema(Description = "Due DateTime for this Task.")]
        public DateTimeOffset DueDateTime { get; set; }
    }
}
