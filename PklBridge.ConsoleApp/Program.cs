using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PklBridge.Core.Configuration;
using PklBridge.Core.Interfaces;
using PklBridge.Infrastructure;
using PklBridge.Infrastructure.Api;
using PklBridge.Infrastructure.Serial;
using PklBridge.ConsoleApp;
using Serilog;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;
Console.WriteLine("🚀 PKL Bridge - Aplicação de Console para Testes");
Console.WriteLine("===============================================");
Console.WriteLine();

var host = CreateHostBuilder(args).Build();

try
{
    Console.WriteLine("Iniciando PKL Bridge Service...");
    
    // Para testes em console, não usar como Windows Service
    await host.StartAsync();
    
    Console.WriteLine("✅ PKL Bridge iniciado com sucesso!");
    Console.WriteLine();
    Console.WriteLine("🌐 TCP Server ativo na porta 8081");
    Console.WriteLine("📡 Named Pipe ativo: \\\\.\\pipe\\pkl_serial");
    Console.WriteLine("🔌 Aguardando conexão do HLAB...");
    Console.WriteLine();
    Console.WriteLine("📊 Modo Monitor ativo");
    Console.WriteLine("Pressione 'q' + ENTER para sair");
    Console.WriteLine("Pressione qualquer outra tecla + ENTER para status");
    Console.WriteLine();

    // Loop para manter console ativo
    while (true)
    {
        var input = Console.ReadLine();
        
        if (input?.ToLower() == "q")
        {
            Console.WriteLine("Encerrando PKL Bridge...");
            break;
        }
        
        // Mostrar status
        Console.WriteLine($"⏰ Status: {DateTime.Now:HH:mm:ss} - PKL Bridge rodando");
        Console.WriteLine($"💾 Memória: {GC.GetTotalMemory(false) / (1024 * 1024)}MB");
        Console.WriteLine();
    }
}
catch (Exception ex)
{
    Console.WriteLine($"❌ Erro: {ex.Message}");
    Console.WriteLine("Pressione qualquer tecla para sair...");
    Console.ReadKey();
}
finally
{
    await host.StopAsync();
    Console.WriteLine("✋ PKL Bridge encerrado.");
}

static IHostBuilder CreateHostBuilder(string[] args) =>
    Host.CreateDefaultBuilder(args)
        .UseSerilog((context, services, configuration) =>
        {
            configuration
                .ReadFrom.Configuration(context.Configuration)
                .ReadFrom.Services(services)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("ServiceName", "PKLBridge-Console")
                .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(
                    "logs/pkl-bridge-console-.log",
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 7,
                    shared: true
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
            
            // API VIDA
            services.AddHttpClient<IVidaApiClient, VidaApiClient>();
            
            services.AddScoped<IAstmMessageBuilder, AstmMessageBuilder>();
            services.AddScoped<IExamOrderService, ExamOrderService>();

            // Register infrastructure services
            services.AddSingleton<PklNamedPipeServer>();
            services.AddSingleton<SerialPortClient>();
            services.AddSingleton<SerialBridge>();
            services.AddSingleton<SerialPortMonitor>(); // Monitor para COM2
            services.AddSingleton<TcpServer>();         // usado pelo TcpAstmTransport
            services.AddSingleton<TcpAstmTransport>();
            services.AddSingleton<SerialAstmTransport>();

            // Selecionar o transporte ASTM conforme TransportMode em BridgeSettings
            var bridgeSection = configuration.GetSection(BridgeSettings.SectionName).Get<BridgeSettings>() ?? new BridgeSettings();
            services.AddSingleton<IAstmTransport>(sp => bridgeSection.TransportMode switch
            {
                TransportMode.Serial => sp.GetRequiredService<SerialAstmTransport>(),
                TransportMode.Tcp    => sp.GetRequiredService<TcpAstmTransport>(),
                _ => throw new InvalidOperationException($"TransportMode inválido: {bridgeSection.TransportMode}")
            });

            services.AddSingleton<AstmSessionManager>(); // ASTM Session Manager
            services.AddSingleton<AstmMessageBuilder>(); // ASTM Message Builder (singleton para ExamRequestService)
            services.AddSingleton<ExamRequestService>(); // Exam Request Service
            services.AddSingleton<ResultProcessor>(); // Result Processor para enviar resultados para API VIDA

            // Register console services
            services.AddScoped<InteractiveMenu>();

            // Register the main worker
            services.AddHostedService<ConsoleWorker>();

            // Health checks
            services.AddHealthChecks();
        });
