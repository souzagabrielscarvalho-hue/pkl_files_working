using Microsoft.Extensions.Logging;
using PklBridge.Core.Interfaces;
using PklBridge.Core.Models;

namespace PklBridge.Infrastructure;

public class ExamOrderService : IExamOrderService
{
    private readonly ILogger<ExamOrderService> _logger;
    private readonly IVidaApiClient _vidaApiClient;
    private readonly IAstmMessageBuilder _astmMessageBuilder;
    private readonly IMessageProcessor _messageProcessor;

    public ExamOrderService(
        ILogger<ExamOrderService> logger,
        IVidaApiClient vidaApiClient,
        IAstmMessageBuilder astmMessageBuilder,
        IMessageProcessor messageProcessor)
    {
        _logger = logger;
        _vidaApiClient = vidaApiClient;
        _astmMessageBuilder = astmMessageBuilder;
        _messageProcessor = messageProcessor;
    }

    public async Task<ProcessingResult> RequestExamsForPatientAsync(string patientId, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.Now;
        
        try
        {
            _logger.LogInformation("🔍 Solicitando exames para paciente {PatientId}", patientId);

            // 1. Buscar dados do paciente
            var patientResponse = await _vidaApiClient.GetPatientAsync(patientId, cancellationToken);
            if (!patientResponse.Success || patientResponse.Data == null)
            {
                var error = $"Paciente {patientId} não encontrado: {patientResponse.ErrorMessage}";
                _logger.LogWarning("⚠️ {Error}", error);
                return new ProcessingResult
                {
                    Success = false,
                    ErrorMessage = error,
                    ProcessingTime = DateTime.Now - startTime
                };
            }

            var patient = patientResponse.Data;
            _logger.LogInformation("✅ Paciente encontrado: {PatientName}", $"{patient.FirstName} {patient.LastName}");

            // 2. Buscar pedidos de exames para o paciente
            var ordersResponse = await _vidaApiClient.GetOrdersForPatientAsync(patientId, cancellationToken);
            if (!ordersResponse.Success || ordersResponse.Data == null || !ordersResponse.Data.Any())
            {
                var error = $"Nenhum exame pendente encontrado para paciente {patientId}";
                _logger.LogWarning("⚠️ {Error}", error);
                return new ProcessingResult
                {
                    Success = false,
                    ErrorMessage = error,
                    ProcessingTime = DateTime.Now - startTime
                };
            }

            var orders = ordersResponse.Data;
            _logger.LogInformation("📋 Encontrados {OrderCount} pedidos de exames", orders.Count);

            // 3. Processar cada pedido
            var results = new List<ProcessingResult>();
            foreach (var order in orders)
            {
                var orderResult = await SendExamOrderToEquipmentAsync(order, cancellationToken);
                results.Add(orderResult);
                
                if (orderResult.Success)
                {
                    _logger.LogInformation("✅ Pedido {OrderId} enviado com sucesso", order.OrderId);
                }
                else
                {
                    _logger.LogError("❌ Falha ao enviar pedido {OrderId}: {Error}", order.OrderId, orderResult.ErrorMessage);
                }
            }

            var successCount = results.Count(r => r.Success);
            var totalTime = DateTime.Now - startTime;

            _logger.LogInformation("📊 Processamento concluído: {SuccessCount}/{TotalCount} pedidos enviados em {ProcessingTime}ms",
                successCount, results.Count, totalTime.TotalMilliseconds);

            return new ProcessingResult
            {
                Success = successCount > 0,
                ErrorMessage = successCount == results.Count ? null : $"{results.Count - successCount} pedidos falharam",
                ProcessingTime = totalTime,
                Metadata = new Dictionary<string, object>
                {
                    ["TotalOrders"] = results.Count,
                    ["SuccessfulOrders"] = successCount,
                    ["FailedOrders"] = results.Count - successCount,
                    ["PatientName"] = $"{patient.FirstName} {patient.LastName}"
                }
            };
        }
        catch (Exception ex)
        {
            var totalTime = DateTime.Now - startTime;
            _logger.LogError(ex, "❌ Erro ao solicitar exames para paciente {PatientId}", patientId);
            
            return new ProcessingResult
            {
                Success = false,
                ErrorMessage = $"Erro inesperado: {ex.Message}",
                ProcessingTime = totalTime
            };
        }
    }

    public async Task<ProcessingResult> SendExamOrderToEquipmentAsync(ExamOrder order, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.Now;
        
        try
        {
            _logger.LogInformation("📤 Enviando pedido {OrderId} para equipamento", order.OrderId);
            _logger.LogInformation("🧪 Testes solicitados: {TestCodes}", string.Join(", ", order.TestCodes));

            // 1. Buscar dados do paciente
            var patientResponse = await _vidaApiClient.GetPatientAsync(order.PatientId, cancellationToken);
            if (!patientResponse.Success || patientResponse.Data == null)
            {
                var error = $"Paciente {order.PatientId} não encontrado para pedido {order.OrderId}";
                _logger.LogError("❌ {Error}", error);
                return new ProcessingResult
                {
                    Success = false,
                    ErrorMessage = error,
                    ProcessingTime = DateTime.Now - startTime
                };
            }

            var patient = patientResponse.Data;

            // 2. Construir mensagem ASTM
            var astmMessage = _astmMessageBuilder.BuildOrderMessage(order, patient);
            
            _logger.LogInformation("📄 Mensagem ASTM construída: {MessageSize} bytes", astmMessage.Length);
            _logger.LogDebug("📄 Mensagem HEX: {MessageHex}", Convert.ToHexString(astmMessage));

            // 3. Aqui seria enviado para o equipamento via TCP/Serial
            // Por enquanto, vamos simular o envio
            await Task.Delay(100, cancellationToken); // Simular latência de rede

            // 4. Atualizar status do pedido
            await _vidaApiClient.UpdateOrderStatusAsync(order.OrderId, "SENT_TO_EQUIPMENT", cancellationToken);

            var processingTime = DateTime.Now - startTime;
            _logger.LogInformation("✅ Pedido {OrderId} enviado com sucesso em {ProcessingTime}ms", 
                order.OrderId, processingTime.TotalMilliseconds);

            return new ProcessingResult
            {
                Success = true,
                ProcessingTime = processingTime,
                Metadata = new Dictionary<string, object>
                {
                    ["OrderId"] = order.OrderId,
                    ["PatientId"] = order.PatientId,
                    ["TestCount"] = order.TestCodes.Count,
                    ["MessageSize"] = astmMessage.Length,
                    ["PatientName"] = $"{patient.FirstName} {patient.LastName}"
                }
            };
        }
        catch (Exception ex)
        {
            var processingTime = DateTime.Now - startTime;
            _logger.LogError(ex, "❌ Erro ao enviar pedido {OrderId} para equipamento", order.OrderId);
            
            return new ProcessingResult
            {
                Success = false,
                ErrorMessage = $"Erro ao enviar pedido: {ex.Message}",
                ProcessingTime = processingTime
            };
        }
    }

    public async Task<ProcessingResult> ProcessExamResultsAsync(List<ExamResult> results, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.Now;
        
        try
        {
            _logger.LogInformation("🔬 Processando {ResultCount} resultados de exames", results.Count);

            if (!results.Any())
            {
                return new ProcessingResult
                {
                    Success = true,
                    ProcessingTime = DateTime.Now - startTime,
                    Metadata = new Dictionary<string, object> { ["ResultCount"] = 0 }
                };
            }

            // Agrupar resultados por paciente
            var resultsByPatient = results.GroupBy(r => r.PatientId).ToList();
            
            foreach (var patientGroup in resultsByPatient)
            {
                var patientId = patientGroup.Key;
                var patientResults = patientGroup.ToList();
                
                _logger.LogInformation("👤 Processando {ResultCount} resultados para paciente {PatientId}", 
                    patientResults.Count, patientId);

                // Buscar dados do paciente
                var patientResponse = await _vidaApiClient.GetPatientAsync(patientId, cancellationToken);
                var patient = patientResponse.Success ? patientResponse.Data : null;

                // Criar batch de resultados
                var batch = new ExamBatch
                {
                    BatchId = Guid.NewGuid().ToString(),
                    Patient = patient,
                    Results = patientResults,
                    CreatedAt = DateTime.Now,
                    Status = BatchStatus.Processing
                };

                // Processar batch
                var batchResult = await _messageProcessor.ProcessBatchAsync(batch, cancellationToken);
                
                if (batchResult.Success)
                {
                    _logger.LogInformation("✅ Batch {BatchId} processado com sucesso", batch.BatchId);
                }
                else
                {
                    _logger.LogError("❌ Falha ao processar batch {BatchId}: {Error}", batch.BatchId, batchResult.ErrorMessage);
                }
            }

            var processingTime = DateTime.Now - startTime;
            _logger.LogInformation("📊 Processamento de resultados concluído em {ProcessingTime}ms", 
                processingTime.TotalMilliseconds);

            return new ProcessingResult
            {
                Success = true,
                ProcessingTime = processingTime,
                Metadata = new Dictionary<string, object>
                {
                    ["ResultCount"] = results.Count,
                    ["PatientCount"] = resultsByPatient.Count
                }
            };
        }
        catch (Exception ex)
        {
            var processingTime = DateTime.Now - startTime;
            _logger.LogError(ex, "❌ Erro ao processar resultados de exames");
            
            return new ProcessingResult
            {
                Success = false,
                ErrorMessage = $"Erro ao processar resultados: {ex.Message}",
                ProcessingTime = processingTime
            };
        }
    }

    public async Task<ProcessingResult> SimulateUrineExamAsync(string patientId, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.Now;
        
        try
        {
            _logger.LogInformation("🧪 Simulando exame de urina completo para paciente {PatientId}", patientId);

            // 1. Solicitar exames
            var requestResult = await RequestExamsForPatientAsync(patientId, cancellationToken);
            if (!requestResult.Success)
            {
                return requestResult;
            }

            _logger.LogInformation("✅ Solicitação enviada, aguardando resultados simulados...");

            // 2. Simular espera pelo equipamento
            await Task.Delay(2000, cancellationToken); // 2 segundos para simular processamento

            // 3. Simular resultados de urina
            var simulatedResults = GenerateSimulatedUrineResults(patientId);
            
            _logger.LogInformation("🔬 Simulando recebimento de {ResultCount} resultados", simulatedResults.Count);

            // 4. Processar resultados simulados
            var processResult = await ProcessExamResultsAsync(simulatedResults, cancellationToken);

            var totalTime = DateTime.Now - startTime;
            
            if (processResult.Success)
            {
                _logger.LogInformation("🎉 Simulação de exame de urina concluída com sucesso em {TotalTime}ms", 
                    totalTime.TotalMilliseconds);
                
                // Log dos resultados para visualização
                LogUrineResults(simulatedResults);
            }

            return new ProcessingResult
            {
                Success = processResult.Success,
                ErrorMessage = processResult.ErrorMessage,
                ProcessingTime = totalTime,
                Metadata = new Dictionary<string, object>
                {
                    ["SimulatedResults"] = simulatedResults.Count,
                    ["PatientId"] = patientId,
                    ["ExamType"] = "Urine"
                }
            };
        }
        catch (Exception ex)
        {
            var totalTime = DateTime.Now - startTime;
            _logger.LogError(ex, "❌ Erro na simulação de exame de urina para paciente {PatientId}", patientId);
            
            return new ProcessingResult
            {
                Success = false,
                ErrorMessage = $"Erro na simulação: {ex.Message}",
                ProcessingTime = totalTime
            };
        }
    }

    private List<ExamResult> GenerateSimulatedUrineResults(string patientId)
    {
        var random = new Random();
        var results = new List<ExamResult>();
        var testDateTime = DateTime.Now;

        // Resultados típicos de exame de urina
        var urineTests = new Dictionary<string, (string[] possibleValues, string unit, string normalRange)>
        {
            ["PROTEIN"] = (new[] { "Negativo", "Traços", "1+", "2+", "3+" }, "", "Negativo"),
            ["GLUCOSE"] = (new[] { "Negativo", "Traços", "1+", "2+", "3+", "4+" }, "", "Negativo"),
            ["KETONES"] = (new[] { "Negativo", "Traços", "Pequeno", "Moderado", "Grande" }, "", "Negativo"),
            ["BLOOD"] = (new[] { "Negativo", "Traços", "1+", "2+", "3+" }, "", "Negativo"),
            ["NITRITE"] = (new[] { "Negativo", "Positivo" }, "", "Negativo"),
            ["LEUKOCYTES"] = (new[] { "Negativo", "Traços", "1+", "2+", "3+" }, "", "Negativo"),
            ["SG"] = (new[] { "1.005", "1.010", "1.015", "1.020", "1.025", "1.030" }, "", "1.003-1.030"),
            ["PH"] = (new[] { "5.0", "5.5", "6.0", "6.5", "7.0", "7.5", "8.0" }, "", "4.6-8.0"),
            ["UROBILINOGEN"] = (new[] { "Normal", "0.2", "1.0", "2.0", "4.0" }, "mg/dL", "0.2-1.0"),
            ["BILIRUBIN"] = (new[] { "Negativo", "1+", "2+", "3+" }, "", "Negativo")
        };

        foreach (var test in urineTests)
        {
            var testCode = test.Key;
            var (possibleValues, unit, normalRange) = test.Value;
            var value = possibleValues[random.Next(possibleValues.Length)];
            
            // Determinar flag anormal
            var abnormalFlag = "N"; // Normal
            if (testCode == "PROTEIN" && value != "Negativo") abnormalFlag = "H";
            if (testCode == "GLUCOSE" && value != "Negativo") abnormalFlag = "H";
            if (testCode == "KETONES" && value != "Negativo") abnormalFlag = "H";
            if (testCode == "BLOOD" && value != "Negativo") abnormalFlag = "H";
            if (testCode == "NITRITE" && value == "Positivo") abnormalFlag = "H";
            if (testCode == "LEUKOCYTES" && value != "Negativo") abnormalFlag = "H";

            results.Add(new ExamResult
            {
                PatientId = patientId,
                SpecimenId = "SPEC001",
                TestCode = testCode,
                TestName = GetTestName(testCode),
                Value = value,
                Units = unit,
                ReferenceRange = normalRange,
                AbnormalFlag = abnormalFlag,
                Status = "F", // Final
                TestDateTime = testDateTime,
                ProcessedDateTime = DateTime.Now,
                InstrumentId = "PPC125_SIM",
                OriginalMessage = $"Simulated result for {testCode}"
            });
        }

        return results;
    }

    private string GetTestName(string testCode)
    {
        return testCode switch
        {
            "PROTEIN" => "Proteína",
            "GLUCOSE" => "Glicose",
            "KETONES" => "Cetonas",
            "BLOOD" => "Sangue",
            "NITRITE" => "Nitrito",
            "LEUKOCYTES" => "Leucócitos",
            "SG" => "Densidade Específica",
            "PH" => "pH",
            "UROBILINOGEN" => "Urobilinogênio",
            "BILIRUBIN" => "Bilirrubina",
            _ => testCode
        };
    }

    private void LogUrineResults(List<ExamResult> results)
    {
        _logger.LogInformation("=== RESULTADOS DO EXAME DE URINA ===");
        
        foreach (var result in results.OrderBy(r => r.TestCode))
        {
            var flag = result.AbnormalFlag == "H" ? "⚠️" : "✅";
            _logger.LogInformation("{Flag} {TestName}: {Value} {Units} (Ref: {Range})", 
                flag, result.TestName, result.Value, result.Units, result.ReferenceRange);
        }
        
        _logger.LogInformation("=====================================");
    }
}
