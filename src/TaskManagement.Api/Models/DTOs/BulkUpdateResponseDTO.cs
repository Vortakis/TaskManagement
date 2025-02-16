namespace TaskManagement.Api.Models.DTOs
{
    public class BulkUpdateResponseDTO
    {
        public int TotalCount { get; set; }
        public int SuccessCount { get; set; }
        public int NotFoundCount { get; set; }
        public int InvalidUpdateCount { get; set; }
        public int FailedCount { get; set; }
        public List<int> NotFoundIds { get; set; } = new List<int>();
        public List<int> InvalidUpdateIds { get; set; } = new List<int>();
        public List<int> FailedIds { get; set; } = new List<int>();
    }
}
