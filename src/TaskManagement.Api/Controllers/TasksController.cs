using Microsoft.AspNetCore.Mvc;
using TaskManagement.Api.Common.Exceptions;
using TaskManagement.Api.Models.DTOs;
using TaskManagement.Api.Services;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;

namespace TaskManagement.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TasksController : ControllerBase
    {
        private readonly ITaskService _taskService;
        public TasksController(ITaskService taskService)
        {
            _taskService = taskService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateTask([FromBody] CreateTaskDTO createTaskDTO)
        {
            var validationError = ValidateDTO();
            if (validationError != null)
            {
                return validationError;
            }

            var newTask = await _taskService.CreateTaskAsync(createTaskDTO);
            return CreatedAtAction(nameof(GetTask), new { id = newTask.Id }, newTask);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetTask([FromRoute] int id)
        {
            var task = await _taskService.GetTaskAsync(id);

            try
            {
                var updatedTask = await _taskService.GetTaskAsync(id);
                return Ok(task);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAllTasks([FromQuery] int page = 1, [FromQuery] int pageSize = 100)
        {
            var tasks = await _taskService.QueryTasksAsync(page, pageSize);
            return Ok(tasks);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTaskStatus([FromRoute] int id, [FromBody] UpdateStatusDTO updateStatusDTO)
        {
            var validationError = ValidateDTO();
            if (validationError != null)
            {
                return validationError;
            }

            try
            {
                var updatedTask = await _taskService.UpdateTaskStatusAsync(id, updateStatusDTO);
                return Ok(updatedTask);
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (ConflictException ex)
            {
                return Conflict(ex.Message);
            }
        }

        [HttpPut]
        public async Task<IActionResult> BulkUpdateTaskStatus([FromBody] BulkUpdateStatusDTO bulkUpdateStatusDTO)
        {
            var validationError = ValidateDTO();
            if (validationError != null)
            {
                return validationError;
            }

            try
            {
                var updatedTasksCount = await _taskService.BulkUpdateTasksAsync(bulkUpdateStatusDTO);
                return Ok(new
                {
                    Requested = bulkUpdateStatusDTO.Ids.Count, 
                    Updated = updatedTasksCount,
                    Skipped = bulkUpdateStatusDTO.Ids.Count - updatedTasksCount
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTask([FromRoute] int id)
        {          
            try
            {
                var result = await _taskService.DeleteTaskAsync(id);

                if (!result)
                    return UnprocessableEntity("Task could not be deleted due to unknown error.");
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }

            return NoContent();
        }

        private IActionResult? ValidateDTO()
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

                return BadRequest(new { Message = "Validation failed", Errors = errors });
            }
            return null;
        }
    }
}
