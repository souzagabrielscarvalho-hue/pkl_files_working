using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PklBridge.Core.Configuration;
using PklBridge.Core.Interfaces;
using PklBridge.Infrastructure;
using PklBridge.Infrastructure.Api;
using PklBridge.Infrastructure.Serial;
using Serilog;

namespace PklBridge.Service;

public class Program
{
    public static async Task Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        
        try
        {
            Log.Information("Starting PKL Bridge Service");
            await host.RunAsync();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "PKL Bridge Service terminated unexpectedly");
            throw;
        }
        finally
        {
            Log.Information("PKL Bridge Service stopped");
            Log.CloseAndFlush();
        }
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseWindowsService(options =>
            {
                options.ServiceName = "PKL Bridge Service";
            })
            .UseSerilog((context, services, configuration) =>
            {
                configuration
                    .ReadFrom.Configuration(context.Configuration)
                    .ReadFrom.Services(services)
                    .Enrich.FromLogContext()
                    .Enrich.WithProperty("ServiceName", "PKLBridge")
                    .WriteTo.Console()
                    .WriteTo.File(
                        "logs/pkl-bridge-.log",
                        rollingInterval: RollingInterval.Day,
                        retainedFileCountLimit: 30,
                        shared: true
                    )
                    .WriteTo.EventLog(
                        source: "PKL Bridge Service",
                        logName: "Application",
                        manageEventSource: false,
                        restrictedToMinimumLevel: Serilog.Events.LogEventLevel.Warning
                    );
            })
            .ConfigureServices((hostContext, services) =>
            {
                var configuration = hostContext.Configuration;

                // Configure settings
                services.Configure<BridgeSettings>(
                    configuration.GetSection(BridgeSettings.SectionName));
                services.Configure<VidaApiSettings>(
                    configuration.GetSection(VidaApiSettings.SectionName));
                services.Configure<AstmSettings>(
                    configuration.GetSection(AstmSettings.SectionName));
                services.Configure<ProcessingSettings>(
                    configuration.GetSection(ProcessingSettings.SectionName));
                services.Configure<HealthCheckSettings>(
                    configuration.GetSection(HealthCheckSettings.SectionName));

                // Register core services
                services.AddScoped<IAstmParser, AstmMessageParser>();
                services.AddScoped<IMessageProcessor, MessageProcessor>();
                services.AddScoped<IVidaApiClient, MockVidaApiClient>();

                // Register infrastructure services
                services.AddSingleton<PklNamedPipeServer>();
                services.AddSingleton<SerialPortClient>();
                services.AddSingleton<SerialBridge>();

                // Configure HTTP client for VIDA API
                services.AddVidaApiHttpClient(configuration);

                // Register the main worker
                services.AddHostedService<Worker>();

                // Health checks
                services.AddHealthChecks()
                    .AddCheck<PklBridgeHealthCheck>("pkl-bridge");
            });
}
