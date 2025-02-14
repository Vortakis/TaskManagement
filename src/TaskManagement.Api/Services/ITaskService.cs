using TaskManagement.Api.Common.DTOs;
using TaskManagement.Api.Models;
using TaskManagement.Api.Models.DTOs;

namespace TaskManagement.Api.Services
{
    public interface ITaskService
    {
        Task<TaskModel> CreateTaskAsync(CreateTaskDTO taskDTO);

        Task<TaskModel> UpdateTaskStatusAsync(int id, UpdateStatusDTO newStatusDTO);

        Task<int> BulkUpdateTasksAsync(BulkUpdateStatusDTO newStatusDTO);

        Task<bool> DeleteTaskAsync(int id);

        Task<TaskModel> GetTaskAsync(int id);

        Task<PaginatedResponse<TaskModel>> QueryTasksAsync(int page, int pageSize);
    }
}
