namespace WorkerTemplate.Interfaces
{
    public interface ISqlServerService
    {
        Task<bool> CheckConnectionAsync(
            CancellationToken cancellationToken);

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
