using Serilog;
using System.Reflection;
using System.Runtime.InteropServices;
using WorkerTemplate.Configs;
using WorkerTemplate.Interfaces;
using WorkerTemplate.Providers;
using WorkerTemplate.Services;
using WorkerTemplate.Workers;


namespace WorkerTemplate;

public class Program
{
    public static void Main(string[] args)
    {
        // Config file path by priority in 1) /app/config/appsettings.json (Azure) 2) /appsettings.json (on-prem)
        string configPath = File.Exists("/app/config/appsettings.json")
            ? "/app/config/appsettings.json"
            : Path.Combine(AppContext.BaseDirectory, "appsettings.json");

        if (File.Exists(configPath))
        {
            var fi = new FileInfo(configPath);
            Console.WriteLine($"[BOOT] Using file: {fi.FullName}, Size={fi.Length} bytes, LastWriteTimeUtc={fi.LastWriteTimeUtc}");
        }
        else
        {
            Console.WriteLine($"[BOOT-FATAL] configPath resolved to {configPath} but file does not exist!");
        }

        // Setup Serilog to read from appsettings.json BEFORE Host is built
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(new ConfigurationBuilder()
                .AddJsonFile(configPath, optional: false, reloadOnChange: true)
                .Build())
            .CreateLogger();

        try
        {
            var assembly = Assembly.GetEntryAssembly();
            var serviceName = assembly?.GetName().Name;
            var version = assembly?.GetName().Version;

            Log.Information("Starting service {ServiceName}, version {Version}", serviceName, version);
            Log.Information("Using config file: {ConfigPath}", configPath);

            var builder = Host.CreateDefaultBuilder(args)
               .ConfigureAppConfiguration((hostContext, config) =>
               {
                   config.Sources.Clear(); // optional: drop the default appsettings.json/env-specific/env-var chain if you want configPath to be authoritative
                   config.AddJsonFile(configPath, optional: false, reloadOnChange: true);
                   config.AddEnvironmentVariables(); // keep env var overrides if you use them
               })
               .UseSerilog()
               .UseConsoleLifetime()
               .ConfigureServices((hostContext, services) =>
               {
                   // Bind both RabbitMQ and Postgres settings
                   services.Configure<RabbitMQSettings>(hostContext.Configuration.GetSection("RabbitMQ"));
                   services.Configure<PostgreSQLSettings>(hostContext.Configuration.GetSection("PostgreSQL"));
                   services.Configure<RedisSettings>(hostContext.Configuration.GetSection("Redis"));
                   services.Configure<ElasticSearchSettings>(hostContext.Configuration.GetSection("ElasticSearch")); // Commented if not used
                   services.Configure<ApplicationSettings>(hostContext.Configuration.GetSection("Application"));

                   // Add core services (shared)
                   services.AddSingleton<IRabbitMQService, RabbitMQService>();
                   services.AddSingleton<IPostgresService, PostgresService>();
                   services.AddSingleton<IRedisService, RedisService>();
                   services.AddSingleton<ElasticSearchService>();

                   // Turn into a Transient service so it's isolated per message
                   services.AddTransient<IDemoRetryService, DemoRetryService>();
                   //services.AddTransient<ITxnService, TxnService>();
                   //services.AddTransient<IPersistorService, PersistorService>();

                   // Register HttpClient (needed for external API calls)
                   services.AddHttpClient<ElasticSearchService>();
                   // Register HttpClient directly mapping the Interface to the Service implementation
                   //var appSettings = hostContext.Configuration.GetSection("Application").Get<ApplicationSettings>();
                   //services.AddHttpClient<ITxnService, TxnService>() // htppClient register with TxnService as Transient instead of singleton
                   //    .ConfigurePrimaryHttpMessageHandler(() =>
                   //    {
                   //        var handler = new HttpClientHandler();
                   //        if (appSettings?.IgnoreServerCert == true)
                   //        {
                   //            handler.ServerCertificateCustomValidationCallback =
                   //                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                   //        }
                   //        return handler;
                   //    });

                   // Add worker
                   services.AddHostedService<DemoWorker>();
                   services.AddHostedService<DemoRetryWorker>();

                   // Read queue config directly from hostContext
                   //var queues = hostContext.Configuration.GetSection("RabbitMQ:Default:Queues");
                   //var clientEnabled = queues.GetValue<bool>("Client:Enabled");
                   //var persistorEnabled = queues.GetValue<bool>("Persistor:Enabled");

                   //if (clientEnabled)
                   //    services.AddHostedService<ClientWorker>();

                   //if (persistorEnabled)
                   //    services.AddHostedService<PersistorWorker>();
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