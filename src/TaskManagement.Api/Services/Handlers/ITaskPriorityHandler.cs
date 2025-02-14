using Microsoft.Data.Sqlite;
using TaskManagement.Api.Models.Enums;

namespace TaskManagement.Api.Services.Handlers
{
    public interface ITaskPriorityHandler
    {
        TaskPriority GetPriority(DateTime dueDate);

        public (string sql, List<SqliteParameter> sqlParams) GetPrioritySQL(string? tableAlias = null);
    }
}
