using Microsoft.Extensions.Logging;
using PklBridge.Core.Interfaces;

namespace PklBridge.ConsoleApp;

public class InteractiveMenu
{
    private readonly ILogger<InteractiveMenu> _logger;
    private readonly IExamOrderService _examOrderService;
    private readonly IVidaApiClient _vidaApiClient;
    private bool _isRunning = true;

    public InteractiveMenu(
        ILogger<InteractiveMenu> logger,
        IExamOrderService examOrderService,
        IVidaApiClient vidaApiClient)
    {
        _logger = logger;
        _examOrderService = examOrderService;
        _vidaApiClient = vidaApiClient;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        Console.Clear();
        ShowWelcomeMessage();

        while (_isRunning && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                ShowMainMenu();
                var choice = Console.ReadLine()?.Trim();

                switch (choice)
                {
                    case "1":
                        await SimulateUrineExamAsync(cancellationToken);
                        break;
                    case "2":
                        await RequestExamsForPatientAsync(cancellationToken);
                        break;
                    case "3":
                        await ShowPendingOrdersAsync(cancellationToken);
                        break;
                    case "4":
                        await ShowPatientInfoAsync(cancellationToken);
                        break;
                    case "5":
                        await TestVidaConnectionAsync(cancellationToken);
                        break;
                    case "6":
                        ShowSystemStatus();
                        break;
                    case "0":
                        _isRunning = false;
                        Console.WriteLine("👋 Saindo do menu interativo...");
                        break;
                    default:
                        Console.WriteLine("❌ Opção inválida! Tente novamente.");
                        break;
                }

                if (_isRunning)
                {
                    Console.WriteLine("\nPressione qualquer tecla para continuar...");
                    Console.ReadKey();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no menu interativo");
                Console.WriteLine($"❌ Erro: {ex.Message}");
                Console.WriteLine("Pressione qualquer tecla para continuar...");
                Console.ReadKey();
            }
        }
    }

    private void ShowWelcomeMessage()
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                    PKL BRIDGE - TESTE INTERATIVO            ║");
        Console.WriteLine("║                                                              ║");
        Console.WriteLine("║  Sistema de Interface PKL 125 ↔ Sistema VIDA               ║");
        Console.WriteLine("║  Protocolo ASTM E1394-97 | TCP Server Ativo                ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
    }

    private void ShowMainMenu()
    {
        Console.Clear();
        Console.WriteLine("🧪 === MENU PRINCIPAL - PKL BRIDGE ===");
        Console.WriteLine();
        Console.WriteLine("1️⃣  Simular Exame de Urina Completo");
        Console.WriteLine("2️⃣  Solicitar Exames para Paciente");
        Console.WriteLine("3️⃣  Ver Pedidos Pendentes");
        Console.WriteLine("4️⃣  Consultar Dados do Paciente");
        Console.WriteLine("5️⃣  Testar Conexão VIDA API");
        Console.WriteLine("6️⃣  Status do Sistema");
        Console.WriteLine("0️⃣  Sair");
        Console.WriteLine();
        Console.Write("Escolha uma opção: ");
    }

    private async Task SimulateUrineExamAsync(CancellationToken cancellationToken)
    {
        Console.Clear();
        Console.WriteLine("🧪 === SIMULAÇÃO DE EXAME DE URINA ===");
        Console.WriteLine();

        // Mostrar pacientes disponíveis
        Console.WriteLine("📋 Pacientes disponíveis para teste:");
        Console.WriteLine("• URINA001 - João Silva (M, 43 anos)");
        Console.WriteLine("• URINA002 - Maria Santos (F, 48 anos)");
        Console.WriteLine("• URINA003 - Pedro Costa (M, 33 anos)");
        Console.WriteLine();

        Console.Write("Digite o ID do paciente (ou Enter para URINA001): ");
        var patientId = Console.ReadLine()?.Trim();
        
        if (string.IsNullOrEmpty(patientId))
        {
            patientId = "URINA001";
        }

        Console.WriteLine();
        Console.WriteLine($"🔄 Iniciando simulação de exame de urina para paciente {patientId}...");
        Console.WriteLine();

        var startTime = DateTime.Now;
        var result = await _examOrderService.SimulateUrineExamAsync(patientId, cancellationToken);
        var duration = DateTime.Now - startTime;

        if (result.Success)
        {
            Console.WriteLine("✅ Simulação concluída com sucesso!");
            Console.WriteLine($"⏱️  Tempo total: {duration.TotalMilliseconds:F0}ms");
            
            if (result.Metadata.ContainsKey("SimulatedResults"))
            {
                Console.WriteLine($"🔬 Resultados simulados: {result.Metadata["SimulatedResults"]}");
            }
        }
        else
        {
            Console.WriteLine("❌ Falha na simulação:");
            Console.WriteLine($"   {result.ErrorMessage}");
        }
    }

    private async Task RequestExamsForPatientAsync(CancellationToken cancellationToken)
    {
        Console.Clear();
        Console.WriteLine("📋 === SOLICITAR EXAMES PARA PACIENTE ===");
        Console.WriteLine();

        Console.Write("Digite o ID do paciente: ");
        var patientId = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(patientId))
        {
            Console.WriteLine("❌ ID do paciente é obrigatório!");
            return;
        }

        Console.WriteLine();
        Console.WriteLine($"🔄 Solicitando exames para paciente {patientId}...");

        var result = await _examOrderService.RequestExamsForPatientAsync(patientId, cancellationToken);

        if (result.Success)
        {
            Console.WriteLine("✅ Solicitação enviada com sucesso!");
            Console.WriteLine($"⏱️  Tempo de processamento: {result.ProcessingTime.TotalMilliseconds:F0}ms");
            
            if (result.Metadata.ContainsKey("TotalOrders"))
            {
                Console.WriteLine($"📊 Total de pedidos: {result.Metadata["TotalOrders"]}");
                Console.WriteLine($"✅ Pedidos enviados: {result.Metadata["SuccessfulOrders"]}");
                
                if (result.Metadata.ContainsKey("FailedOrders") && (int)result.Metadata["FailedOrders"] > 0)
                {
                    Console.WriteLine($"❌ Pedidos falharam: {result.Metadata["FailedOrders"]}");
                }
            }
        }
        else
        {
            Console.WriteLine("❌ Falha na solicitação:");
            Console.WriteLine($"   {result.ErrorMessage}");
        }
    }

    private async Task ShowPendingOrdersAsync(CancellationToken cancellationToken)
    {
        Console.Clear();
        Console.WriteLine("📋 === PEDIDOS PENDENTES ===");
        Console.WriteLine();

        Console.WriteLine("🔄 Consultando pedidos pendentes...");

        var result = await _vidaApiClient.GetPendingOrdersAsync(cancellationToken);

        if (result.Success && result.Data != null)
        {
            var orders = result.Data;
            
            if (orders.Any())
            {
                Console.WriteLine($"📊 Encontrados {orders.Count} pedidos pendentes:");
                Console.WriteLine();

                foreach (var order in orders)
                {
                    Console.WriteLine($"🆔 Pedido: {order.OrderId}");
                    Console.WriteLine($"👤 Paciente: {order.PatientId}");
                    Console.WriteLine($"🧪 Testes: {string.Join(", ", order.TestCodes)}");
                    Console.WriteLine($"⚡ Prioridade: {order.Priority}");
                    Console.WriteLine($"📅 Data: {order.OrderDateTime:dd/MM/yyyy HH:mm}");
                    Console.WriteLine($"📊 Status: {order.Status}");
                    Console.WriteLine("─────────────────────────────────────");
                }
            }
            else
            {
                Console.WriteLine("ℹ️  Nenhum pedido pendente encontrado.");
            }
        }
        else
        {
            Console.WriteLine("❌ Falha ao consultar pedidos:");
            Console.WriteLine($"   {result.ErrorMessage}");
        }
    }

    private async Task ShowPatientInfoAsync(CancellationToken cancellationToken)
    {
        Console.Clear();
        Console.WriteLine("👤 === CONSULTAR DADOS DO PACIENTE ===");
        Console.WriteLine();

        Console.Write("Digite o ID do paciente: ");
        var patientId = Console.ReadLine()?.Trim();

        if (string.IsNullOrEmpty(patientId))
        {
            Console.WriteLine("❌ ID do paciente é obrigatório!");
            return;
        }

        Console.WriteLine();
        Console.WriteLine($"🔄 Consultando dados do paciente {patientId}...");

        var result = await _vidaApiClient.GetPatientAsync(patientId, cancellationToken);

        if (result.Success && result.Data != null)
        {
            var patient = result.Data;
            
            Console.WriteLine("✅ Paciente encontrado:");
            Console.WriteLine();
            Console.WriteLine($"🆔 ID: {patient.Id}");
            Console.WriteLine($"👤 Nome: {patient.FirstName} {patient.LastName}");
            Console.WriteLine($"🎂 Data Nascimento: {patient.BirthDate?.ToString("dd/MM/yyyy") ?? "Não informado"}");
            Console.WriteLine($"⚧️  Sexo: {patient.Gender}");
            Console.WriteLine($"🏥 Localização: {patient.Location}");
            Console.WriteLine($"🔗 ID Externo: {patient.ExternalId}");

            // Buscar pedidos do paciente
            Console.WriteLine();
            Console.WriteLine("🔄 Consultando pedidos do paciente...");
            
            var ordersResult = await _vidaApiClient.GetOrdersForPatientAsync(patientId, cancellationToken);
            
            if (ordersResult.Success && ordersResult.Data != null && ordersResult.Data.Any())
            {
                Console.WriteLine($"📋 Pedidos encontrados: {ordersResult.Data.Count}");
                
                foreach (var order in ordersResult.Data)
                {
                    Console.WriteLine($"  • {order.OrderId}: {string.Join(", ", order.TestCodes)} ({order.Status})");
                }
            }
            else
            {
                Console.WriteLine("ℹ️  Nenhum pedido encontrado para este paciente.");
            }
        }
        else
        {
            Console.WriteLine("❌ Paciente não encontrado:");
            Console.WriteLine($"   {result.ErrorMessage}");
        }
    }

    private async Task TestVidaConnectionAsync(CancellationToken cancellationToken)
    {
        Console.Clear();
        Console.WriteLine("🔗 === TESTE DE CONEXÃO VIDA API ===");
        Console.WriteLine();

        Console.WriteLine("🔄 Testando conexão com VIDA API...");

        var result = await _vidaApiClient.TestConnectionAsync(cancellationToken);

        if (result.Success)
        {
            Console.WriteLine("✅ Conexão com VIDA API bem-sucedida!");
            Console.WriteLine($"⏱️  Tempo de resposta: {result.Duration.TotalMilliseconds:F0}ms");
            Console.WriteLine($"📊 Status Code: {result.StatusCode}");
        }
        else
        {
            Console.WriteLine("❌ Falha na conexão com VIDA API:");
            Console.WriteLine($"   {result.ErrorMessage}");
            Console.WriteLine($"📊 Status Code: {result.StatusCode}");
        }
    }

    private void ShowSystemStatus()
    {
        Console.Clear();
        Console.WriteLine("📊 === STATUS DO SISTEMA ===");
        Console.WriteLine();

        var process = System.Diagnostics.Process.GetCurrentProcess();
        var uptime = DateTime.Now - process.StartTime;
        var memoryMB = GC.GetTotalMemory(false) / (1024 * 1024);

        Console.WriteLine($"🚀 Sistema: PKL Bridge Console");
        Console.WriteLine($"⏱️  Uptime: {uptime.Days}d {uptime.Hours}h {uptime.Minutes}m {uptime.Seconds}s");
        Console.WriteLine($"💾 Memória: {memoryMB} MB");
        Console.WriteLine($"🔧 Versão .NET: {Environment.Version}");
        Console.WriteLine($"💻 OS: {Environment.OSVersion}");
        Console.WriteLine();
        Console.WriteLine("🌐 Serviços:");
        Console.WriteLine("  • TCP Server: ✅ Ativo (porta 8081)");
        Console.WriteLine("  • VIDA API: ✅ Mock ativo");
        Console.WriteLine("  • ASTM Parser: ✅ Disponível");
        Console.WriteLine("  • Message Processor: ✅ Disponível");
        Console.WriteLine();
        Console.WriteLine("📋 Funcionalidades:");
        Console.WriteLine("  • ✅ Recebimento de dados ASTM via TCP");
        Console.WriteLine("  • ✅ Envio de respostas ACK automáticas");
        Console.WriteLine("  • ✅ Simulação de exames de urina");
        Console.WriteLine("  • ✅ Integração com sistema VIDA (mock)");
        Console.WriteLine("  • ✅ Validação de checksum ASTM");
        Console.WriteLine("  • ✅ Logging estruturado");
    }
}
