using Microsoft.Data.Sqlite;
using System.Linq.Expressions;
using TaskManagement.Api.Models;
using TaskManagement.Api.Models.Enums;

namespace TaskManagement.Api.Services.Handlers
{
    public interface ICompletionStatusHandler
    {
        string IsValid(CompletionStatus from, CompletionStatus to, DateTime dateTime);

        Expression<Func<TaskModel, bool>> IsValidStatusFilter(CompletionStatus to);

        (string sql, SqliteParameter sqlParam) IsValidStatusSQL(string? tableAlias = null);
    }
}
