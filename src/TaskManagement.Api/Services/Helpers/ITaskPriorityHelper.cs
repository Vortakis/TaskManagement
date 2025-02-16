using Microsoft.Data.Sqlite;
using TaskManagement.Api.Models.Enums;

namespace TaskManagement.Api.Services.Helpers
{
    public interface ITaskPriorityHelper
    {
        TaskPriority GetPriority(DateTime dueDate);

        public (string sql, List<SqliteParameter> sqlParams) GetPrioritySQL(string? tableAlias = null);
    }
}
