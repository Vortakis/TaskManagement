using Microsoft.AspNetCore.Mvc;
using TaskManagement.Api.Common.Exceptions;
using TaskManagement.Api.Models.DTOs;
using TaskManagement.Api.Services;

namespace TaskManagement.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TasksController : ControllerBase
    {
        private readonly ITaskService _taskService;
        private readonly ILogger<TasksController> _logger;

        public TasksController(
            ITaskService taskService,
            ILogger<TasksController> logger)
        {
            _taskService = taskService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> CreateTask([FromBody] CreateTaskDTO createTaskDTO)
        {
            _logger.LogInformation($"CreateTask API started...");

            var validationError = ValidateDTO();
            if (validationError != null)
            {
                return validationError;
            }

            var newTask = await _taskService.CreateTaskAsync(createTaskDTO);

            _logger.LogInformation($"CreateTask API finished - Created ID: '{newTask.Id}'.");
            return CreatedAtAction(nameof(GetTask), new { id = newTask.Id }, newTask);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetTask([FromRoute] int id)
        {
            _logger.LogInformation($"GetTask API started...");

            var task = await _taskService.GetTaskAsync(id);

            try
            {
                var updatedTask = await _taskService.GetTaskAsync(id);
                _logger.LogInformation($"GetTask API finished.");
                return Ok(task);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning($"GetTask API Task Not Found: '{id}'.");
                return NotFound(ex.Message);
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetAllTasks([FromQuery] int page = 1, [FromQuery] int pageSize = 100)
        {
            _logger.LogInformation($"GetAllTasks API started...");
            var tasks = await _taskService.QueryTasksAsync(page, pageSize);
            _logger.LogInformation($"GetAllTasks API finished.");
            return Ok(tasks);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTaskStatus([FromRoute] int id, [FromBody] UpdateStatusDTO updateStatusDTO)
        {
            _logger.LogInformation($"UpdateTaskStatus API started...");

            var validationError = ValidateDTO();
            if (validationError != null)
            {
                return validationError;
            }

            try
            {
                var updatedTask = await _taskService.UpdateTaskStatusAsync(id, updateStatusDTO);
                _logger.LogInformation($"UpdateTaskStatus API finished.");
                return Ok(updatedTask);
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning($"UpdateTaskStatus API Task Not Found: '{id}'.");
                return NotFound(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning($"UpdateTaskStatus API Invalid Operation: '{id}'. " + ex.Message);
                return BadRequest(ex.Message);
            }
            catch (ConflictException ex)
            {
                _logger.LogWarning($"UpdateTaskStatus API Conflict: '{id}'. " + ex.Message);
                return Conflict(ex.Message);
            }
        }

        [HttpPut]
        public async Task<IActionResult> BulkUpdateTaskStatus([FromBody] BulkUpdateStatusDTO bulkUpdateStatusDTO)
        {
            _logger.LogInformation($"BulkUpdateTaskStatus API started...");

            var validationError = ValidateDTO();
            if (validationError != null)
            {
                return validationError;
            }

            try
            {
                var results = await _taskService.BulkUpdateTasksAsync(bulkUpdateStatusDTO);
                _logger.LogInformation($"BulkUpdateTaskStatus API finished.");
                return Ok(results);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning($"BulkUpdateTaskStatus API Invalid Operation. " + ex.Message);
                return BadRequest(ex.Message);
            }
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTask([FromRoute] int id)
        {
            _logger.LogInformation($"DeleteTask API started...");

            try
            {
                var result = await _taskService.DeleteTaskAsync(id);

                if (!result)
                {
                    _logger.LogWarning($"DeleteTask API ID: '{id}'. UnprocessableEntity - Task could not be deleted due to unknown error.");
                    return UnprocessableEntity("Task could not be deleted due to unknown error.");
                }
            }
            catch (KeyNotFoundException ex)
            {
                _logger.LogWarning($"UnprocessableEntity API Task Not Found: '{id}'.");
                return NotFound(ex.Message);
            }

            _logger.LogInformation($"DeleteTask API finished.");
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

                _logger.LogWarning($"Bad Request - Validation Errors: {string.Join(" ||", errors)}.");
                return BadRequest(new { Message = "Validation failed", Errors = errors });
            }
            return null;
        }
    }
}
