namespace TaskManagement.Api.Common.DTOs
{
    public class PaginatedResponse<T>
    {
        public int TotalCount { get; set; }

        public int Count { get; set; }

        public List<T> Response { get; set; } = new List<T>();

    }
}
