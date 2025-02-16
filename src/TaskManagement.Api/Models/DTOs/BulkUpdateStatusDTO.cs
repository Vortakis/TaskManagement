using Swashbuckle.AspNetCore.Annotations;
using System.ComponentModel.DataAnnotations;

namespace TaskManagement.Api.Models.DTOs
{
    public class BulkUpdateStatusDTO : UpdateStatusDTO
    {
        [Required]
        [MinLength(1)]
        [SwaggerSchema(Description = "List of Task Ids to update their Status.")]
        public HashSet<int> Ids { get; set; } = new HashSet<int>();
    }
}
