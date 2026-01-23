using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PklBridge.Core.Configuration;
using PklBridge.Core.Interfaces;
using PklBridge.Core.Models;
using System.Collections.Concurrent;

namespace PklBridge.Infrastructure;

/// <summary>
/// Processa resultados recebidos do HLAB e envia para API VIDA
/// </summary>
public class ResultProcessor
{
    private readonly ILogger<ResultProcessor> _logger;
    private readonly IVidaApiClient _vidaClient;
    private readonly VidaApiSettings _vidaSettings;
    private readonly IAstmParser _astmParser;
    
    // Armazena resultados agrupados por tag_id até receber EOT
    private readonly ConcurrentDictionary<string, ResultBatch> _pendingResults = new();
    
    // Armazena informações de paciente por sessão
    private readonly ConcurrentDictionary<string, PatientInfo> _sessionPatients = new();

    public ResultProcessor(
        ILogger<ResultProcessor> logger,
        IVidaApiClient vidaClient,
        IOptions<VidaApiSettings> vidaSettings,
        IAstmParser astmParser)
    {
        _logger = logger;
        _vidaClient = vidaClient;
        _vidaSettings = vidaSettings.Value;
        _astmParser = astmParser;
    }

    /// <summary>
    /// Processa uma mensagem ASTM recebida do HLAB
    /// </summary>
    public async Task ProcessMessageAsync(AstmMessage message, string clientEndpoint, CancellationToken cancellationToken = default)
    {
        try
        {
            switch (message)
            {
                case AstmHeaderRecord header:
                    await HandleHeaderAsync(header, clientEndpoint);
                    break;
                    
                case AstmPatientRecord patient:
                    await HandlePatientAsync(patient, clientEndpoint);
                    break;
                    
                case AstmOrderRecord order:
                    await HandleOrderAsync(order, clientEndpoint);
                    break;
                    
                case AstmResultRecord result:
                    await HandleResultAsync(result, clientEndpoint);
                    break;
                    
                default:
                    _logger.LogDebug("Tipo de mensagem não processado: {MessageType}", message.Type);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar mensagem ASTM do tipo {MessageType}", message.Type);
        }
    }

    /// <summary>
    /// Finaliza uma sessão e envia resultados acumulados para API VIDA
    /// </summary>
    public async Task FinalizeSessionAsync(string clientEndpoint, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("[HLAB→VIDA] Finalizando sessão {ClientEndpoint}", clientEndpoint);
            
            // Buscar todos os batches pendentes para este cliente
            var batchesToSend = _pendingResults
                .Where(kvp => kvp.Value.ClientEndpoint == clientEndpoint)
                .ToList();
            
            if (batchesToSend.Count == 0)
            {
                _logger.LogDebug("Nenhum resultado pendente para enviar");
                return;
            }
            
            foreach (var kvp in batchesToSend)
            {
                var tagId = kvp.Key;
                var batch = kvp.Value;
                
                if (batch.Results.Count == 0)
                {
                    _logger.LogWarning("Batch {TagId} não contém resultados", tagId);
                    continue;
                }
                
                await SendResultsToVidaAsync(tagId, batch, cancellationToken);
                
                // Remover batch após envio
                _pendingResults.TryRemove(tagId, out _);
            }
            
            // Limpar informações de paciente da sessão
            _sessionPatients.TryRemove(clientEndpoint, out _);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao finalizar sessão {ClientEndpoint}", clientEndpoint);
        }
    }

    private Task HandleHeaderAsync(AstmHeaderRecord header, string clientEndpoint)
    {
        _logger.LogDebug("[HLAB→Bridge] Header recebido | Sender: {SenderId}", header.SenderId);
        return Task.CompletedTask;
    }

    private Task HandlePatientAsync(AstmPatientRecord patient, string clientEndpoint)
    {
        _logger.LogDebug("[HLAB→Bridge] Patient recebido | ID: {PatientId} | Nome: {FirstName} {LastName}", 
            patient.PatientId, patient.FirstName, patient.LastName);
        
        // Armazenar informações do paciente para esta sessão
        _sessionPatients[clientEndpoint] = new PatientInfo
        {
            PatientId = patient.PatientId,
            FirstName = patient.FirstName,
            LastName = patient.LastName,
            BirthDate = patient.BirthDate,
            Gender = patient.Gender
        };
        
        return Task.CompletedTask;
    }

    private Task HandleOrderAsync(AstmOrderRecord order, string clientEndpoint)
    {
        _logger.LogDebug("[HLAB→Bridge] Order recebido | Specimen: {SpecimenId} | Tests: {TestCount}", 
            order.SpecimenId, order.TestCodes.Count);
        
        // Inicializar batch para este specimen/tag se ainda não existe
        if (!_pendingResults.ContainsKey(order.SpecimenId))
        {
            _pendingResults[order.SpecimenId] = new ResultBatch
            {
                TagId = order.SpecimenId,
                ClientEndpoint = clientEndpoint,
                OrderDateTime = order.RequestedDateTime ?? DateTime.Now
            };
        }
        
        return Task.CompletedTask;
    }

    private Task HandleResultAsync(AstmResultRecord result, string clientEndpoint)
    {
        try
        {
            _logger.LogDebug("[HLAB→Bridge] Result recebido | Test: {TestName} | Value: {Value}", 
                result.TestName, result.Value);
            
            // Extrair tag_id do InstrumentId ou usar o último specimen conhecido
            var tagId = ExtractTagIdFromResult(result, clientEndpoint);
            
            if (string.IsNullOrEmpty(tagId))
            {
                _logger.LogWarning("Não foi possível determinar tag_id para resultado: {TestName}", result.TestName);
                return Task.CompletedTask;
            }
            
            // Garantir que existe um batch para este tag_id
            if (!_pendingResults.ContainsKey(tagId))
            {
                _pendingResults[tagId] = new ResultBatch
                {
                    TagId = tagId,
                    ClientEndpoint = clientEndpoint,
                    OrderDateTime = DateTime.Now
                };
            }
            
            var batch = _pendingResults[tagId];
            
            // Parsear exam_code e test do UniversalTestId
            // Formato esperado: ^^^EXAM_CODE ou similar
            var (examCode, test) = ParseUniversalTestId(result.UniversalTestId, result.TestName);
            
            // Adicionar resultado ao batch
            batch.Results.Add(new VidaResultData
            {
                ExamCode = examCode,
                Test = test,
                Value = result.Value
            });
            
            _logger.LogInformation("✅ Resultado adicionado | Tag: {TagId} | Exam: {ExamCode} | Test: {Test} | Value: {Value}", 
                tagId, examCode, test, result.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar resultado");
        }
        
        return Task.CompletedTask;
    }

    private string ExtractTagIdFromResult(AstmResultRecord result, string clientEndpoint)
    {
        // Tentar extrair do InstrumentId
        if (!string.IsNullOrEmpty(result.InstrumentId))
        {
            return result.InstrumentId;
        }
        
        // Caso contrário, usar o último batch conhecido para este cliente
        var lastBatch = _pendingResults.Values
            .Where(b => b.ClientEndpoint == clientEndpoint)
            .OrderByDescending(b => b.OrderDateTime)
            .FirstOrDefault();
        
        return lastBatch?.TagId ?? string.Empty;
    }

    private (string examCode, string test) ParseUniversalTestId(string universalTestId, string testName)
    {
        // Formato ASTM: ^^^TEST_CODE ou EXAM_CODE^^^TEST_CODE
        // Exemplo: ^^^GLI ou HEMO^^^WBC
        
        if (string.IsNullOrEmpty(universalTestId))
        {
            // Fallback: usar testName como test code
            return ("UNKNOWN", testName);
        }
        
        var parts = universalTestId.Split(new[] { '^' }, StringSplitOptions.RemoveEmptyEntries);
        
        if (parts.Length == 0)
        {
            return ("UNKNOWN", testName);
        }
        
        if (parts.Length == 1)
        {
            // Apenas test code
            return (parts[0], parts[0]);
        }
        
        // exam_code^^^test_code
        return (parts[0], parts[parts.Length - 1]);
    }

    private async Task SendResultsToVidaAsync(string tagId, ResultBatch batch, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrEmpty(_vidaSettings.FranchiseCredentialId))
            {
                _logger.LogError("❌ FranchiseCredentialId não configurado! Não é possível enviar resultados.");
                return;
            }
            
            _logger.LogInformation("[Bridge→VIDA] Enviando {ResultCount} resultados para tag {TagId}", 
                batch.Results.Count, tagId);
            
            var request = new VidaResultRequest
            {
                FranchiseCredentialId = _vidaSettings.FranchiseCredentialId,
                TagId = tagId,
                Results = batch.Results
            };
            
            var response = await _vidaClient.SendResultsByTagAsync(request, cancellationToken);
            
            if (response.Success && response.Data != null)
            {
                var processedCount = response.Data.Data?.Count ?? 0;
                _logger.LogInformation("✅ Resultados enviados com sucesso | Tag: {TagId} | Processados: {ProcessedCount}/{TotalCount}", 
                    tagId, processedCount, batch.Results.Count);
                
                if (processedCount < batch.Results.Count)
                {
                    _logger.LogWarning("⚠️ Alguns resultados não foram processados | Tag: {TagId} | Esperados: {Expected} | Processados: {Processed}", 
                        tagId, batch.Results.Count, processedCount);
                }
            }
            else
            {
                _logger.LogError("❌ Falha ao enviar resultados | Tag: {TagId} | Erro: {Error}", 
                    tagId, response.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao enviar resultados para API VIDA | Tag: {TagId}", tagId);
        }
    }

    /// <summary>
    /// Limpa batches antigos que não foram finalizados
    /// </summary>
    public void CleanupOldBatches(TimeSpan maxAge)
    {
        try
        {
            var cutoffTime = DateTime.Now - maxAge;
            var oldBatches = _pendingResults
                .Where(kvp => kvp.Value.OrderDateTime < cutoffTime)
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (var tagId in oldBatches)
            {
                if (_pendingResults.TryRemove(tagId, out var batch))
                {
                    _logger.LogWarning("⚠️ Batch {TagId} removido por timeout | Resultados: {ResultCount}", 
                        tagId, batch.Results.Count);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao limpar batches antigos");
        }
    }
}

/// <summary>
/// Batch de resultados agrupados por tag_id
/// </summary>
internal class ResultBatch
{
    public string TagId { get; set; } = string.Empty;
    public string ClientEndpoint { get; set; } = string.Empty;
    public DateTime OrderDateTime { get; set; }
    public List<VidaResultData> Results { get; set; } = new();
}

/// <summary>
/// Informações do paciente da sessão atual
/// </summary>
internal class PatientInfo
{
    public string PatientId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime? BirthDate { get; set; }
    public string Gender { get; set; } = string.Empty;
}
