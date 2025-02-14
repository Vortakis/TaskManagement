
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using TaskManagement.Api.Common.Configuration.Settings.Sections;
using TaskManagement.Api.Models;
using TaskManagement.Api.Models.Enums;

namespace TaskManagement.Api.Services.Handlers
{
    public class CompletionStatusHandler : ICompletionStatusHandler
    {
        private readonly TaskCompletionSection _settings;

        public CompletionStatusHandler(IOptions<TaskCompletionSection> options)
        {
            _settings = options.Value;
        }

        public string IsValid(CompletionStatus from, CompletionStatus to, DateTime dateTime)
        {
            return CheckTransition(from, to) ?? CheckCompletionDueDate(to, dateTime);
        }

        public Expression<Func<TaskModel, bool>> IsValidStatusFilter(CompletionStatus to)
        {
            var limitDate = DateTime.UtcNow.AddDays(_settings.EarlyCompletionLimit);

            return task => ((task.Status == CompletionStatus.Pending && (to == CompletionStatus.InProgress || to == CompletionStatus.Completed)) ||
                            (task.Status == CompletionStatus.InProgress && (to == CompletionStatus.Pending || to == CompletionStatus.Completed)))
                         && !(to == CompletionStatus.Completed && task.DueDateTimeUtc > limitDate);
        }

        public (string sql, SqliteParameter sqlParam) IsValidStatusSQL(string? tableAlias = null)
        {
            tableAlias = tableAlias == null ? string.Empty : $"{tableAlias}.";

            var query = @$"
                (({tableAlias}Status = 0 AND (@toStatus IN (1, 2)))
                OR ({tableAlias}Status = 1 AND (@toStatus IN (0, 2))))
                AND NOT (@toStatus = 2 AND {tableAlias}DueDateTimeUtc > datetime('now', '+' || @EarlyCompletion || ' days'))";

            SqliteParameter sqlParam = new SqliteParameter("@EarlyCompletion", _settings.EarlyCompletionLimit);

            return (query, sqlParam);
        }

        private string CheckTransition(CompletionStatus from, CompletionStatus to)
        {
            if (IsTransitionValid(from, to))
                return null!;

            return $"Task Status cannot be changed from '{from}' to '{to}'.";
        }

        private string CheckCompletionDueDate(CompletionStatus to, DateTime dateTime)
        {
            if (to == CompletionStatus.Completed && dateTime > DateTime.UtcNow.AddDays(_settings.EarlyCompletionLimit))
            {
                return "Cannot mark task as 'Completed' if due date is more than 3 days ahead.";
            }
            return null!;
        }

        private bool IsTransitionValid(CompletionStatus from, CompletionStatus to)
        {
            return (from == CompletionStatus.Pending && (to == CompletionStatus.InProgress || to == CompletionStatus.Completed))
                || (from == CompletionStatus.InProgress && (to == CompletionStatus.Pending || to == CompletionStatus.Completed));
        }
    }
}
