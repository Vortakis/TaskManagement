using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaskManagement.Api.Common.Configuration.Settings.Sections;
using TaskManagement.Api.Data;
using TaskManagement.Api.Models;
using TaskManagement.Api.Models.Enums;
using TaskManagement.Api.Models.Internal;
using TaskManagement.Api.Services.Handlers;
using TaskManagement.Api.Services.Helpers;

namespace TaskManagement.Tests.Services.Handlers
{
    public class BulkUpdateHandlerTests
    {
        private readonly TaskPriorityHelper _priorityHelperMock;
        private readonly CompletionStatusHelper _statusHelperMock;
        private readonly TaskDbContext _dbContext;
        private readonly BulkUpdateHandler _handler;

        private Mock<IOptions<ConcurrentProcessingSection>> _concurSettingsMock;
        private Mock<IOptions<TaskPrioritySection>> _prioritySettingsMock;
        private Mock<IOptions<TaskCompletionSection>> _statusSettingsMock;

        public BulkUpdateHandlerTests()
        {
            var connection = new SqliteConnection("Filename=:memory:");
            connection.Open();
            var options = new DbContextOptionsBuilder<TaskDbContext>()
                .UseSqlite(connection)
                .Options;

            SetupSettings();

            _priorityHelperMock = new TaskPriorityHelper(_prioritySettingsMock!.Object);
            _statusHelperMock = new CompletionStatusHelper(_statusSettingsMock!.Object);

            _dbContext = new TaskDbContext(options, null!, _concurSettingsMock!.Object);
            _handler = new BulkUpdateHandler(_dbContext, _concurSettingsMock.Object, _priorityHelperMock, _statusHelperMock);

            _dbContext.Database.EnsureCreated();
        }

        [Fact]
        public async Task ProcessBulkUpdateAsync_NotFoundIds()
        {
            // Arrange
            var taskList = new List<TaskModel>
            {
                new TaskModel { Id = 1, Status = CompletionStatus.InProgress },
                new TaskModel { Id = 2, Status = CompletionStatus.InProgress }
            };
            var requestedIds = taskList.Select(t => t.Id);
            var batchResult = new BatchResult();
            var toStatus = CompletionStatus.Completed;

            // Act
            await _handler.ProcessBulkUpdateAsync(batchResult, requestedIds, toStatus);

            // Assert
            batchResult.NotFoundIds.Should().Contain(requestedIds);
        }

        [Fact]
        public async Task ProcessBulkUpdateAsync_MixResults()
        {
            // Arrange

            var tasks = new List<TaskModel>
            { 
                new TaskModel {Id = 1, Status = CompletionStatus.InProgress, DueDateTimeUtc =DateTime.UtcNow},
                new TaskModel {Id = 2, Status = CompletionStatus.InProgress, DueDateTimeUtc =DateTime.UtcNow.AddDays(2)},
                new TaskModel {Id = 3, Status = CompletionStatus.InProgress, DueDateTimeUtc =DateTime.UtcNow.AddDays(5)}
            };
            _dbContext.Tasks.AddRange(tasks);
            await _dbContext.SaveChangesAsync(); 

            var requestedIds = tasks.Select(t => t.Id);
            var batchResult = new BatchResult();

            var toStatus = CompletionStatus.Completed;

            // Act
            await _handler.ProcessBulkUpdateAsync(batchResult, requestedIds, toStatus);

            // Assert
            batchResult.SuccessIds.Count().Should().Be(2);
            batchResult.InvalidIds.Count().Should().Be(1);
            var successTask = await _dbContext.Tasks
                .Where(t => batchResult.SuccessIds.Contains(t.Id))
                .ToListAsync();
            successTask.Should().OnlyContain(task => task.Status == CompletionStatus.Completed);
            var invalidTasks = await _dbContext.Tasks
                .Where(t => batchResult.InvalidIds.Contains(t.Id))
                .ToListAsync();

            invalidTasks.Should().OnlyContain(task => task.Status == CompletionStatus.InProgress);

        }
        
        private void SetupSettings()
        {
            _concurSettingsMock = new Mock<IOptions<ConcurrentProcessingSection>>();
            _concurSettingsMock.Setup(o => o.Value).Returns(new ConcurrentProcessingSection());
            _prioritySettingsMock = new Mock<IOptions<TaskPrioritySection>>();
            _prioritySettingsMock.Setup(o => o.Value).Returns(new TaskPrioritySection());
            _statusSettingsMock = new Mock<IOptions<TaskCompletionSection>>();
            _statusSettingsMock.Setup(o => o.Value).Returns(new TaskCompletionSection());
        }
    }
}
