namespace TaskManagement.Api.Models.DTOs
{
    public class BulkUpdateResponseDTO
    {
        public int TotalRequested { get; set; }

        public int Updated { get; set; }

        public int Skipped
        {
            get
            {
                return TotalRequested - Updated;
            }
        }
    }
}
