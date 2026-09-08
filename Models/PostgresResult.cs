using System.Data;

namespace WorkerTemplate.Models;

public class PostgresResult<T>
{
    public List<T> Rows { get; set; } = [];
    public int RowsAffected { get; set; }
}