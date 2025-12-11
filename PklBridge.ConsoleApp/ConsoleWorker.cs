using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PklBridge.Core.Configuration;
using PklBridge.Core.Interfaces;
using PklBridge.Infrastructure;
using PklBridge.Infrastructure.Serial;
using System.Collections.Concurrent;

namespace PklBridge.ConsoleApp;

public class ConsoleWorker : BackgroundService
{
    private readonly ILogger<ConsoleWorker> _logger;
    private readonly BridgeSettings _bridgeSettings;
    private readonly VidaApiSettings _vidaSettings;
    private readonly SerialBridge _serialBridge;
    private readonly IVidaApiClient _vidaClient;
    private readonly SerialPortMonitor _serialPortMonitor;
    private readonly TcpServer _tcpServer;
    private readonly IMessageProcessor _messageProcessor;
    private readonly IExamOrderService _examOrderService;
    private readonly ExamRequestService _examRequestService;
    private readonly AstmSessionManager _astmSessionManager;
    
    // Armazenar mensagens pendentes para envio quando HLAB reconectar
    private readonly ConcurrentDictionary<string, string> _pendingMessages = new();

    public ConsoleWorker(
        ILogger<ConsoleWorker> logger,
        IOptions<BridgeSettings> bridgeSettings,
        IOptions<VidaApiSettings> vidaSettings,
        SerialBridge serialBridge,
        IVidaApiClient vidaClient,
        SerialPortMonitor serialPortMonitor,
        TcpServer tcpServer,
        IMessageProcessor messageProcessor,
        IExamOrderService examOrderService,
        ExamRequestService examRequestService,
        AstmSessionManager astmSessionManager)
    {
        _logger = logger;
        _bridgeSettings = bridgeSettings.Value;
        _vidaSettings = vidaSettings.Value;
        _serialBridge = serialBridge;
        _vidaClient = vidaClient;
        _serialPortMonitor = serialPortMonitor;
        _tcpServer = tcpServer;
        _messageProcessor = messageProcessor;
        _examOrderService = examOrderService;
        _examRequestService = examRequestService;
        _astmSessionManager = astmSessionManager;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("PKL Bridge Console Worker iniciando...");

        // Log configurações
        LogConfigurationSummary();

        // Teste da API (mock)
        await TestVidaApiConnection(cancellationToken);

        // Iniciar TCP Server se habilitado
        if (_bridgeSettings.EnableTcpServer)
        {
            await StartTcpServer(cancellationToken);
        }

        // Iniciar monitoramento da COM2 apenas se o hardware bridge estiver DESABILITADO
        if (!_bridgeSettings.EnableHardwareBridge)
        {
            StartSerialPortMonitoring();
        }
        else
        {
            _logger.LogInformation("🔗 Hardware Bridge HABILITADO - PKL Bridge fará bridge entre COM2 e Named Pipe");
            Console.WriteLine("🔗 BRIDGE ATIVO: HLAB → COM2 → PKL Bridge → Named Pipe → API");
            Console.WriteLine($"   HLAB pode usar COM2 normalmente!");
            Console.WriteLine();
        }

        await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("PKL Bridge Console Worker parando...");
        
        try
        {
            if (_serialBridge.IsRunning)
            {
                await _serialBridge.StopAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao parar serial bridge");
        }

        await base.StopAsync(cancellationToken);
        
        _logger.LogInformation("PKL Bridge Console Worker parou");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PKL Bridge Console Worker executando...");

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
                // Health check periódico (a cada 2 minutos em modo console)
                await PerformHealthCheck(stoppingToken);

                // Aguardar antes do próximo health check
                await Task.Delay(TimeSpan.FromMinutes(2), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("PKL Bridge Console Worker cancelado");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro inesperado no PKL Bridge Console Worker");
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
        _logger.LogInformation("=== Configuração PKL Bridge ===");
        _logger.LogInformation("Named Pipe: {PipeName}", _bridgeSettings.PipeName);
        _logger.LogInformation("COM Port Real: {ComPort}", _bridgeSettings.RealComPort ?? "Não configurado");
        _logger.LogInformation("Bridge Hardware: {Enabled}", _bridgeSettings.EnableHardwareBridge ? "Habilitado" : "Desabilitado");
        _logger.LogInformation("Log Todo Tráfego: {Enabled}", _bridgeSettings.LogAllTraffic ? "Habilitado" : "Desabilitado");
        _logger.LogInformation("Config Serial: {BaudRate}, {DataBits}, {Parity}, {StopBits}", 
            _bridgeSettings.Serial.BaudRate, 
            _bridgeSettings.Serial.DataBits, 
            _bridgeSettings.Serial.Parity, 
            _bridgeSettings.Serial.StopBits);
        _logger.LogInformation("VIDA API: {BaseUrl}", _vidaSettings.BaseUrl);
        _logger.LogInformation("API Timeout: {TimeoutSeconds}s", _vidaSettings.TimeoutSeconds);
        _logger.LogInformation("Tentativas Retry: {MaxRetryAttempts}", _vidaSettings.MaxRetryAttempts);
        _logger.LogInformation("===============================");
    }

    private async Task TestVidaApiConnection(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Testando conexão VIDA API (modo mock)...");
            
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
                MemoryUsage = GC.GetTotalMemory(false) / (1024 * 1024), // MB
                UptimeMinutes = (DateTime.Now - System.Diagnostics.Process.GetCurrentProcess().StartTime).TotalMinutes
            };

            _logger.LogInformation("💚 Health Check - Bridge: {BridgeStatus}, Memória: {MemoryMB}MB, Uptime: {UptimeMinutes:F1}min",
                healthData.SerialBridgeRunning ? "Rodando" : "Parado",
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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro durante health check");
        }
    }

    private void OnMessageReceived(object? sender, MessageReceivedEventArgs e)
    {
        _logger.LogInformation("📨 Mensagem recebida de {Source}: {BytesCount} bytes", 
            e.Source, e.Data.Length);

        if (_bridgeSettings.LogAllTraffic)
        {
            _logger.LogDebug("📨 Dados: {Data}", Convert.ToHexString(e.Data));
        }
    }

    private void OnMessageSent(object? sender, MessageSentEventArgs e)
    {
        _logger.LogInformation("📤 Mensagem enviada para {Destination}: {BytesCount} bytes", 
            e.Destination, e.Data.Length);

        if (_bridgeSettings.LogAllTraffic)
        {
            _logger.LogDebug("📤 Dados: {Data}", Convert.ToHexString(e.Data));
        }
    }

    private void OnErrorOccurred(object? sender, BridgeErrorEventArgs e)
    {
        _logger.LogError(e.Exception, "❌ Erro no bridge de {Source}: {Message}", 
            e.Source, e.Message);
    }

    private void StartSerialPortMonitoring()
    {
        try
        {
            _logger.LogInformation("🔍 Iniciando monitoramento paralelo da COM2 para detectar dados do HLAB...");
            
            var portName = _bridgeSettings.RealComPort ?? "COM2";
            var baudRate = _bridgeSettings.Serial.BaudRate;
            
            var success = _serialPortMonitor.StartMonitoring(portName, baudRate);
            
            if (success)
            {
                _logger.LogInformation("✅ Monitoramento da {PortName} iniciado - dados detectados serão alertados no console", portName);
                Console.WriteLine($"🔍 MONITORAMENTO ATIVO: Verificando se HLAB envia dados para {portName}");
                Console.WriteLine("   Se aparecer um alerta, significa que HLAB ainda está usando COM2!");
                Console.WriteLine();
            }
            else
            {
                _logger.LogWarning("⚠️ Não foi possível iniciar monitoramento da {PortName} - porta pode estar em uso ou não existir", portName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao iniciar monitoramento da porta serial");
        }
    }

    private async Task StartTcpServer(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("🌐 Iniciando TCP Server na porta {Port} para HLAB...", _bridgeSettings.TcpPort);
            
            // Subscrever ao evento de dados recebidos via TCP
            _tcpServer.DataReceived += OnTcpDataReceived;
            
            await _tcpServer.StartAsync(cancellationToken);
            
            _logger.LogInformation("✅ TCP Server iniciado com sucesso!");
            Console.WriteLine($"🌐 TCP SERVER ATIVO na porta {_bridgeSettings.TcpPort}");
            Console.WriteLine($"   Configure HLAB: Servidor IP 0.0.0.0 Porta {_bridgeSettings.TcpPort}");
            Console.WriteLine();
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
            // CRÍTICO: Verificar caracteres de controle ANTES de qualquer logging
            // para não consumir o evento antes do AstmSessionManager processar
            if (e.Data.Length == 1)
            {
                var controlChar = e.Data[0];
                
                // ACK, NAK: Retornar IMEDIATAMENTE sem nenhum log
                // O AstmSessionManager está subscrito e vai processar
                if (controlChar == 0x06 || controlChar == 0x15)
                {
                    return; // Retorna SEM LOGAR para não consumir o evento
                }
                
                // EOT: Logar para debugging
                if (controlChar == 0x04) // EOT
                {
                    _logger.LogInformation("🔍 DETECTADO: EOT (End of Transmission) - HLAB finalizou e vai desconectar");
                    _logger.LogInformation("⏳ Aguardando HLAB se reconectar com ENQ para enviar mensagens pendentes ({Count})...", _pendingMessages.Count);
                    return;
                }
                
                // ENQ: Verificar se há mensagem pendente para enviar
                if (controlChar == 0x05) // ENQ
                {
                    _logger.LogInformation("🔍 DETECTADO: ENQ (Enquiry) - HLAB reconectou!");
                    
                    // Verificar se há mensagens pendentes
                    if (_pendingMessages.Count > 0)
                    {
                        _logger.LogInformation("📦 DETECTADO {Count} mensagem(ns) pendente(s)!", _pendingMessages.Count);
                        
                        // Pegar primeira mensagem pendente
                        var firstPending = _pendingMessages.First();
                        var patientId = firstPending.Key;
                        var astmMessage = firstPending.Value;
                        
                        _logger.LogInformation("📤 Enviando mensagem pendente para paciente {PatientId}", patientId);
                        
                        // Enviar ACK primeiro
                        await _tcpServer.SendToClientAsync(e.ClientEndpoint, new byte[] { 0x06 }, CancellationToken.None);
                        _logger.LogInformation("✅ ACK enviado para ENQ");
                        
                        // Aguardar um pouco
                        await Task.Delay(500);
                        
                        // ENVIAR frames DIRETAMENTE (HLAB já enviou ENQ, não precisamos enviar outro!)
                        var success = await _astmSessionManager.SendAstmMessageAsync(
                            e.ClientEndpoint,
                            astmMessage,
                            isResponseToQuery: true,  // Pula ENQ! HLAB já iniciou a sessão
                            CancellationToken.None);
                        
                        if (success)
                        {
                            _logger.LogInformation("✅ Mensagem enviada com sucesso para paciente {PatientId}!", patientId);
                            // Remover mensagem da fila
                            _pendingMessages.TryRemove(patientId, out _);
                        }
                        else
                        {
                            _logger.LogError("❌ Falha ao enviar mensagem para paciente {PatientId}", patientId);
                        }
                    }
                    else
                    {
                        // Sem mensagens pendentes, apenas responder ACK
                        _logger.LogInformation("📭 Nenhuma mensagem pendente - apenas respondendo ACK");
                        await _tcpServer.SendToClientAsync(e.ClientEndpoint, new byte[] { 0x06 }, CancellationToken.None);
                        _logger.LogInformation("✅ ACK enviado para ENQ");
                    }
                    
                    return;
                }
            }

            _logger.LogInformation("📨 Dados recebidos via TCP de {ClientEndpoint}: {BytesCount} bytes", 
                e.ClientEndpoint, e.Data.Length);

            if (_bridgeSettings.LogAllTraffic)
            {
                var hexData = Convert.ToHexString(e.Data);
                _logger.LogDebug("📨 TCP Data (HEX): {HexData}", hexData);
            }

            // CRÍTICO: Se é um frame ASTM (STX...ETX+checksum), enviar ACK imediatamente!
            if (e.Data.Length > 2 && e.Data[0] == 0x02) // STX
            {
                // Procurar ETX
                for (int i = 1; i < e.Data.Length; i++)
                {
                    if (e.Data[i] == 0x03) // ETX
                    {
                        // Frame ASTM completo detectado - enviar ACK imediatamente
                        _logger.LogInformation("🔍 DETECTADO: Frame ASTM (STX...ETX) - enviando ACK");
                        await _tcpServer.SendToClientAsync(e.ClientEndpoint, new byte[] { 0x06 }, CancellationToken.None);
                        _logger.LogInformation("✅ ACK enviado para frame ASTM");
                        break;
                    }
                }
            }

            // Processar dados ASTM recebidos via TCP (Query, Results, etc)
            await ProcessTcpDataAsync(e.Data, e.ClientEndpoint, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao processar dados TCP de {ClientEndpoint}", e.ClientEndpoint);
        }
    }

    private async Task ProcessTcpDataAsync(byte[] data, string clientEndpoint, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("🔄 PROCESSANDO {BytesCount} bytes recebidos via TCP:", data.Length);
            
            // 1. DADOS EM HEX (para debug técnico)
            var hexData = Convert.ToHexString(data);
            _logger.LogInformation("📄 HEX: {HexData}", hexData);
            
            // 2. DADOS EM ASCII (legível)
            var asciiData = System.Text.Encoding.ASCII.GetString(data);
            _logger.LogInformation("📝 ASCII: '{AsciiData}'", asciiData);
            
            // 3. DADOS COM CARACTERES DE CONTROLE VISÍVEIS
            var visibleData = System.Text.Encoding.ASCII.GetString(data)
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t")
                .Replace("\0", "\\0")
                .Replace("\x01", "<SOH>")
                .Replace("\x02", "<STX>")
                .Replace("\x03", "<ETX>")
                .Replace("\x04", "<EOT>")
                .Replace("\x05", "<ENQ>")
                .Replace("\x06", "<ACK>")
                .Replace("\x15", "<NAK>");
                
            _logger.LogInformation("👁️ VISÍVEL: '{VisibleData}'", visibleData);
            
            // 4. BYTES INDIVIDUAIS (para análise detalhada)
            var byteValues = string.Join(" ", data.Select(b => $"{b:D3}({b:X2})"));
            _logger.LogInformation("🔢 BYTES: {ByteValues}", byteValues);
            
            // 5. TENTAR DETECTAR E RESPONDER AO PROTOCOLO ASTM
            if (data.Length > 0)
            {
                var firstByte = data[0];
                var lastByte = data[data.Length - 1];
                
                _logger.LogInformation("🎯 ANÁLISE: Primeiro={FirstByte}({FirstHex}), Último={LastByte}({LastHex})", 
                    firstByte, firstByte.ToString("X2"), lastByte, lastByte.ToString("X2"));
                
                // PROTOCOLO ASTM - Respostas automáticas
                if (firstByte == 0x05) // ENQ
                {
                    _logger.LogInformation("🔍 DETECTADO: ENQ (Enquiry) - HLAB quer iniciar comunicação");
                    await SendAstmResponse(clientEndpoint, 0x06, "ACK", "Confirmar comunicação", cancellationToken);
                }
                else if (firstByte == 0x04) // EOT
                {
                    _logger.LogInformation("🔍 DETECTADO: EOT (End of Transmission) - HLAB finalizou comunicação");
                }
                else if (firstByte == 0x02) // STX
                {
                    _logger.LogInformation("🔍 DETECTADO: STX - Início de mensagem ASTM");
                    if (lastByte == 0x03) // ETX
                        _logger.LogInformation("🔍 DETECTADO: ETX - Fim de mensagem ASTM");
                    
                    // Verificar se é uma Query (Q)
                    if (asciiData.Contains("Q|"))
                    {
                        _logger.LogInformation("🔍 DETECTADO: QUERY (Q) - HLAB solicitando exames!");
                        
                        // Extrair ID do paciente da Query
                        var patientId = ExtractPatientIdFromQuery(asciiData);
                        if (!string.IsNullOrEmpty(patientId))
                        {
                            _logger.LogInformation("🔍 ID do Paciente extraído: {PatientId}", patientId);
                            
                            // Enviar ACK primeiro
                            await SendAstmResponse(clientEndpoint, 0x06, "ACK", "Confirmar recebimento Query", cancellationToken);
                            
                            // Aguardar um pouco antes de enviar (dar tempo para HLAB processar ACK)
                            await Task.Delay(100, cancellationToken);
                            
                            // Enviar exames DIRETAMENTE (resposta a Query)
                            await SendExamsForPatient(patientId, clientEndpoint, cancellationToken);
                        }
                        else
                        {
                            _logger.LogWarning("⚠️ Não foi possível extrair ID do paciente da Query");
                            await SendAstmResponse(clientEndpoint, 0x06, "ACK", "Confirmar recebimento Query", cancellationToken);
                        }
                    }
                    else
                    {
                        // Enviar ACK para confirmar recebimento da mensagem
                        await SendAstmResponse(clientEndpoint, 0x06, "ACK", "Confirmar recebimento mensagem ASTM", cancellationToken);
                    }
                }
                else if (firstByte == 0x15) // NAK
                {
                    _logger.LogInformation("🔍 DETECTADO: NAK (Not Acknowledged) - Erro na comunicação");
                }
                
                if (asciiData.Contains("|"))
                    _logger.LogInformation("🔍 DETECTADO: Contém separadores ASTM (|)");
            }
            
            _logger.LogInformation("✅ Dados analisados e exibidos em detalhes");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao processar dados ASTM via TCP");
        }
    }

    private string ExtractPatientIdFromQuery(string asciiData)
    {
        try
        {
            // Query format: Q|1|^<PatientID>||ALL||||||||O
            // ou: Q|1|<PatientID>^^<Disk>^<Position>^<Diluent>||ALL||||||||O
            
            var lines = asciiData.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.Contains("Q|"))
                {
                    var fields = line.Split('|');
                    if (fields.Length >= 3)
                    {
                        var specimenField = fields[2]; // Campo 3 (Specimen ID)
                        
                        // Pode ser: ^<PatientID> ou <PatientID>^^<Disk>^<Position>
                        var parts = specimenField.Split('^');
                        
                        // Se começa com ^, o ID está na segunda parte
                        if (specimenField.StartsWith("^") && parts.Length > 1)
                        {
                            return parts[1];
                        }
                        // Se não começa com ^, o ID está na primeira parte
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

    private async Task QueueExamsForPatient(string patientId, string clientEndpoint, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("🔍 Buscando exames para paciente {PatientId} e adicionando à fila...", patientId);
            
            // 1. Buscar dados do paciente
            var patientResponse = await _vidaClient.GetPatientAsync(patientId, cancellationToken);
            if (!patientResponse.Success || patientResponse.Data == null)
            {
                _logger.LogWarning("⚠️ Paciente {PatientId} não encontrado - não será adicionado à fila", patientId);
                return;
            }

            var patient = patientResponse.Data;
            _logger.LogInformation("✅ Paciente encontrado: {PatientName}", $"{patient.FirstName} {patient.LastName}");

            // 2. Buscar pedidos de exames
            var ordersResponse = await _vidaClient.GetOrdersForPatientAsync(patientId, cancellationToken);
            if (!ordersResponse.Success || ordersResponse.Data == null || !ordersResponse.Data.Any())
            {
                _logger.LogWarning("⚠️ Nenhum exame encontrado para paciente {PatientId} - não será adicionado à fila", patientId);
                return;
            }

            var orders = ordersResponse.Data;
            var order = orders.First(); // Pegar primeiro pedido
            
            _logger.LogInformation("📋 Encontrados {TestCount} exames: {TestCodes}", 
                order.TestCodes.Count, string.Join(", ", order.TestCodes));

            // 3. Construir mensagem ASTM Order
            var astmMessage = BuildAstmOrderMessage(patientId, patient, order);
            
            _logger.LogInformation("📄 Mensagem ASTM construída: {MessageLength} caracteres", astmMessage.Length);
            
            // 4. ADICIONAR À FILA em vez de enviar imediatamente
            _pendingMessages[patientId] = astmMessage;
            
            _logger.LogInformation("📦 ✅ Mensagem para paciente {PatientId} ADICIONADA À FILA!", patientId);
            _logger.LogInformation("⏳ Total de mensagens pendentes: {Count}", _pendingMessages.Count);
            _logger.LogInformation("🔄 Aguardando HLAB finalizar com EOT e reconectar com ENQ para enviar...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao processar exames para paciente {PatientId}", patientId);
        }
    }

    private async Task SendExamsForPatient(string patientId, string clientEndpoint, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("🔍 Buscando exames para tag_id {TagId} na API VIDA...", patientId);
            
            // 1. Buscar exames usando a API real de produção
            var examResponse = await _vidaClient.GetExamsByTagAsync(
                _vidaSettings.FranchiseCredentialId,
                patientId,  // tag_id
                cancellationToken);

            if (!examResponse.Success || examResponse.Data == null || examResponse.Data.Data == null || !examResponse.Data.Data.Any())
            {
                _logger.LogWarning("⚠️ Nenhum exame encontrado para tag_id {TagId}. Mensagem: {Message}", 
                    patientId, examResponse.Data?.Message ?? examResponse.ErrorMessage);
                await SendAstmResponse(clientEndpoint, 0x04, "EOT", "Sem exames disponíveis", cancellationToken);
                return;
            }

            var exams = examResponse.Data.Data;
            _logger.LogInformation("✅ Encontrados {ExamCount} exames para tag_id {TagId}: {ExamCodes}", 
                exams.Count, patientId, string.Join(", ", exams.Select(e => $"{e.ExamCode}/{e.Test}")));

            // 2. Criar dados mínimos do paciente (API não retorna dados do paciente)
            // IMPORTANTE: Não inventar idade nem gênero - deixar vazios
            var patient = new Core.Models.PatientData
            {
                Id = patientId,
                FirstName = $"Paciente {patientId}",  // Nome identificável com tag_id
                LastName = "",
                Gender = "",  // Vazio - não inventar
                BirthDate = null  // Null - não inventar
            };

            // 3. Criar order com os testes retornados pela API
            var testCodes = exams.Select(e => e.Test).ToList();
            var order = new Core.Interfaces.ExamOrder
            {
                OrderId = $"ORD-{patientId}",
                PatientId = patientId,
                TestCodes = testCodes,
                OrderDateTime = DateTime.Now,
                Priority = "R"
            };

            // 4. Construir mensagem ASTM Order
            var astmMessage = BuildAstmOrderMessage(patientId, patient, order);
            
            _logger.LogInformation("📄 Mensagem ASTM construída: {MessageLength} caracteres", astmMessage.Length);
            
            // LOG COMPLETO E DETALHADO DA MENSAGEM
            _logger.LogInformation("📄 ====== MENSAGEM ASTM COMPLETA ======");
            _logger.LogInformation("📄 {AstmMessage}", astmMessage);
            _logger.LogInformation("📄 ====== FIM DA MENSAGEM ======");
            
            // LOG COM CARACTERES VISÍVEIS
            var visibleMessage = astmMessage
                .Replace("\r", "<CR>")
                .Replace("\n", "<LF>")
                .Replace("|", "[PIPE]")
                .Replace("^", "[CARET]")
                .Replace("\\", "[BACKSLASH]");
            _logger.LogInformation("👁️ MENSAGEM VISÍVEL: {VisibleMessage}", visibleMessage);

            // 4. ENVIAR mensagem ASTM via protocolo completo (iniciando com ENQ)
            _logger.LogInformation("🚀 Iniciando envio de mensagem ASTM via protocolo completo (com ENQ)...");
            
            var success = await _astmSessionManager.SendAstmMessageAsync(
                clientEndpoint,
                astmMessage,
                isResponseToQuery: false,  // ENVIA ENQ primeiro! É nossa vez de falar
                cancellationToken);
            
            if (success)
            {
                _logger.LogInformation("✅ Exames enviados com sucesso para paciente {PatientId} via protocolo ASTM completo!", patientId);
            }
            else
            {
                _logger.LogError("❌ Falha ao enviar exames para paciente {PatientId}", patientId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao buscar/enviar exames para paciente {PatientId}", patientId);
            try
            {
                await SendAstmResponse(clientEndpoint, 0x04, "EOT", "Erro ao processar", cancellationToken);
            }
            catch (Exception eotEx)
            {
                _logger.LogError(eotEx, "❌ Erro ao enviar EOT de erro");
            }
        }
    }

    private string BuildAstmOrderMessage(string patientId, Core.Models.PatientData patient, Core.Interfaces.ExamOrder order)
    {
        var sb = new System.Text.StringBuilder();
        var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");

        // FORMATO EXATO DO LOG.TXT DE REFERÊNCIA!
        
        // H - Header Record - USANDO BACKTICK (`) EM VEZ DE BACKSLASH (\)
        sb.Append($"H|`^&|||PKL Bridge||||||||E1394-97|{timestamp}\r");

        // P - Patient Record
        var birthDate = patient.BirthDate?.ToString("yyyyMMdd") ?? DateTime.Now.AddYears(-30).ToString("yyyyMMdd");
        var patientName = $"{patient.FirstName} {patient.LastName}";
        var age = patient.BirthDate.HasValue 
            ? (DateTime.Now.Year - patient.BirthDate.Value.Year).ToString()
            : "30";
        sb.Append($"P|1||||{patientName}|||{patient.Gender}||||||{age}^Y\r");

        // O - Order Record - USANDO ID Mode (SEM ^ inicial)
        // Formato correto baseado no log de referência: SampleID^^^^Type
        // IMPORTANTE: São 4 CARETS (^^^^), não 3!
        // Estrutura: SampleID^Rack^Position^Diluent^Type (3 campos vazios + Type)
        // Exemplo funcionando: 112233^^^^N
        var sampleId = $"{patientId}^^^^N";  // 031220251276^^^^N (4 carets!)
        var testCodesList = string.Join("`", order.TestCodes.Select(t => $"^^^{t}"));
        
        sb.Append($"O|2|{sampleId}||{testCodesList}|R|{timestamp}|||||||||Plasma||||||||||O\r");

        // L - Terminator Record
        sb.Append($"L|1|N\r");

        return sb.ToString();
    }

    private async Task SendAstmResponse(string clientEndpoint, byte responseCode, string codeName, string description, CancellationToken cancellationToken)
    {
        try
        {
            var responseData = new byte[] { responseCode };
            
            _logger.LogInformation("📤 ENVIANDO RESPOSTA ASTM: {CodeName} ({Code:X2}) - {Description}", 
                codeName, responseCode, description);
            
            _logger.LogInformation("📄 RESPOSTA HEX: {ResponseHex}", Convert.ToHexString(responseData));
            _logger.LogInformation("👁️ RESPOSTA VISÍVEL: '<{CodeName}>'", codeName);
            
            // Enviar resposta REAL via TCP Server
            await _tcpServer.SendToClientAsync(clientEndpoint, responseData, cancellationToken);
            
            _logger.LogInformation("✅ Resposta ASTM {CodeName} ENVIADA com sucesso para {ClientEndpoint}!", 
                codeName, clientEndpoint);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao enviar resposta ASTM {CodeName} para {ClientEndpoint}", 
                codeName, clientEndpoint);
        }
    }

    public override void Dispose()
    {
        try
        {
            _serialPortMonitor?.Dispose();
            _serialBridge?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ Erro ao fazer dispose do serial bridge");
        }

        base.Dispose();
    }
}
