using Serilog;
using System.Reflection;
using System.Runtime.InteropServices;
using WorkerTemplate;
using WorkerTemplate.Configs;
using WorkerTemplate.Providers;
using WorkerTemplate.Services;


namespace WorkerTemplate
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Setup Serilog to read from appsettings.json BEFORE Host is built
            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .Build())
                .CreateLogger();

            try
            {
                var assembly = Assembly.GetEntryAssembly();
                var serviceName = assembly?.GetName().Name;
                var version = assembly?.GetName().Version;

                Log.Information("Starting service {ServiceName}, version {Version}", serviceName, version);

                var builder = Host.CreateDefaultBuilder(args)
                    .UseSerilog() // Important to apply Serilog here
                    .UseConsoleLifetime()
                    .ConfigureServices((hostContext, services) =>
                    {
                        // Bind both RabbitMQ and Postgres settings
                        services.Configure<RabbitMQSettings>(hostContext.Configuration.GetSection("RabbitMQ"));
                        services.Configure<PostgreSQLSettings>(hostContext.Configuration.GetSection("PostgreSQL"));
                        services.Configure<HashSettings>(hostContext.Configuration.GetSection("Hash"));
                        services.Configure<MerchantSettings>(hostContext.Configuration.GetSection("Merchant"));

                        // Add your services as singleton
                        services.AddSingleton<RabbitMQService>();
                        services.AddSingleton<PostgresService>();

                        // TxnService orchestrates consuming and DB inserts
                        services.AddSingleton<TxnService>();

                        // Add the worker
                        services.AddHostedService<Worker>();
                    });

                if (!System.Diagnostics.Debugger.IsAttached && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    builder.UseWindowsService();
                }

                builder.Build().Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Application failed to start correctly.");
            }
            finally
            {
                Log.CloseAndFlush(); // Always flush Serilog
            }
        }
    }
}
