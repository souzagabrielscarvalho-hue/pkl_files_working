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
    private readonly ResultProcessor _resultProcessor;
    private readonly IAstmParser _astmParser;
    
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
        AstmSessionManager astmSessionManager,
        ResultProcessor resultProcessor,
        IAstmParser astmParser)
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
        _resultProcessor = resultProcessor;
        _astmParser = astmParser;
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
            }

            // Processar dados ASTM recebidos via TCP (Query, Results, etc)
            await ProcessTcpDataAsync(e.Data, e.ClientEndpoint, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ERRO] Processamento TCP | Cliente: {ClientEndpoint}", e.ClientEndpoint);
        }
    }

    private async Task ProcessTcpDataAsync(byte[] data, string clientEndpoint, CancellationToken cancellationToken)
    {
        try
        {
            var asciiData = System.Text.Encoding.ASCII.GetString(data);
            
            // Log apenas se for Query do HLAB
            if (data.Length > 0 && data[0] == 0x02 && asciiData.Contains("Q|"))
            {
                var patientId = ExtractPatientIdFromQuery(asciiData);
                if (!string.IsNullOrEmpty(patientId))
                {
                    _logger.LogInformation("[HLAB→Bridge] Query recebida | Paciente: {PatientId}", patientId);
                    
                    await SendAstmResponse(clientEndpoint, 0x06, "ACK", "Query confirmada", cancellationToken);
                    await Task.Delay(100, cancellationToken);
                    await SendExamsForPatient(patientId, clientEndpoint, cancellationToken);
                }
                else
                {
                    _logger.LogWarning("[HLAB→Bridge] Query sem ID válido");
                    await SendAstmResponse(clientEndpoint, 0x06, "ACK", "Query confirmada", cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ERRO] Processamento ASTM");
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
            var examResponse = await _vidaClient.GetExamsByTagAsync(
                _vidaSettings.FranchiseCredentialId,
                patientId,
                cancellationToken);

            if (!examResponse.Success || examResponse.Data == null || examResponse.Data.Data == null || !examResponse.Data.Data.Any())
            {
                _logger.LogWarning("[API VIDA] Sem exames | Tag: {TagId}", patientId);
                await SendAstmResponse(clientEndpoint, 0x04, "EOT", "Sem exames", cancellationToken);
                return;
            }

            var exams = examResponse.Data.Data;
            var firstExam = exams.First();
            
            _logger.LogInformation("[API VIDA] {ExamCount} exames | Tag: {TagId} | Paciente: {PatientName}", 
                exams.Count, patientId, firstExam.PatientName);

            // 2. Usar dados REAIS do paciente retornados pela API VIDA
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

            // 3. Filtrar exames "OBS" e pegar códigos dos testes
            var testCodes = exams
                .Where(e => e.Test != "OBS")
                .Select(e => e.Test)
                .Distinct()
                .ToList();
            
            var sampleType = firstExam.SampleType?.FirstOrDefault() ?? "SORO";
            var ageFromApi = firstExam.Age;
            
            var order = new Core.Interfaces.ExamOrder
            {
                OrderId = $"ORD-{patientId}",
                PatientId = patientId,
                TestCodes = testCodes,
                OrderDateTime = DateTime.Now,
                Priority = "R",
                SampleType = sampleType
            };
            
            var astmMessage = BuildAstmOrderMessage(patientId, patient, order, ageFromApi);
            
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
                await SendAstmResponse(clientEndpoint, 0x04, "EOT", "Erro", cancellationToken);
            }
            catch { }
        }
    }

    private string BuildAstmOrderMessage(string patientId, Core.Models.PatientData patient, Core.Interfaces.ExamOrder order, int? ageFromApi = null)
    {
        var sb = new System.Text.StringBuilder();
        var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");

        // FORMATO EXATO DO LOG.TXT DE REFERÊNCIA!
        
        // H - Header Record - USANDO BACKTICK (`) EM VEZ DE BACKSLASH (\)
        sb.Append($"H|`^&|||PKL Bridge||||||||E1394-97|{timestamp}\r");

        // P - Patient Record
        var patientName = $"{patient.FirstName} {patient.LastName}".Trim();
        
        // Data de nascimento no formato ASTM (yyyyMMdd)
        var birthDate = patient.BirthDate?.ToString("yyyyMMdd") ?? "";
        
        // IMPORTANTE: Estrutura ASTM E1394 correta!
        // Campo 5: Patient Name
        // Campo 6: Mother's Maiden Name (vazio)
        // Campo 7: Birth Date (yyyyMMdd)
        // Campo 8: Patient Sex (M/F)
        // Campo 9-12: Vazios (Race, Address, Reserved, Attending Physician)
        // Campo 13: Patient Age - REMOVIDO! HLAB calcula automaticamente da data de nascimento
        //           e estava interpretando este campo como "Nome Médico"
        sb.Append($"P|1||||{patientName}||{birthDate}|{patient.Gender}|||||\r");

        // O - Order Record - USANDO ID Mode (SEM ^ inicial)
        // Formato correto baseado no log de referência: SampleID^^^^Type
        // IMPORTANTE: São 4 CARETS (^^^^), não 3!
        // Estrutura: SampleID^Rack^Position^Diluent^Type (3 campos vazios + Type)
        // Exemplo funcionando: 112233^^^^N
        var sampleId = $"{patientId}^^^^N";  // 031220251276^^^^N (4 carets!)
        var testCodesList = string.Join("`", order.TestCodes.Select(t => $"^^^{t}"));
        
        // IMPORTANTE: Usar SampleType da API, não hardcoded!
        var sampleType = order.SampleType ?? "SORO";  // Usar tipo real da API
        
        sb.Append($"O|2|{sampleId}||{testCodesList}|R|{timestamp}|||||||||{sampleType}||||||||||O\r");

        // L - Terminator Record
        sb.Append($"L|1|N\r");

        return sb.ToString();
    }

    // Construir mensagem ASTM com MÚLTIPLOS Order Records (um para cada exame)
    private string BuildAstmOrderMessageMultiple(string patientId, Core.Models.PatientData patient, List<string> testCodes, string sampleType, int? ageFromApi = null)
    {
        var sb = new System.Text.StringBuilder();
        var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");

        // H - Header Record
        sb.Append($"H|`^&|||PKL Bridge||||||||E1394-97|{timestamp}\r");

        // P - Patient Record
        var patientName = $"{patient.FirstName} {patient.LastName}".Trim();
        var birthDate = patient.BirthDate?.ToString("yyyyMMdd") ?? "";
        sb.Append($"P|1||||{patientName}||{birthDate}|{patient.Gender}|||||\r");

        // O - Order Records - UM PARA CADA EXAME!
        int orderSequence = 2;  // Começa em 2 (depois do Header e Patient)
        foreach (var testCode in testCodes)
        {
            var sampleId = $"{patientId}^^^^N";
            sb.Append($"O|{orderSequence}|{sampleId}||^^^{testCode}|R|{timestamp}|||||||||{sampleType}||||||||||O\r");
            orderSequence++;
        }

        // L - Terminator Record
        sb.Append($"L|1|N\r");

        return sb.ToString();
    }

    // FUNÇÃO MOCK SEPARADA - Retorna dados exatos do log de referência
    private string BuildMockAstmOrderMessage()
    {
        var sb = new System.Text.StringBuilder();
        var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");

        // DADOS EXATOS DO LOG DE REFERÊNCIA QUE FUNCIONA
        
        // H - Header Record
        sb.Append($"H|`^&|||LABPLUS||||||||E1394-97|{timestamp}\r");

        // P - Patient Record - EXATAMENTE como no log funcionando
        sb.Append("P|1|||00004568||Mr.Test1 Surname||M||||||29^Y\r");

        // O - Order Record - EXATAMENTE como no log funcionando
        sb.Append($"O|2|112233^^^^N||^^^EUM`^^^CREA`^^^GLUC|R|{timestamp}||||||Plasma||||||||||O\r");

        // L - Terminator Record
        sb.Append("L|1|N\r");

        return sb.ToString();
    }

    private async Task SendAstmResponse(string clientEndpoint, byte responseCode, string codeName, string description, CancellationToken cancellationToken)
    {
        try
        {
            await _tcpServer.SendToClientAsync(clientEndpoint, new byte[] { responseCode }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ERRO] Envio {CodeName}", codeName);
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
