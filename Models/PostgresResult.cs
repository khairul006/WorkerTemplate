using System.Data;

namespace WorkerTemplate.Models
{
    public class PostgresResult
    {
        public DataTable Rows { get; set; } = new DataTable();
        public int RowsAffected { get; set; } = 0;
    }
}
