using RabbitMQ.Client;
using WorkerTemplate.Configs;
using WorkerTemplate.Models;

namespace WorkerTemplate.Interfaces
{
    public interface IRabbitMQService : IAsyncDisposable
    {
        Task<IConnection> GetConnectionAsync(
            BrokerSettings config,
            string brokerName = "default");

        Task StartConsumingAsync(
            BrokerSettings connConfig,
            QueueConfig queueConfig,
            Func<string, int, Task<RabbitmqHandlerResult>> handler,
            string brokerName = "default",
            CancellationToken cancellationToken = default);

        Task<bool> PublishAsync(
            ExchangeConfig exchangeConfig,
            string message,
            string brokerName = "default",
            CancellationToken cancellationToken = default);

        Task EnsureRetryQueuesAsync(
            string baseQueue,
            IEnumerable<int> delayIntervalsMs,
            string brokerName = "default");
    }
}
