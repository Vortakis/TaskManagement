using Swashbuckle.AspNetCore.Annotations;
using System.ComponentModel.DataAnnotations;
using TaskManagement.Api.Common.DataAnnotations.Validations;
using TaskManagement.Api.Models.Enums;

namespace TaskManagement.Api.Models.DTOs
{
    public class UpdateStatusDTO
    {
        [Required]
        [ValidEnum(typeof(CompletionStatus))]
        [SwaggerSchema(Description = "New Status to update Task.")]
        public CompletionStatus Status { get; set; }
    }
}
