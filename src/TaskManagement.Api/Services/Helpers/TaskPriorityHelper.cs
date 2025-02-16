using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Options;
using System.Linq.Expressions;
using TaskManagement.Api.Common.Configuration.Settings.Sections;
using TaskManagement.Api.Models;
using TaskManagement.Api.Models.Enums;

namespace TaskManagement.Api.Services.Helpers
{
    public class TaskPriorityHelper : ITaskPriorityHelper
    {
        private readonly TaskPrioritySection _settings;

        public TaskPriorityHelper(IOptions<TaskPrioritySection> options)
        {
            _settings = options.Value;
        }

        public TaskPriority GetPriority(DateTime dueDate)
        {
            var timeRemaining = dueDate - DateTime.UtcNow;

            if (timeRemaining.TotalDays <= _settings.UrgentDaysLimt)
                return TaskPriority.Urgent;
            if (timeRemaining.TotalDays <= _settings.NormalDaysLimit)
                return TaskPriority.Normal;
            return TaskPriority.Low;
        }

        public (string sql, List<SqliteParameter> sqlParams) GetPrioritySQL(string? tableAlias = null)
        {
            tableAlias = tableAlias == null ? string.Empty : $"{tableAlias}.";
            var query = @$"
                CASE 
                    WHEN (julianday({tableAlias}DueDateTimeUtc) - julianday(datetime('now'))) <= @UrgentDaysLimit THEN 0 
                    WHEN (julianday({tableAlias}DueDateTimeUtc) - julianday(datetime('now'))) <= @NormalDaysLimit THEN 1
                    ELSE 2
                END";

            List<SqliteParameter> sqlParams = new()
            {
                new SqliteParameter("@UrgentDaysLimit", _settings.UrgentDaysLimt),
                new SqliteParameter("@NormalDaysLimit", _settings.NormalDaysLimit)
            };

            return (query, sqlParams);
        }
    }
}
