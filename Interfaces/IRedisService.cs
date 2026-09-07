using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WorkerTemplate.Models;

namespace WorkerTemplate.Interfaces
{
    public interface IRedisService
    {
        Task ConnectAsync(CancellationToken cancellationToken = default);
        Task<bool> CheckConnectionAsync(CancellationToken cancellationToken);
        Task<string?> GetStringAsync(
            string key,
            CancellationToken cancellationToken = default);
        Task<bool> SetStringAsync(
            string key, string value,
            TimeSpan? expiry = null,
            CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(
            string key,
            CancellationToken cancellationToken = default);
        Task<bool> KeyExistsAsync(
            string key,
            CancellationToken cancellationToken = default);

        Task<string?> HashGetAsync(
            string key,
            string field,
            CancellationToken cancellationToken = default);
        Task<bool> HashSetAsync(
            string key,
            string field,
            string value,
            CancellationToken cancellationToken = default);
        Task<bool> HashDeleteAsync(
            string key,
            string field,
            CancellationToken cancellationToken = default);
        Task<bool> HashExistsAsync(
            string key,
            string field,
            CancellationToken cancellationToken = default);
    }
}
