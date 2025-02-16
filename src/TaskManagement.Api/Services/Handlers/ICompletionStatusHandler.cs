using Microsoft.Data.Sqlite;
using System.Linq.Expressions;
using TaskManagement.Api.Models;
using TaskManagement.Api.Models.Enums;

namespace TaskManagement.Api.Services.Handlers
{
    public interface ICompletionStatusHandler
    {
        string ValidateStatus(CompletionStatus from, CompletionStatus to, DateTime dueDateTime);

        (string sql, SqliteParameter sqlParam) ValidateStatusSQL(string? tableAlias = null);
    }
}
