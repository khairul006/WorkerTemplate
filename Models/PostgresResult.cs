using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OBUTxnPst.Models
{
    public class PostgresResult
    {
        public DataTable Rows { get; set; } = new DataTable();
        public int RowsAffected { get; set; } = 0;
    }
}
