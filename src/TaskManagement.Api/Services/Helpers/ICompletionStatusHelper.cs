using Microsoft.Data.Sqlite;
using System.Linq.Expressions;
using TaskManagement.Api.Models;
using TaskManagement.Api.Models.Enums;

namespace TaskManagement.Api.Services.Helpers
{
    public interface ICompletionStatusHelper
    {
        string ValidateStatus(CompletionStatus from, CompletionStatus to, DateTime dueDateTime);

        (string sql, SqliteParameter sqlParam) ValidateStatusSQL(string? tableAlias = null);
    }
}
