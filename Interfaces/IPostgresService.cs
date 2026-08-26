using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WorkerTemplate.Models;

namespace WorkerTemplate.Interfaces
{
    public interface IPostgresService
    {
        Task<bool> CheckConnectionAsync(CancellationToken cancellationToken);

        Task<IEnumerable<T>> QueryAsync<T>(
            string sql,
            object? parameters = null,
            CancellationToken cancellationToken = default);

        Task<int> ExecuteAsync(
            string sql,
            object? parameters = null,
            CancellationToken cancellationToken = default);
    }
}
