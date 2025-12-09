using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using System.Text;
using System.Net;

namespace PklHttpMonitor;

class Program
{
    private static string _logDirectory = "logs";
    private static string _responseMessage = "<ACK>";
    private static int _requestCounter = 0;

    static async Task Main(string[] args)
    {
        // Configurar logging
        ConfigureLogging();

        // Criar diretório de logs
        Directory.CreateDirectory(_logDirectory);

        // Mostrar informações iniciais
        ShowWelcomeMessage();

        // Configurar builder da aplicação
        var builder = WebApplication.CreateBuilder(args);
        
        // Adicionar Serilog
        builder.Host.UseSerilog();

        // Encontrar porta disponível
        var portOptions = new[] { 8080, 5000, 3000, 4000, 9000 };
        int availablePort = -1;

        foreach (var port in portOptions)
        {
            if (IsPortAvailable(port))
            {
                availablePort = port;
                break;
            }
            else
            {
                Console.WriteLine($"❌ Porta {port} não disponível");
            }
        }

        if (availablePort == -1)
        {
            Console.WriteLine("❌ Erro: Nenhuma porta disponível encontrada!");
            Console.WriteLine("💡 Tente:");
            Console.WriteLine("   1. Executar como Administrador");
            Console.WriteLine("   2. Fechar outros aplicativos que usam HTTP");
            Console.WriteLine("   3. Verificar firewall do Windows");
            return;
        }

        // Criar aplicação com porta disponível
        var finalBuilder = WebApplication.CreateBuilder(args);
        finalBuilder.Host.UseSerilog();
        finalBuilder.WebHost.UseUrls($"http://localhost:{availablePort}");

        var app = finalBuilder.Build();

        // Configurar middleware de captura
        app.Use(async (context, next) =>
        {
            await LogCompleteRequest(context);
            await next();
        });

        // Endpoint catch-all para capturar todas as requisições
        app.MapFallback(async (HttpContext context) =>
        {
            var response = await HandlePklRequest(context);
            return Results.Content(response, "text/plain");
        });

        // Mostrar informações do servidor
        Console.WriteLine("🚀 Iniciando servidor de monitoramento PKL...");
        Console.WriteLine($"📁 Logs salvos em: {Path.GetFullPath(_logDirectory)}");
        Console.WriteLine($"🌐 Servidor iniciado com sucesso na porta {availablePort}");
        Console.WriteLine($"   URL: http://localhost:{availablePort}");
        Console.WriteLine($"   Acesso local: http://localhost:{availablePort}");
        Console.WriteLine($"   Acesso rede: http://{GetLocalIPAddress()}:{availablePort}");
        Console.WriteLine();
        Console.WriteLine("⚡ Aguardando requisições da PKL...");
        Console.WriteLine("📝 Pressione Ctrl+C para parar");
        Console.WriteLine();
        Console.WriteLine("💡 Configure a PKL 125 para:");
        Console.WriteLine($"   - Modo: TCP/IP");
        Console.WriteLine($"   - IP: {GetLocalIPAddress()}");
        Console.WriteLine($"   - Porta: {availablePort}");
        Console.WriteLine();

        try
        {
            await app.RunAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Erro durante execução: {ex.Message}");
            Log.Error(ex, "Erro durante execução do servidor");
        }
    }

    private static void ConfigureLogging()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File(Path.Combine(_logDirectory, "pkl-monitor-.log"), 
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();
    }

    private static void ShowWelcomeMessage()
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                    PKL HTTP MONITOR                         ║");
        Console.WriteLine("║                  Servidor de Monitoramento                  ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  🎯 Objetivo: Capturar comunicação HTTP da máquina PKL      ║");
        Console.WriteLine("║  📊 Funcionalidades:                                        ║");
        Console.WriteLine("║    • Captura todas as requisições HTTP                      ║");
        Console.WriteLine("║    • Salva logs detalhados em arquivos TXT                  ║");
        Console.WriteLine("║    • Mostra informações em tempo real no console            ║");
        Console.WriteLine("║    • Responde com ACK automático                            ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
    }

    private static async Task LogCompleteRequest(HttpContext context)
    {
        var requestId = Interlocked.Increment(ref _requestCounter);
        var timestamp = DateTime.Now;
        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        // Capturar informações da requisição
        var method = context.Request.Method;
        var path = context.Request.Path;
        var queryString = context.Request.QueryString.ToString();
        var headers = context.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString());

        // Capturar body
        context.Request.EnableBuffering();
        var body = await ReadRequestBody(context.Request);
        context.Request.Body.Position = 0;

        // Criar log detalhado
        var logEntry = CreateDetailedLogEntry(requestId, timestamp, clientIp, method, path, queryString, headers, body);

        // Salvar em arquivo
        await SaveRequestToFile(requestId, timestamp, logEntry);

        // Log no console
        LogToConsole(requestId, timestamp, clientIp, method, path, body.Length);

        // Log estruturado
        Log.Information("PKL Request {RequestId} from {ClientIp}: {Method} {Path}", 
            requestId, clientIp, method, path);
    }

    private static async Task<string> ReadRequestBody(HttpRequest request)
    {
        try
        {
            using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
            return await reader.ReadToEndAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Erro ao ler body da requisição");
            return $"[ERRO AO LER BODY: {ex.Message}]";
        }
    }

    private static string CreateDetailedLogEntry(
        int requestId, DateTime timestamp, string clientIp, 
        string method, string path, string queryString, 
        Dictionary<string, string> headers, string body)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("================================================================================");
        sb.AppendLine($"PKL HTTP REQUEST #{requestId}");
        sb.AppendLine($"Timestamp: {timestamp:yyyy-MM-dd HH:mm:ss.fff}");
        sb.AppendLine($"Client IP: {clientIp}");
        sb.AppendLine("================================================================================");
        sb.AppendLine();
        
        sb.AppendLine("🌐 REQUEST INFO:");
        sb.AppendLine($"   Method: {method}");
        sb.AppendLine($"   Path: {path}");
        sb.AppendLine($"   Query: {queryString}");
        sb.AppendLine();
        
        sb.AppendLine("📋 HEADERS:");
        if (headers.Any())
        {
            foreach (var header in headers)
            {
                sb.AppendLine($"   {header.Key}: {header.Value}");
            }
        }
        else
        {
            sb.AppendLine("   (nenhum header)");
        }
        sb.AppendLine();
        
        sb.AppendLine("📄 BODY:");
        sb.AppendLine($"   Length: {body.Length} bytes");
        if (!string.IsNullOrEmpty(body))
        {
            sb.AppendLine($"   Content: {body}");
            sb.AppendLine();
            sb.AppendLine("   Hex Dump:");
            sb.AppendLine($"   {BitConverter.ToString(Encoding.UTF8.GetBytes(body)).Replace("-", " ")}");
            sb.AppendLine();
            sb.AppendLine("   ASCII Codes:");
            sb.AppendLine($"   {string.Join(" ", Encoding.UTF8.GetBytes(body).Select(b => b.ToString("D3")))}");
        }
        else
        {
            sb.AppendLine("   (corpo vazio)");
        }
        sb.AppendLine();
        
        sb.AppendLine($"📤 RESPONSE: {_responseMessage}");
        sb.AppendLine("================================================================================");
        sb.AppendLine();
        
        return sb.ToString();
    }

    private static async Task SaveRequestToFile(int requestId, DateTime timestamp, string logEntry)
    {
        var fileName = $"pkl-request-{timestamp:yyyy-MM-dd}.txt";
        var filePath = Path.Combine(_logDirectory, fileName);
        
        try
        {
            await File.AppendAllTextAsync(filePath, logEntry);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Erro ao salvar request no arquivo {FilePath}", filePath);
        }
    }

    private static void LogToConsole(int requestId, DateTime timestamp, string clientIp, string method, string path, int bodyLength)
    {
        var timeStr = timestamp.ToString("HH:mm:ss.fff");
        var methodColor = GetMethodColor(method);
        
        Console.WriteLine($"[{timeStr}] 📨 Request #{requestId:D4} from {clientIp}");
        Console.WriteLine($"          {methodColor}{method,-6}\u001b[0m {path} ({bodyLength} bytes)");
        Console.WriteLine();
    }

    private static string GetMethodColor(string method)
    {
        return method switch
        {
            "GET" => "\u001b[32m",    // Verde
            "POST" => "\u001b[33m",   // Amarelo
            "PUT" => "\u001b[34m",    // Azul
            "DELETE" => "\u001b[31m", // Vermelho
            _ => "\u001b[37m"         // Branco
        };
    }

    private static async Task<string> HandlePklRequest(HttpContext context)
    {
        // Simular pequeno delay para resposta mais realista
        await Task.Delay(100);
        
        // Responder com ACK padrão
        // Você pode modificar isso baseado no que descobrir sobre o protocolo PKL
        return _responseMessage;
    }

    private static string GetLocalIPAddress()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    return ip.ToString();
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Erro ao obter IP local");
        }
        
        return "127.0.0.1";
    }

    private static bool IsPortAvailable(int port)
    {
        try
        {
            using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch (System.Net.Sockets.SocketException)
        {
            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Erro ao verificar porta {Port}", port);
            return false;
        }
    }
}
