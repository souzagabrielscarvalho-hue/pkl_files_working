using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PklBridge.Core.Configuration;
using PklBridge.Core.Interfaces;
using PklBridge.Infrastructure;
using PklBridge.Infrastructure.Serial;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace PklBridge.Service;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly BridgeSettings _bridgeSettings;
    private readonly VidaApiSettings _vidaSettings;
    private readonly SerialBridge _serialBridge;
    private readonly IVidaApiClient _vidaClient;
    private readonly TcpServer _tcpServer;
    private readonly AstmSessionManager _astmSessionManager;
    private readonly ResultProcessor _resultProcessor;
    private readonly IAstmParser _astmParser;
    
    // Armazenar mensagens pendentes para envio quando HLAB reconectar
    private readonly ConcurrentDictionary<string, string> _pendingMessages = new();

    public Worker(
        ILogger<Worker> logger,
        IOptions<BridgeSettings> bridgeSettings,
        IOptions<VidaApiSettings> vidaSettings,
        SerialBridge serialBridge,
        IVidaApiClient vidaClient,
        TcpServer tcpServer,
        AstmSessionManager astmSessionManager,
        ResultProcessor resultProcessor,
        IAstmParser astmParser)
    {
        _logger = logger;
        _bridgeSettings = bridgeSettings.Value;
        _vidaSettings = vidaSettings.Value;
        _serialBridge = serialBridge;
        _vidaClient = vidaClient;
        _tcpServer = tcpServer;
        _astmSessionManager = astmSessionManager;
        _resultProcessor = resultProcessor;
        _astmParser = astmParser;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("PKL Bridge Service iniciando...");

        // Log configurações
        LogConfigurationSummary();

        // Teste da API
        await TestVidaApiConnection(cancellationToken);

        // Iniciar TCP Server se habilitado
        if (_bridgeSettings.EnableTcpServer)
        {
            await StartTcpServer(cancellationToken);
        }

        await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("PKL Bridge Service parando...");
        
        try
        {
            if (_serialBridge.IsRunning)
            {
                await _serialBridge.StopAsync(cancellationToken);
            }
            
            if (_tcpServer.IsRunning)
            {
                await _tcpServer.StopAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao parar serviços");
        }

        await base.StopAsync(cancellationToken);
        
        _logger.LogInformation("PKL Bridge Service parou");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PKL Bridge Service executando...");

        try
        {
            // Iniciar o serial bridge
            await _serialBridge.StartAsync(stoppingToken);

            // Subscrever aos eventos para monitoramento
            _serialBridge.MessageReceived += OnMessageReceived;
            _serialBridge.MessageSent += OnMessageSent;
            _serialBridge.ErrorOccurred += OnErrorOccurred;

            // Manter o serviço vivo
            while (!stoppingToken.IsCancellationRequested)
            {
                // Health check periódico (a cada 5 minutos)
                await PerformHealthCheck(stoppingToken);

                // Aguardar antes do próximo health check
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("PKL Bridge Service cancelado");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado no PKL Bridge Service");
            throw;
        }
        finally
        {
            // Desinscrever dos eventos
            _serialBridge.MessageReceived -= OnMessageReceived;
            _serialBridge.MessageSent -= OnMessageSent;
            _serialBridge.ErrorOccurred -= OnErrorOccurred;
        }
    }

    private void LogConfigurationSummary()
    {
        _logger.LogInformation("=== Configuração PKL Bridge Service ===");
        _logger.LogInformation("Named Pipe: {PipeName}", _bridgeSettings.PipeName);
        _logger.LogInformation("COM Port Real: {ComPort}", _bridgeSettings.RealComPort ?? "Não configurado");
        _logger.LogInformation("TCP Server: {Enabled} | Porta: {Port}", 
            _bridgeSettings.EnableTcpServer ? "Habilitado" : "Desabilitado",
            _bridgeSettings.TcpPort);
        _logger.LogInformation("VIDA API: {BaseUrl}", _vidaSettings.BaseUrl);
        _logger.LogInformation("Franchise ID: {FranchiseId}", _vidaSettings.FranchiseCredentialId);
        _logger.LogInformation("=======================================");
    }

    private async Task TestVidaApiConnection(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Testando conexão VIDA API...");
            
            var result = await _vidaClient.TestConnectionAsync(cancellationToken);
            
            if (result.Success)
            {
                _logger.LogInformation("✅ Teste de conexão VIDA API bem-sucedido");
            }
            else
            {
                _logger.LogWarning("⚠️ Teste de conexão VIDA API falhou: {Error}", result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("⚠️ Teste de conexão VIDA API falhou: {Error}", ex.Message);
        }
    }

    private async Task PerformHealthCheck(CancellationToken cancellationToken)
    {
        try
        {
            var healthData = new
            {
                Timestamp = DateTime.Now,
                SerialBridgeRunning = _serialBridge.IsRunning,
                TcpServerRunning = _tcpServer.IsRunning,
                MemoryUsage = GC.GetTotalMemory(false) / (1024 * 1024), // MB
                UptimeMinutes = (DateTime.Now - Process.GetCurrentProcess().StartTime).TotalMinutes
            };

            _logger.LogInformation("💚 Health Check - Bridge: {BridgeStatus} | TCP: {TcpStatus} | Memória: {MemoryMB}MB | Uptime: {UptimeMinutes:F1}min",
                healthData.SerialBridgeRunning ? "Rodando" : "Parado",
                healthData.TcpServerRunning ? "Rodando" : "Parado",
                healthData.MemoryUsage,
                healthData.UptimeMinutes);

            // Reiniciar bridge se não estiver rodando
            if (!_serialBridge.IsRunning)
            {
                _logger.LogWarning("⚠️ Serial bridge não está rodando, tentando reiniciar...");
                
                try
                {
                    await _serialBridge.StartAsync(cancellationToken);
                    _logger.LogInformation("✅ Serial bridge reiniciado com sucesso");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Falha ao reiniciar serial bridge");
                }
            }
            
            // Reiniciar TCP server se não estiver rodando
            if (_bridgeSettings.EnableTcpServer && !_tcpServer.IsRunning)
            {
                _logger.LogWarning("⚠️ TCP Server não está rodando, tentando reiniciar...");
                
                try
                {
                    await _tcpServer.StartAsync(cancellationToken);
                    _logger.LogInformation("✅ TCP Server reiniciado com sucesso");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "❌ Falha ao reiniciar TCP Server");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro durante health check");
        }
    }

    private void OnMessageReceived(object? sender, MessageReceivedEventArgs e)
    {
        _logger.LogDebug("📨 Mensagem recebida de {Source}: {BytesCount} bytes", 
            e.Source, e.Data.Length);
    }

    private void OnMessageSent(object? sender, MessageSentEventArgs e)
    {
        _logger.LogDebug("📤 Mensagem enviada para {Destination}: {BytesCount} bytes", 
            e.Destination, e.Data.Length);
    }

    private void OnErrorOccurred(object? sender, BridgeErrorEventArgs e)
    {
        _logger.LogError(e.Exception, "❌ Erro no bridge de {Source}: {Message}", 
            e.Source, e.Message);
    }

    private async Task StartTcpServer(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("🌐 Iniciando TCP Server na porta {Port} para HLAB...", _bridgeSettings.TcpPort);
            
            // Subscrever ao evento de dados recebidos via TCP
            _tcpServer.DataReceived += OnTcpDataReceived;
            
            await _tcpServer.StartAsync(cancellationToken);
            
            _logger.LogInformation("✅ TCP Server iniciado com sucesso na porta {Port}", _bridgeSettings.TcpPort);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao iniciar TCP Server");
        }
    }

    private async void OnTcpDataReceived(object? sender, TcpDataReceivedEventArgs e)
    {
        try
        {
            // Verificar caracteres de controle
            if (e.Data.Length == 1)
            {
                var controlChar = e.Data[0];
                
                // ACK, NAK: Retornar imediatamente
                if (controlChar == 0x06 || controlChar == 0x15)
                {
                    return;
                }
                
                // EOT: HLAB finalizou transmissão - ENVIAR RESULTADOS PARA API VIDA
                if (controlChar == 0x04)
                {
                    _logger.LogInformation("[HLAB→Bridge] EOT recebido | Finalizando sessão e enviando resultados para API VIDA");
                    
                    // Finalizar sessão e enviar resultados acumulados
                    await _resultProcessor.FinalizeSessionAsync(e.ClientEndpoint, CancellationToken.None);
                    
                    _logger.LogInformation("[HLAB→Bridge] Sessão finalizada | Pendentes: {Count}", _pendingMessages.Count);
                    return;
                }
                
                // ENQ: HLAB quer iniciar comunicação
                if (controlChar == 0x05)
                {
                    _logger.LogInformation("[HLAB→Bridge] ENQ recebido | Pendentes: {Count}", _pendingMessages.Count);
                    
                    if (_pendingMessages.Count > 0)
                    {
                        var firstPending = _pendingMessages.First();
                        var patientId = firstPending.Key;
                        var astmMessage = firstPending.Value;
                        
                        await _tcpServer.SendToClientAsync(e.ClientEndpoint, new byte[] { 0x06 }, CancellationToken.None);
                        await Task.Delay(500);
                        
                        var success = await _astmSessionManager.SendAstmMessageAsync(
                            e.ClientEndpoint,
                            astmMessage,
                            isResponseToQuery: true,
                            CancellationToken.None);
                        
                        if (success)
                        {
                            _logger.LogInformation("[Bridge→HLAB] Mensagem enviada | Paciente: {PatientId}", patientId);
                            _pendingMessages.TryRemove(patientId, out _);
                        }
                        else
                        {
                            _logger.LogError("[Bridge→HLAB] Falha ao enviar | Paciente: {PatientId}", patientId);
                        }
                    }
                    else
                    {
                        await _tcpServer.SendToClientAsync(e.ClientEndpoint, new byte[] { 0x06 }, CancellationToken.None);
                    }
                    
                    return;
                }
            }

            // Frame ASTM: Enviar ACK e processar
            if (e.Data.Length > 2 && e.Data[0] == 0x02)
            {
                // Enviar ACK
                for (int i = 1; i < e.Data.Length; i++)
                {
                    if (e.Data[i] == 0x03)
                    {
                        await _tcpServer.SendToClientAsync(e.ClientEndpoint, new byte[] { 0x06 }, CancellationToken.None);
                        break;
                    }
                }
                
                // Parsear mensagem ASTM
                var messages = _astmParser.Parse(e.Data);
                
                // Processar cada mensagem (incluindo resultados)
                foreach (var message in messages)
                {
                    await _resultProcessor.ProcessMessageAsync(message, e.ClientEndpoint, CancellationToken.None);
                }
                
                // Processar Query se houver
                var asciiData = System.Text.Encoding.ASCII.GetString(e.Data);
                if (asciiData.Contains("Q|"))
                {
                    var patientId = ExtractPatientIdFromQuery(asciiData);
                    if (!string.IsNullOrEmpty(patientId))
                    {
                        _logger.LogInformation("[HLAB→Bridge] Query recebida | Paciente: {PatientId}", patientId);
                        await SendExamsForPatient(patientId, e.ClientEndpoint, CancellationToken.None);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ERRO] Processamento TCP | Cliente: {ClientEndpoint}", e.ClientEndpoint);
        }
    }

    private string ExtractPatientIdFromQuery(string asciiData)
    {
        try
        {
            var lines = asciiData.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.Contains("Q|"))
                {
                    var fields = line.Split('|');
                    if (fields.Length >= 3)
                    {
                        var specimenField = fields[2];
                        var parts = specimenField.Split('^');
                        
                        if (specimenField.StartsWith("^") && parts.Length > 1)
                        {
                            return parts[1];
                        }
                        else if (!string.IsNullOrEmpty(parts[0]))
                        {
                            return parts[0];
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao extrair ID do paciente da Query");
        }
        
        return string.Empty;
    }

    private async Task SendExamsForPatient(string patientId, string clientEndpoint, CancellationToken cancellationToken)
    {
        try
        {
            var examResponse = await _vidaClient.GetExamsByTagAsync(
                _vidaSettings.FranchiseCredentialId,
                patientId,
                cancellationToken);

            if (!examResponse.Success || examResponse.Data == null || examResponse.Data.Data == null || !examResponse.Data.Data.Any())
            {
                _logger.LogWarning("[API VIDA] Sem exames | Tag: {TagId}", patientId);
                await _tcpServer.SendToClientAsync(clientEndpoint, new byte[] { 0x04 }, cancellationToken);
                return;
            }

            var exams = examResponse.Data.Data;
            var firstExam = exams.First();
            
            _logger.LogInformation("[API VIDA] {ExamCount} exames | Tag: {TagId} | Paciente: {PatientName}", 
                exams.Count, patientId, firstExam.PatientName);

            // Construir mensagem ASTM e enviar
            var patient = new Core.Models.PatientData
            {
                Id = patientId,
                FirstName = firstExam.PatientName ?? $"Paciente {patientId}",
                LastName = "",
                Gender = firstExam.Gender ?? "",
                BirthDate = !string.IsNullOrEmpty(firstExam.BirthDate) && DateTime.TryParse(firstExam.BirthDate, out var parsedDate) 
                    ? parsedDate 
                    : (DateTime?)null
            };

            var testCodes = exams
                .Where(e => e.Test != "OBS")
                .Select(e => e.Test)
                .Distinct()
                .ToList();
            
            var sampleType = firstExam.SampleType?.FirstOrDefault() ?? "SORO";
            
            var astmMessage = BuildAstmOrderMessage(patientId, patient, testCodes, sampleType, firstExam.Age);
            
            var success = await _astmSessionManager.SendAstmMessageAsync(
                clientEndpoint,
                astmMessage,
                isResponseToQuery: false,
                cancellationToken);
            
            if (success)
            {
                _logger.LogInformation("[Bridge→HLAB] Exames enviados | Paciente: {PatientId} | Testes: {TestCount}", 
                    patientId, testCodes.Count);
            }
            else
            {
                _logger.LogError("[Bridge→HLAB] Falha ao enviar | Paciente: {PatientId}", patientId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ERRO] Envio exames | Paciente: {PatientId}", patientId);
            try
            {
                await _tcpServer.SendToClientAsync(clientEndpoint, new byte[] { 0x04 }, cancellationToken);
            }
            catch { }
        }
    }

    private string BuildAstmOrderMessage(string patientId, Core.Models.PatientData patient, List<string> testCodes, string sampleType, int? ageFromApi = null)
    {
        var sb = new System.Text.StringBuilder();
        var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");

        // H - Header Record
        sb.Append($"H|`^&|||PKL Bridge||||||||E1394-97|{timestamp}\r");

        // P - Patient Record
        var patientName = $"{patient.FirstName} {patient.LastName}".Trim();
        var birthDate = patient.BirthDate?.ToString("yyyyMMdd") ?? "";
        sb.Append($"P|1||||{patientName}||{birthDate}|{patient.Gender}|||||\r");

        // O - Order Record
        var sampleId = $"{patientId}^^^^N";
        var testCodesList = string.Join("`", testCodes.Select(t => $"^^^{t}"));
        sb.Append($"O|2|{sampleId}||{testCodesList}|R|{timestamp}|||||||||{sampleType}||||||||||O\r");

        // L - Terminator Record
        sb.Append($"L|1|N\r");

        return sb.ToString();
    }

    public override void Dispose()
    {
        try
        {
            _serialBridge?.Dispose();
            _tcpServer?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Erro ao fazer dispose");
        }

        base.Dispose();
    }
}
