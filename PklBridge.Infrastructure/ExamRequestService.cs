using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PklBridge.Core.Configuration;
using PklBridge.Core.Interfaces;
using PklBridge.Core.Models;
using PklBridge.Infrastructure.Serial;
using System.Text;

namespace PklBridge.Infrastructure;

/// <summary>
/// Serviço responsável por solicitar exames ao equipamento PKL 125
/// Integra API VIDA → ASTM Message Builder → Session Manager → HLAB/PKL
/// </summary>
public class ExamRequestService
{
    private readonly ILogger<ExamRequestService> _logger;
    private readonly IVidaApiClient _vidaApiClient;
    private readonly AstmMessageBuilder _astmBuilder;
    private readonly AstmSessionManager _sessionManager;
    private readonly VidaApiSettings _vidaSettings;
    private readonly TcpServer _tcpServer;

    public ExamRequestService(
        ILogger<ExamRequestService> logger,
        IVidaApiClient vidaApiClient,
        AstmMessageBuilder astmBuilder,
        AstmSessionManager sessionManager,
        IOptions<VidaApiSettings> vidaSettings,
        TcpServer tcpServer)
    {
        _logger = logger;
        _vidaApiClient = vidaApiClient;
        _astmBuilder = astmBuilder;
        _sessionManager = sessionManager;
        _vidaSettings = vidaSettings.Value;
        _tcpServer = tcpServer;
    }

    /// <summary>
    /// Solicita exames para uma etiqueta específica
    /// Fluxo: Busca exames na API VIDA → Gera mensagem ASTM → Envia para HLAB/PKL
    /// </summary>
    public async Task<ExamRequestResult> RequestExamsByTagAsync(string tagId, string? clientEndpoint = null, CancellationToken cancellationToken = default)
    {
        var result = new ExamRequestResult { TagId = tagId };
        
        try
        {
            _logger.LogInformation("🔍 Iniciando solicitação de exames para etiqueta {TagId}", tagId);

            // 1. Buscar exames na API VIDA
            _logger.LogDebug("📡 Buscando exames na API VIDA para etiqueta {TagId}", tagId);
            
            var apiResponse = await _vidaApiClient.GetExamsByTagAsync(
                _vidaSettings.FranchiseCredentialId, 
                tagId, 
                cancellationToken);

            if (!apiResponse.Success || apiResponse.Data == null)
            {
                result.Success = false;
                result.ErrorMessage = apiResponse.ErrorMessage ?? "Falha ao buscar exames na API VIDA";
                _logger.LogError("❌ {ErrorMessage}", result.ErrorMessage);
                return result;
            }

            var exams = apiResponse.Data.Data;
            
            if (exams == null || exams.Count == 0)
            {
                result.Success = false;
                result.ErrorMessage = $"Nenhum exame encontrado para etiqueta {tagId}";
                _logger.LogWarning("⚠️ {ErrorMessage}", result.ErrorMessage);
                return result;
            }

            // Validar dados do paciente (primeiro exame contém os dados)
            var firstExam = exams.First();
            if (string.IsNullOrEmpty(firstExam.PatientName))
            {
                _logger.LogWarning("⚠️ Dados de paciente incompletos para etiqueta {TagId}", tagId);
            }
            else
            {
                _logger.LogInformation("👤 Paciente: {PatientName}, Idade: {Age}, Gênero: {Gender}", 
                    firstExam.PatientName, firstExam.Age, firstExam.Gender);
            }

            _logger.LogInformation("✅ Encontrados {ExamCount} exames: {ExamCodes}", 
                exams.Count, string.Join(", ", exams.Select(e => e.ExamCode)));

            result.ExamsFound = exams.Count;
            result.ExamCodes = exams.Select(e => e.ExamCode).ToList();

            // 2. Gerar mensagem ASTM Order
            _logger.LogDebug("📝 Gerando mensagem ASTM Order para paciente {PatientName}", 
                firstExam.PatientName);
            
            var astmMessage = BuildAstmOrderMessage(tagId, apiResponse.Data);
            result.AstmMessage = astmMessage;

            _logger.LogDebug("📄 Mensagem ASTM gerada: {MessageLength} caracteres", astmMessage.Length);
            
            if (_logger.IsEnabled(LogLevel.Trace))
            {
                _logger.LogTrace("Mensagem ASTM completa:\n{AstmMessage}", astmMessage);
            }

            // 3. Determinar cliente endpoint (se não fornecido, usar o primeiro conectado)
            if (string.IsNullOrEmpty(clientEndpoint))
            {
                // Em implementação real, deveria pegar da lista de clientes conectados
                clientEndpoint = "default_client";
                _logger.LogDebug("Cliente endpoint não especificado, usando: {ClientEndpoint}", clientEndpoint);
            }

            // 4. Enviar mensagem ASTM para HLAB/PKL via Session Manager
            _logger.LogInformation("📤 Enviando mensagem ASTM para {ClientEndpoint}", clientEndpoint);
            
            var sendSuccess = await _sessionManager.SendAstmMessageAsync(
                clientEndpoint, 
                astmMessage,
                isResponseToQuery: false,  // Solicita exames, não é resposta
                cancellationToken);

            if (!sendSuccess)
            {
                result.Success = false;
                result.ErrorMessage = "Falha ao enviar mensagem ASTM para o equipamento";
                _logger.LogError("❌ {ErrorMessage}", result.ErrorMessage);
                return result;
            }

            // 5. Sucesso!
            result.Success = true;
            result.SentAt = DateTime.Now;
            
            _logger.LogInformation("✅ Solicitação de exames para etiqueta {TagId} concluída com sucesso", tagId);
            
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Erro ao solicitar exames: {ex.Message}";
            _logger.LogError(ex, "❌ Erro ao solicitar exames para etiqueta {TagId}", tagId);
            return result;
        }
    }

    /// <summary>
    /// Constrói mensagem ASTM Order completa usando dados da API VIDA
    /// </summary>
    private string BuildAstmOrderMessage(string tagId, VidaExamResponse examResponse)
    {
        var message = new StringBuilder();
        var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");

        // Pegar dados do primeiro exame (todos têm os mesmos dados de paciente)
        var firstExam = examResponse.Data.FirstOrDefault();
        
        // Valores padrão se não houver dados
        var patientName = firstExam?.PatientName ?? $"Paciente {tagId}";
        var birthDate = firstExam?.BirthDate ?? DateTime.Now.AddYears(-30).ToString("yyyy-MM-dd");
        var gender = firstExam?.Gender ?? "M";
        var sampleType = firstExam?.SampleType?.FirstOrDefault() ?? "SORO";

        // Converter data de nascimento para formato ASTM (yyyyMMdd)
        var birthDateFormatted = DateTime.TryParse(birthDate, out var parsedDate) 
            ? parsedDate.ToString("yyyyMMdd") 
            : DateTime.Now.AddYears(-30).ToString("yyyyMMdd");

        // H - Header Record
        message.AppendLine($"H|\\^&|||PKL Bridge^1.0^PKL125|||||||P|1|{timestamp}");

        // P - Patient Record - Usando dados reais da API VIDA
        // P|seq|Practice ID|Lab ID|ID3|Patient Name|Mother Name|DOB|Sex|Race|Address|Reserved|Phone|Physician|Special1|Special2|Height|Weight|Diagnosis|Medication|Diet|Practice1|Practice2|Admission|Discharge|Attending|Specialty
        message.AppendLine($"P|1|||{tagId}|{patientName}||{birthDateFormatted}|{gender}||||||||||||||||||||");

        // O - Order Record - Formato correto segundo log.txt: ID^^^^Type
        var sampleId = $"{tagId}^^^^N";
        var testCodes = string.Join("`", examResponse.Data.Select(e => $"^^^{e.Test}"));
        
        // Usar tipo de amostra da API (SORO, PLASMA, etc.)
        message.AppendLine($"O|2|{sampleId}|{tagId}|{testCodes}|R|{timestamp}|||||||||{sampleType}||||||||||O");

        // L - Terminator Record
        message.AppendLine($"L|1|N");

        return message.ToString();
    }

    /// <summary>
    /// Processa resultados recebidos do equipamento e envia para API VIDA
    /// </summary>
    public async Task<ResultProcessingResult> ProcessResultsAsync(
        string tagId, 
        List<ExamResult> results, 
        CancellationToken cancellationToken = default)
    {
        var processingResult = new ResultProcessingResult { TagId = tagId };
        
        try
        {
            _logger.LogInformation("🔬 Processando {ResultCount} resultados para etiqueta {TagId}", 
                results.Count, tagId);

            // Mapear ExamResult para VidaResultData
            var vidaResults = results.Select(r => new VidaResultData
            {
                ExamCode = r.TestCode,
                Test = r.TestCode, // Assumindo que TestCode é o mesmo que Test
                Value = r.Value
            }).ToList();

            // Criar request para API VIDA
            var request = new VidaResultRequest
            {
                FranchiseCredentialId = _vidaSettings.FranchiseCredentialId,
                TagId = tagId,
                Results = vidaResults
            };

            // Enviar para API VIDA
            _logger.LogDebug("📤 Enviando {ResultCount} resultados para API VIDA", vidaResults.Count);
            
            var apiResponse = await _vidaApiClient.SendResultsByTagAsync(request, cancellationToken);

            if (!apiResponse.Success || apiResponse.Data == null)
            {
                processingResult.Success = false;
                processingResult.ErrorMessage = apiResponse.ErrorMessage ?? "Falha ao enviar resultados para API VIDA";
                _logger.LogError("❌ {ErrorMessage}", processingResult.ErrorMessage);
                return processingResult;
            }

            // Analisar resposta
            var processedCount = apiResponse.Data.Data?.Count ?? 0;
            var failedCount = vidaResults.Count - processedCount;

            processingResult.Success = true;
            processingResult.TotalResults = vidaResults.Count;
            processingResult.ProcessedResults = processedCount;
            processingResult.FailedResults = failedCount;
            processingResult.ProcessedAt = DateTime.Now;

            if (failedCount > 0)
            {
                _logger.LogWarning("⚠️ {ProcessedCount}/{TotalCount} resultados processados. {FailedCount} falharam.", 
                    processedCount, vidaResults.Count, failedCount);
            }
            else
            {
                _logger.LogInformation("✅ Todos os {ResultCount} resultados processados com sucesso", processedCount);
            }

            return processingResult;
        }
        catch (Exception ex)
        {
            processingResult.Success = false;
            processingResult.ErrorMessage = $"Erro ao processar resultados: {ex.Message}";
            _logger.LogError(ex, "❌ Erro ao processar resultados para etiqueta {TagId}", tagId);
            return processingResult;
        }
    }
}

/// <summary>
/// Resultado da solicitação de exames
/// </summary>
public class ExamRequestResult
{
    public bool Success { get; set; }
    public string TagId { get; set; } = string.Empty;
    public int ExamsFound { get; set; }
    public List<string> ExamCodes { get; set; } = new();
    public string? AstmMessage { get; set; }
    public DateTime? SentAt { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Resultado do processamento de resultados
/// </summary>
public class ResultProcessingResult
{
    public bool Success { get; set; }
    public string TagId { get; set; } = string.Empty;
    public int TotalResults { get; set; }
    public int ProcessedResults { get; set; }
    public int FailedResults { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
