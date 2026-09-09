using WorkerTemplate.Models;

namespace WorkerTemplate.Interfaces;

public interface IPersistorService
{
    Task<RabbitmqHandlerResult> SaveToDbAsync(
        DemoModel payload,
        int retryCount,
        CancellationToken cancellationToken = default);
}
