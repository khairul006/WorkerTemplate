using OBUTxnPst;
using OBUTxnPst.Configs;
using OBUTxnPst.Providers;
using Serilog;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Serilog.Events;


namespace OBUTxnPst
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
                Log.Information("Starting up the service");

                var builder = Host.CreateDefaultBuilder(args)
                    .UseSerilog() // Important to apply Serilog here
                    .UseConsoleLifetime()
                    .ConfigureAppConfiguration((hostingContext, config) =>
                    {
                        config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
                    })
                    .ConfigureServices((hostContext, services) =>
                    {
                        // Bind both RabbitMQ and Postgres settings
                        services.Configure<RabbitMQSettings>(hostContext.Configuration.GetSection("RabbitMQ"));
                        services.Configure<PostgreSQLSettings>(hostContext.Configuration.GetSection("PostgreSQL"));

                        // Add your services as singleton
                        services.AddSingleton<QueueManager>();
                        services.AddSingleton<OBUService>();

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
