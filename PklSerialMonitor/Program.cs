using Microsoft.Extensions.Configuration;
using Serilog;
using Spectre.Console;
using System.IO.Ports;
using System.Text;

namespace PklSerialMonitor;

class Program
{
    private static IConfiguration? _configuration;
    private static SerialPort? _serialPort;
    private static bool _monitoring = false;
    private static int _messageCounter = 0;
    private static string _logDirectory = "logs";

    static async Task Main(string[] args)
    {
        // Configurar
        ConfigureApplication();

        // Mostrar interface
        ShowWelcomeMessage();

        // Verificar portas disponíveis
        await CheckAvailablePorts();

        // Menu principal
        await ShowMainMenu();
    }

    private static void ConfigureApplication()
    {
        // Configuração
        _configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .Build();

        // Logging
        _logDirectory = _configuration["Logging:LogDirectory"] ?? "logs";
        Directory.CreateDirectory(_logDirectory);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File(Path.Combine(_logDirectory, "pkl-serial-.log"),
                rollingInterval: RollingInterval.Day)
            .CreateLogger();

        Log.Information("PKL Serial Monitor iniciado");
    }

    private static void ShowWelcomeMessage()
    {
        AnsiConsole.Clear();
        
        var panel = new Panel(
            new Markup("[bold blue]PKL SERIAL MONITOR[/]\n\n" +
                      "[green]Monitor de Comunicação Serial PKL 125[/]\n" +
                      "[yellow]Protocolo ASTM E1394-97[/]\n\n" +
                      "🎯 [bold]Funcionalidades:[/]\n" +
                      "• Monitor em tempo real\n" +
                      "• Análise de protocolo ASTM\n" +
                      "• Simulador de testes\n" +
                      "• Logs detalhados\n" +
                      "• Validação de checksum"))
        {
            Header = new PanelHeader("🔍 INTERFACE PKL HEMOGRAMA"),
            Border = BoxBorder.Rounded
        };

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    private static async Task CheckAvailablePorts()
    {
        AnsiConsole.Write(new Rule("[yellow]VERIFICAÇÃO DE PORTAS SERIAIS[/]"));
        AnsiConsole.WriteLine();

        var availablePorts = SerialPort.GetPortNames();
        
        if (availablePorts.Length == 0)
        {
            AnsiConsole.MarkupLine("[red]❌ Nenhuma porta serial encontrada![/]");
            return;
        }

        var table = new Table();
        table.AddColumn("Porta");
        table.AddColumn("Status");
        table.AddColumn("Descrição");

        var configuredPort = _configuration?["Serial:PortName"] ?? "COM2";

        foreach (var port in availablePorts)
        {
            var status = port == configuredPort ? "[green]Configurada[/]" : "[gray]Disponível[/]";
            var description = port == configuredPort ? "Porta configurada para PKL" : "Porta disponível";
            
            table.AddRow(port, status, description);
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();

        if (!availablePorts.Contains(configuredPort))
        {
            AnsiConsole.MarkupLine($"[yellow]⚠️ Porta configurada {configuredPort} não encontrada![/]");
            AnsiConsole.MarkupLine("[gray]Altere a configuração em appsettings.json[/]");
        }
    }

    private static async Task ShowMainMenu()
    {
        while (true)
        {
            AnsiConsole.Write(new Rule("[cyan]MENU PRINCIPAL[/]"));
            AnsiConsole.WriteLine();
            
            AnsiConsole.MarkupLine("[yellow]Escolha uma opção:[/]");
            AnsiConsole.MarkupLine("[green]1.[/] Iniciar Monitor Serial");
            AnsiConsole.MarkupLine("[green]2.[/] Testar Comunicação");
            AnsiConsole.MarkupLine("[green]3.[/] Enviar Comando ASTM");
            AnsiConsole.MarkupLine("[green]4.[/] Verificar Configuração");
            AnsiConsole.MarkupLine("[green]5.[/] Ver Logs");
            AnsiConsole.MarkupLine("[green]6.[/] Sair");
            AnsiConsole.WriteLine();
            AnsiConsole.Markup("[cyan]Digite sua escolha (1-6): [/]");

            var input = Console.ReadLine();
            if (string.IsNullOrEmpty(input))
                continue;

            switch (input.Trim())
            {
                case "1":
                    await StartSerialMonitor();
                    break;
                case "2":
                    await TestCommunication();
                    break;
                case "3":
                    await SendAstmCommand();
                    break;
                case "4":
                    ShowConfiguration();
                    break;
                case "5":
                    ShowLogs();
                    break;
                case "6":
                    await StopMonitoring();
                    Environment.Exit(0);
                    break;
                default:
                    AnsiConsole.MarkupLine("[red]Opção inválida! Digite um número de 1 a 6.[/]");
                    AnsiConsole.MarkupLine("Pressione qualquer tecla para continuar...");
                    Console.ReadKey();
                    break;
            }
        }
    }

    private static async Task StartSerialMonitor()
    {
        if (_monitoring)
        {
            AnsiConsole.MarkupLine("[yellow]Monitor já está ativo![/]");
            AnsiConsole.MarkupLine("Pressione qualquer tecla para continuar...");
            Console.ReadKey();
            return;
        }

        try
        {
            var portName = _configuration?["Serial:PortName"] ?? "COM2";
            var baudRate = int.Parse(_configuration?["Serial:BaudRate"] ?? "19200");

            _serialPort = new SerialPort(portName, baudRate);
            _serialPort.DataBits = 8;
            _serialPort.Parity = Parity.None;
            _serialPort.StopBits = StopBits.One;
            _serialPort.Handshake = Handshake.None;
            _serialPort.ReadTimeout = 5000;
            _serialPort.WriteTimeout = 5000;

            _serialPort.DataReceived += OnDataReceived;
            _serialPort.ErrorReceived += OnErrorReceived;

            _serialPort.Open();
            _monitoring = true;

            AnsiConsole.MarkupLine($"[green]✅ Monitor iniciado na porta {portName}[/]");
            AnsiConsole.MarkupLine("[cyan]Aguardando dados da PKL...[/]");
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[gray]Pressione 'q' para parar o monitor[/]");

            // Loop de monitoramento
            while (_monitoring)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true);
                    if (key.KeyChar == 'q' || key.KeyChar == 'Q')
                    {
                        await StopMonitoring();
                        break;
                    }
                }
                await Task.Delay(100);
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]❌ Erro ao iniciar monitor: {ex.Message}[/]");
            Log.Error(ex, "Erro ao iniciar monitor serial");
        }

        AnsiConsole.MarkupLine("Pressione qualquer tecla para continuar...");
        Console.ReadKey();
    }

    private static void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        if (_serialPort == null) return;

        try
        {
            var data = _serialPort.ReadExisting();
            if (!string.IsNullOrEmpty(data))
            {
                var messageId = Interlocked.Increment(ref _messageCounter);
                ProcessReceivedData(data, messageId, "RX");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Erro ao processar dados recebidos");
        }
    }

    private static void OnErrorReceived(object sender, SerialErrorReceivedEventArgs e)
    {
        Log.Error("Erro na porta serial: {Error}", e.EventType);
        AnsiConsole.MarkupLine($"[red]❌ Erro na porta serial: {e.EventType}[/]");
    }

    private static void ProcessReceivedData(string data, int messageId, string direction)
    {
        var timestamp = DateTime.Now;
        var logEntry = CreateLogEntry(messageId, timestamp, direction, data);

        // Log no console
        LogToConsole(messageId, timestamp, direction, data);

        // Log em arquivo
        SaveToFile(logEntry);

        // Log estruturado
        Log.Information("Serial {Direction} Message {MessageId}: {Data}", 
            direction, messageId, data.Replace("\r", "\\r").Replace("\n", "\\n"));
    }

    private static void LogToConsole(int messageId, DateTime timestamp, string direction, string data)
    {
        var timeStr = timestamp.ToString("HH:mm:ss.fff");
        var directionColor = direction == "TX" ? "green" : "cyan";
        var arrow = direction == "TX" ? "→" : "←";
        
        AnsiConsole.MarkupLine($"[gray][{timeStr}][/] [{directionColor}]{direction} {arrow} PKL[/]: {EscapeMarkup(data)}");
        
        // Análise ASTM se contém STX
        if (data.Contains('\x02'))
        {
            AnalyzeAstmMessage(data, messageId, direction);
        }
    }

    private static void AnalyzeAstmMessage(string data, int messageId, string direction)
    {
        try
        {
            var analysis = AstmParser.Parse(data);
            if (analysis != null)
            {
                AnsiConsole.MarkupLine($"[yellow]📋 ASTM Analysis #{messageId}:[/]");
                AnsiConsole.MarkupLine($"   Frame: {analysis.FrameNumber}");
                AnsiConsole.MarkupLine($"   Type: {analysis.RecordType}");
                AnsiConsole.MarkupLine($"   Checksum: {analysis.Checksum} {(analysis.IsValidChecksum ? "[green](Valid)[/]" : "[red](Invalid)[/]")}");
                if (!string.IsNullOrEmpty(analysis.ParsedContent))
                {
                    AnsiConsole.MarkupLine($"   Content: {EscapeMarkup(analysis.ParsedContent)}");
                }
                AnsiConsole.WriteLine();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Erro ao analisar mensagem ASTM");
        }
    }

    private static string EscapeMarkup(string text)
    {
        return text.Replace("[", "[[").Replace("]", "]]");
    }

    private static string CreateLogEntry(int messageId, DateTime timestamp, string direction, string data)
    {
        var sb = new StringBuilder();
        
        sb.AppendLine("================================================================================");
        sb.AppendLine($"PKL SERIAL MESSAGE #{messageId}");
        sb.AppendLine($"Timestamp: {timestamp:yyyy-MM-dd HH:mm:ss.fff}");
        sb.AppendLine($"Direction: {direction} ({(direction == "TX" ? "Sent to PKL" : "Received from PKL")})");
        sb.AppendLine("================================================================================");
        sb.AppendLine();
        
        sb.AppendLine("📄 RAW DATA:");
        sb.AppendLine($"   Length: {data.Length} bytes");
        sb.AppendLine($"   Content: {data}");
        sb.AppendLine();
        
        sb.AppendLine("🔍 HEX DUMP:");
        sb.AppendLine($"   {BitConverter.ToString(Encoding.ASCII.GetBytes(data)).Replace("-", " ")}");
        sb.AppendLine();
        
        sb.AppendLine("📊 ASCII CODES:");
        sb.AppendLine($"   {string.Join(" ", Encoding.ASCII.GetBytes(data).Select(b => b.ToString("D3")))}");
        sb.AppendLine();

        // Análise ASTM
        if (data.Contains('\x02'))
        {
            try
            {
                var analysis = AstmParser.Parse(data);
                if (analysis != null)
                {
                    sb.AppendLine("🧬 ASTM ANALYSIS:");
                    sb.AppendLine($"   Frame Number: {analysis.FrameNumber}");
                    sb.AppendLine($"   Record Type: {analysis.RecordType}");
                    sb.AppendLine($"   Checksum: {analysis.Checksum}");
                    sb.AppendLine($"   Valid Checksum: {analysis.IsValidChecksum}");
                    if (!string.IsNullOrEmpty(analysis.ParsedContent))
                    {
                        sb.AppendLine($"   Parsed Content: {analysis.ParsedContent}");
                    }
                    sb.AppendLine();
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine("🧬 ASTM ANALYSIS:");
                sb.AppendLine($"   Error: {ex.Message}");
                sb.AppendLine();
            }
        }
        
        sb.AppendLine("================================================================================");
        sb.AppendLine();
        
        return sb.ToString();
    }

    private static void SaveToFile(string logEntry)
    {
        try
        {
            var fileName = $"pkl-serial-{DateTime.Now:yyyy-MM-dd}.txt";
            var filePath = Path.Combine(_logDirectory, fileName);
            File.AppendAllText(filePath, logEntry);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Erro ao salvar log em arquivo");
        }
    }

    private static async Task TestCommunication()
    {
        AnsiConsole.Write(new Rule("[yellow]TESTE DE COMUNICAÇÃO[/]"));
        AnsiConsole.WriteLine();

        if (_serialPort == null || !_serialPort.IsOpen)
        {
            AnsiConsole.MarkupLine("[red]❌ Porta serial não está aberta![/]");
            AnsiConsole.MarkupLine("Inicie o monitor primeiro (opção 1)");
            AnsiConsole.MarkupLine("Pressione qualquer tecla para continuar...");
            Console.ReadKey();
            return;
        }

        AnsiConsole.MarkupLine("[cyan]Enviando ENQ (Enquiry)...[/]");
        
        try
        {
            var enq = new byte[] { 0x05 }; // ENQ
            await _serialPort.BaseStream.WriteAsync(enq);
            
            var messageId = Interlocked.Increment(ref _messageCounter);
            ProcessReceivedData("\x05", messageId, "TX");
            
            AnsiConsole.MarkupLine("[green]✅ ENQ enviado com sucesso![/]");
            AnsiConsole.MarkupLine("Aguardando resposta da PKL...");
            
            // Aguardar resposta por alguns segundos
            await Task.Delay(3000);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]❌ Erro ao enviar ENQ: {ex.Message}[/]");
            Log.Error(ex, "Erro ao enviar teste de comunicação");
        }

        AnsiConsole.MarkupLine("Pressione qualquer tecla para continuar...");
        Console.ReadKey();
    }

    private static async Task SendAstmCommand()
    {
        // Implementar envio de comandos ASTM personalizados
        AnsiConsole.MarkupLine("[yellow]Funcionalidade em desenvolvimento...[/]");
        AnsiConsole.MarkupLine("Pressione qualquer tecla para continuar...");
        Console.ReadKey();
    }

    private static void ShowConfiguration()
    {
        AnsiConsole.Write(new Rule("[yellow]CONFIGURAÇÃO ATUAL[/]"));
        AnsiConsole.WriteLine();

        var table = new Table();
        table.AddColumn("Configuração");
        table.AddColumn("Valor");

        table.AddRow("Porta Serial", _configuration?["Serial:PortName"] ?? "COM2");
        table.AddRow("Baud Rate", _configuration?["Serial:BaudRate"] ?? "19200");
        table.AddRow("Data Bits", _configuration?["Serial:DataBits"] ?? "8");
        table.AddRow("Parity", _configuration?["Serial:Parity"] ?? "None");
        table.AddRow("Stop Bits", _configuration?["Serial:StopBits"] ?? "One");
        table.AddRow("Diretório de Logs", _logDirectory);

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("Pressione qualquer tecla para continuar...");
        Console.ReadKey();
    }

    private static void ShowLogs()
    {
        AnsiConsole.Write(new Rule("[yellow]LOGS RECENTES[/]"));
        AnsiConsole.WriteLine();

        try
        {
            var logFiles = Directory.GetFiles(_logDirectory, "pkl-serial-*.txt")
                .OrderByDescending(f => File.GetLastWriteTime(f))
                .Take(5);

            if (!logFiles.Any())
            {
                AnsiConsole.MarkupLine("[yellow]Nenhum log encontrado[/]");
            }
            else
            {
                foreach (var file in logFiles)
                {
                    var info = new FileInfo(file);
                    AnsiConsole.MarkupLine($"📄 {Path.GetFileName(file)} - {info.Length} bytes - {info.LastWriteTime:dd/MM/yyyy HH:mm:ss}");
                }
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Erro ao listar logs: {ex.Message}[/]");
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("Pressione qualquer tecla para continuar...");
        Console.ReadKey();
    }

    private static async Task StopMonitoring()
    {
        if (_monitoring)
        {
            _monitoring = false;
            
            if (_serialPort != null && _serialPort.IsOpen)
            {
                _serialPort.Close();
                _serialPort.Dispose();
            }
            
            AnsiConsole.MarkupLine("[yellow]🛑 Monitor parado[/]");
            Log.Information("Monitor serial parado");
        }
    }
}

// Classe para análise ASTM
public static class AstmParser
{
    public static AstmMessage? Parse(string data)
    {
        if (string.IsNullOrEmpty(data) || !data.Contains('\x02'))
            return null;

        try
        {
            var stxIndex = data.IndexOf('\x02');
            if (stxIndex == -1) return null;

            var etxIndex = data.IndexOf('\x03', stxIndex);
            if (etxIndex == -1) return null;

            var frameNumber = data[stxIndex + 1];
            var content = data.Substring(stxIndex + 2, etxIndex - stxIndex - 2);
            var recordType = content.Length > 0 ? content[0].ToString() : "";

            // Extrair checksum se presente
            var checksum = "";
            if (etxIndex + 2 < data.Length)
            {
                checksum = data.Substring(etxIndex + 1, 2);
            }

            // Validar checksum
            var calculatedChecksum = CalculateChecksum(data.Substring(stxIndex + 1, etxIndex - stxIndex));
            var isValidChecksum = checksum.Equals(calculatedChecksum, StringComparison.OrdinalIgnoreCase);

            return new AstmMessage
            {
                FrameNumber = frameNumber.ToString(),
                RecordType = recordType,
                Content = content,
                Checksum = checksum,
                CalculatedChecksum = calculatedChecksum,
                IsValidChecksum = isValidChecksum,
                ParsedContent = ParseRecordContent(recordType, content)
            };
        }
        catch
        {
            return null;
        }
    }

    private static string CalculateChecksum(string message)
    {
        int sum = 0;
        foreach (char c in message)
        {
            sum += (int)c;
        }
        return (sum % 256).ToString("X2");
    }

    private static string ParseRecordContent(string recordType, string content)
    {
        var fields = content.Split('|');
        
        return recordType switch
        {
            "H" => $"Header - Sender: {(fields.Length > 4 ? fields[4] : "")}, Receiver: {(fields.Length > 9 ? fields[9] : "")}",
            "P" => $"Patient - ID: {(fields.Length > 2 ? fields[2] : "")}, Name: {(fields.Length > 5 ? fields[5] : "")}",
            "O" => $"Order - Sample: {(fields.Length > 2 ? fields[2] : "")}, Tests: {(fields.Length > 4 ? fields[4] : "")}",
            "R" => $"Result - Test: {(fields.Length > 2 ? fields[2] : "")}, Value: {(fields.Length > 3 ? fields[3] : "")}",
            "Q" => $"Query - Sample: {(fields.Length > 2 ? fields[2] : "")}",
            "L" => "Terminator",
            _ => "Unknown record type"
        };
    }
}

public class AstmMessage
{
    public string FrameNumber { get; set; } = "";
    public string RecordType { get; set; } = "";
    public string Content { get; set; } = "";
    public string Checksum { get; set; } = "";
    public string CalculatedChecksum { get; set; } = "";
    public bool IsValidChecksum { get; set; }
    public string ParsedContent { get; set; } = "";
}
