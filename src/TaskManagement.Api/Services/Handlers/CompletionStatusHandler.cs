
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

        public string ValidateStatus(CompletionStatus from, CompletionStatus to, DateTime dueDateTime)
        {
            return CheckTransition(from, to) ?? CheckCompletionDueDate(to, dueDateTime);
        }

        public (string sql, SqliteParameter sqlParam) ValidateStatusSQL(string? tableAlias = null)
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
            if ((from == CompletionStatus.Pending && (to == CompletionStatus.InProgress || to == CompletionStatus.Completed))
                || (from == CompletionStatus.InProgress && (to == CompletionStatus.Pending || to == CompletionStatus.Completed)))
                return null!;

            return $"Task Status cannot be changed from '{from}' to '{to}'.";
        }

        private string CheckCompletionDueDate(CompletionStatus to, DateTime dueSateTime)
        {
            if (to == CompletionStatus.Completed && dueSateTime > DateTime.UtcNow.AddDays(_settings.EarlyCompletionLimit))
            {
                return "Cannot mark task as 'Completed' if due date is more than 3 days ahead.";
            }
            return null!;
        }
    }
}
