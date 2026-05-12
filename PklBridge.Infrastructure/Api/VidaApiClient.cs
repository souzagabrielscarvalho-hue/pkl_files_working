using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PklBridge.Core.Configuration;
using PklBridge.Core.Interfaces;
using PklBridge.Core.Models;
using Polly;
using Polly.Extensions.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace PklBridge.Infrastructure.Api;

public class VidaApiClient : IVidaApiClient
{
    private readonly ILogger<VidaApiClient> _logger;
    private readonly HttpClient _httpClient;
    private readonly VidaApiSettings _settings;
    private readonly JsonSerializerOptions _jsonOptions;
    
    public VidaApiClient(
        ILogger<VidaApiClient> logger,
        HttpClient httpClient,
        IOptions<VidaApiSettings> settings)
    {
        _logger = logger;
        _httpClient = httpClient;
        _settings = settings.Value;
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
        };

        ConfigureHttpClient();
    }

    public async Task<ApiResponse<bool>> SendResultsAsync(ExamBatch batch, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.Now;
        
        try
        {
            _logger.LogInformation("Sending batch {BatchId} with {ResultCount} results to VIDA API", 
                batch.BatchId, batch.Results.Count);

            // Prepare the payload
            var payload = new
            {
                BatchId = batch.BatchId,
                Timestamp = batch.CreatedAt,
                Patient = batch.Patient != null ? new
                {
                    Id = batch.Patient.Id,
                    FirstName = batch.Patient.FirstName,
                    LastName = batch.Patient.LastName,
                    BirthDate = batch.Patient.BirthDate,
                    Gender = batch.Patient.Gender,
                    Location = batch.Patient.Location,
                    ExternalId = batch.Patient.ExternalId
                } : null,
                Results = batch.Results.Select(r => new
                {
                    PatientId = r.PatientId,
                    SpecimenId = r.SpecimenId,
                    TestCode = r.TestCode,
                    TestName = r.TestName,
                    Value = r.Value,
                    Units = r.Units,
                    ReferenceRange = r.ReferenceRange,
                    AbnormalFlag = r.AbnormalFlag,
                    Status = r.Status,
                    TestDateTime = r.TestDateTime,
                    ProcessedDateTime = r.ProcessedDateTime,
                    InstrumentId = r.InstrumentId,
                    OriginalMessage = r.OriginalMessage
                }).ToArray()
            };

            var jsonContent = JsonSerializer.Serialize(payload, _jsonOptions);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            _logger.LogDebug("Sending POST request to {Endpoint} with payload: {Payload}", 
                _settings.ResultEndpoint, jsonContent);

            var response = await _httpClient.PostAsync(_settings.ResultEndpoint, httpContent, cancellationToken);
            var duration = DateTime.Now - startTime;

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully sent batch {BatchId} to VIDA API in {Duration}ms. Status: {StatusCode}", 
                    batch.BatchId, duration.TotalMilliseconds, response.StatusCode);

                return ApiResponse<bool>.SuccessResult(true, (int)response.StatusCode, duration);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var errorMessage = $"API returned {response.StatusCode}: {errorContent}";
                
                _logger.LogError("Failed to send batch {BatchId} to VIDA API. Status: {StatusCode}, Error: {Error}", 
                    batch.BatchId, response.StatusCode, errorContent);

                return ApiResponse<bool>.ErrorResult(errorMessage, (int)response.StatusCode, duration);
            }
        }
        catch (HttpRequestException ex)
        {
            var duration = DateTime.Now - startTime;
            _logger.LogError(ex, "HTTP error sending batch {BatchId} to VIDA API", batch.BatchId);
            return ApiResponse<bool>.ErrorResult($"HTTP error: {ex.Message}", 500, duration);
        }
        catch (TaskCanceledException ex)
        {
            var duration = DateTime.Now - startTime;
            var message = ex.CancellationToken.IsCancellationRequested ? "Request was cancelled" : "Request timed out";
            _logger.LogError(ex, "Request timeout/cancellation sending batch {BatchId} to VIDA API", batch.BatchId);
            return ApiResponse<bool>.ErrorResult(message, 408, duration);
        }
        catch (Exception ex)
        {
            var duration = DateTime.Now - startTime;
            _logger.LogError(ex, "Unexpected error sending batch {BatchId} to VIDA API", batch.BatchId);
            return ApiResponse<bool>.ErrorResult($"Unexpected error: {ex.Message}", 500, duration);
        }
    }

    public async Task<ApiResponse<PatientData>> GetPatientAsync(string patientId, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.Now;
        
        try
        {
            if (string.IsNullOrEmpty(patientId))
            {
                return ApiResponse<PatientData>.ErrorResult("Patient ID is required", 400);
            }

            _logger.LogDebug("Getting patient {PatientId} from VIDA API", patientId);

            var endpoint = $"{_settings.PatientEndpoint}/{Uri.EscapeDataString(patientId)}";
            var response = await _httpClient.GetAsync(endpoint, cancellationToken);
            var duration = DateTime.Now - startTime;

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var patient = JsonSerializer.Deserialize<PatientData>(content, _jsonOptions);
                
                _logger.LogDebug("Successfully retrieved patient {PatientId} from VIDA API", patientId);
                
                return ApiResponse<PatientData>.SuccessResult(patient, (int)response.StatusCode, duration);
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogInformation("Patient {PatientId} not found in VIDA API", patientId);
                return ApiResponse<PatientData>.ErrorResult("Patient not found", 404, duration);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var errorMessage = $"API returned {response.StatusCode}: {errorContent}";
                
                _logger.LogError("Error getting patient {PatientId} from VIDA API. Status: {StatusCode}, Error: {Error}", 
                    patientId, response.StatusCode, errorContent);

                return ApiResponse<PatientData>.ErrorResult(errorMessage, (int)response.StatusCode, duration);
            }
        }
        catch (Exception ex)
        {
            var duration = DateTime.Now - startTime;
            _logger.LogError(ex, "Error getting patient {PatientId} from VIDA API", patientId);
            return ApiResponse<PatientData>.ErrorResult($"Error: {ex.Message}", 500, duration);
        }
    }

    public async Task<ApiResponse<List<ExamOrder>>> GetPendingOrdersAsync(CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.Now;
        
        try
        {
            _logger.LogDebug("Getting pending orders from VIDA API");

            var endpoint = $"{_settings.ResultEndpoint}/pending";
            var response = await _httpClient.GetAsync(endpoint, cancellationToken);
            var duration = DateTime.Now - startTime;

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var orders = JsonSerializer.Deserialize<List<ExamOrder>>(content, _jsonOptions) ?? new List<ExamOrder>();
                
                _logger.LogDebug("Successfully retrieved {OrderCount} pending orders from VIDA API", orders.Count);
                
                return ApiResponse<List<ExamOrder>>.SuccessResult(orders, (int)response.StatusCode, duration);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var errorMessage = $"API returned {response.StatusCode}: {errorContent}";
                
                _logger.LogError("Error getting pending orders from VIDA API. Status: {StatusCode}, Error: {Error}", 
                    response.StatusCode, errorContent);

                return ApiResponse<List<ExamOrder>>.ErrorResult(errorMessage, (int)response.StatusCode, duration);
            }
        }
        catch (Exception ex)
        {
            var duration = DateTime.Now - startTime;
            _logger.LogError(ex, "Error getting pending orders from VIDA API");
            return ApiResponse<List<ExamOrder>>.ErrorResult($"Error: {ex.Message}", 500, duration);
        }
    }

    public async Task<ApiResponse<List<ExamOrder>>> GetOrdersForPatientAsync(string patientId, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.Now;
        
        try
        {
            if (string.IsNullOrEmpty(patientId))
            {
                return ApiResponse<List<ExamOrder>>.ErrorResult("Patient ID is required", 400);
            }

            _logger.LogDebug("Getting orders for patient {PatientId} from VIDA API", patientId);

            var endpoint = $"{_settings.PatientEndpoint}/{Uri.EscapeDataString(patientId)}/orders";
            var response = await _httpClient.GetAsync(endpoint, cancellationToken);
            var duration = DateTime.Now - startTime;

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var orders = JsonSerializer.Deserialize<List<ExamOrder>>(content, _jsonOptions) ?? new List<ExamOrder>();
                
                _logger.LogDebug("Successfully retrieved {OrderCount} orders for patient {PatientId} from VIDA API", orders.Count, patientId);
                
                return ApiResponse<List<ExamOrder>>.SuccessResult(orders, (int)response.StatusCode, duration);
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogInformation("No orders found for patient {PatientId} in VIDA API", patientId);
                return ApiResponse<List<ExamOrder>>.SuccessResult(new List<ExamOrder>(), 200, duration);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var errorMessage = $"API returned {response.StatusCode}: {errorContent}";
                
                _logger.LogError("Error getting orders for patient {PatientId} from VIDA API. Status: {StatusCode}, Error: {Error}", 
                    patientId, response.StatusCode, errorContent);

                return ApiResponse<List<ExamOrder>>.ErrorResult(errorMessage, (int)response.StatusCode, duration);
            }
        }
        catch (Exception ex)
        {
            var duration = DateTime.Now - startTime;
            _logger.LogError(ex, "Error getting orders for patient {PatientId} from VIDA API", patientId);
            return ApiResponse<List<ExamOrder>>.ErrorResult($"Error: {ex.Message}", 500, duration);
        }
    }

    public async Task<ApiResponse<bool>> UpdateOrderStatusAsync(string orderId, string status, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.Now;
        
        try
        {
            if (string.IsNullOrEmpty(orderId) || string.IsNullOrEmpty(status))
            {
                return ApiResponse<bool>.ErrorResult("Order ID and status are required", 400);
            }

            _logger.LogDebug("Updating order {OrderId} status to {Status} in VIDA API", orderId, status);

            var payload = new { Status = status, UpdatedAt = DateTime.Now };
            var endpoint = $"{_settings.ResultEndpoint}/orders/{Uri.EscapeDataString(orderId)}/status";
            
            var response = await _httpClient.PutAsJsonAsync(endpoint, payload, _jsonOptions, cancellationToken);
            var duration = DateTime.Now - startTime;

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("Successfully updated order {OrderId} status to {Status}", orderId, status);
                return ApiResponse<bool>.SuccessResult(true, (int)response.StatusCode, duration);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var errorMessage = $"API returned {response.StatusCode}: {errorContent}";
                
                _logger.LogError("Error updating order {OrderId} status. Status: {StatusCode}, Error: {Error}", 
                    orderId, response.StatusCode, errorContent);

                return ApiResponse<bool>.ErrorResult(errorMessage, (int)response.StatusCode, duration);
            }
        }
        catch (Exception ex)
        {
            var duration = DateTime.Now - startTime;
            _logger.LogError(ex, "Error updating order {OrderId} status to {Status}", orderId, status);
            return ApiResponse<bool>.ErrorResult($"Error: {ex.Message}", 500, duration);
        }
    }

    public async Task<ApiResponse<bool>> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.Now;
        
        try
        {
            _logger.LogDebug("Testing connection to VIDA API");

            // Try to call a simple health check endpoint
            var endpoint = "/api/health";
            var response = await _httpClient.GetAsync(endpoint, cancellationToken);
            var duration = DateTime.Now - startTime;

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("VIDA API connection test successful");
                return ApiResponse<bool>.SuccessResult(true, (int)response.StatusCode, duration);
            }
            else
            {
                _logger.LogWarning("VIDA API connection test failed with status {StatusCode}", response.StatusCode);
                return ApiResponse<bool>.ErrorResult($"Connection test failed: {response.StatusCode}", (int)response.StatusCode, duration);
            }
        }
        catch (Exception ex)
        {
            var duration = DateTime.Now - startTime;
            _logger.LogError(ex, "Error testing VIDA API connection");
            return ApiResponse<bool>.ErrorResult($"Connection error: {ex.Message}", 500, duration);
        }
    }

    public async Task<ApiResponse<VidaExamResponse>> GetExamsByTagAsync(string franchiseCredentialId, string tagId, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.Now;
        
        try
        {
            if (string.IsNullOrEmpty(franchiseCredentialId) || string.IsNullOrEmpty(tagId))
            {
                return ApiResponse<VidaExamResponse>.ErrorResult("Franchise credential ID and tag ID are required", 400);
            }

            _logger.LogInformation("Buscando exames para etiqueta {TagId} na API VIDA", tagId);

            var endpoint = $"{_settings.IntegrationEndpoint}?franchise_credential_id={Uri.EscapeDataString(franchiseCredentialId)}&tag_id={Uri.EscapeDataString(tagId)}";
            
            _logger.LogDebug("GET {Endpoint}", endpoint);
            
            var response = await _httpClient.GetAsync(endpoint, cancellationToken);
            var duration = DateTime.Now - startTime;

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogDebug("Resposta da API VIDA: {Content}", content);
                
                var examResponse = JsonSerializer.Deserialize<VidaExamResponse>(content, _jsonOptions);
                
                if (examResponse != null && examResponse.Data != null)
                {
                    _logger.LogInformation("✅ Encontrados {ExamCount} exames para etiqueta {TagId}: {ExamCodes}", 
                        examResponse.Data.Count, tagId, string.Join(", ", examResponse.Data.Select(e => e.ExamCode)));
                    
                    return ApiResponse<VidaExamResponse>.SuccessResult(examResponse, (int)response.StatusCode, duration);
                }
                else
                {
                    _logger.LogWarning("Resposta da API VIDA não contém dados válidos para etiqueta {TagId}", tagId);
                    return ApiResponse<VidaExamResponse>.ErrorResult("Resposta inválida da API", (int)response.StatusCode, duration);
                }
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Etiqueta {TagId} não encontrada na API VIDA", tagId);
                return ApiResponse<VidaExamResponse>.ErrorResult("Etiqueta não encontrada", 404, duration);
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var errorMessage = $"API retornou {response.StatusCode}: {errorContent}";
                
                _logger.LogError("Erro ao buscar exames para etiqueta {TagId}. Status: {StatusCode}, Erro: {Error}", 
                    tagId, response.StatusCode, errorContent);

                return ApiResponse<VidaExamResponse>.ErrorResult(errorMessage, (int)response.StatusCode, duration);
            }
        }
        catch (Exception ex)
        {
            var duration = DateTime.Now - startTime;
            _logger.LogError(ex, "Erro ao buscar exames para etiqueta {TagId} na API VIDA", tagId);
            return ApiResponse<VidaExamResponse>.ErrorResult($"Erro: {ex.Message}", 500, duration);
        }
    }

    public async Task<ApiResponse<VidaResultResponse>> SendResultsByTagAsync(VidaResultRequest request, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.Now;
        
        try
        {
            if (request == null || string.IsNullOrEmpty(request.FranchiseCredentialId) || string.IsNullOrEmpty(request.TagId))
            {
                return ApiResponse<VidaResultResponse>.ErrorResult("Request inválido: franchise_credential_id e tag_id são obrigatórios", 400);
            }

            if (request.Results == null || request.Results.Count == 0)
            {
                return ApiResponse<VidaResultResponse>.ErrorResult("Request inválido: lista de resultados está vazia", 400);
            }

            _logger.LogInformation("Enviando {ResultCount} resultados para etiqueta {TagId} na API VIDA", 
                request.Results.Count, request.TagId);

            var jsonContent = JsonSerializer.Serialize(request, _jsonOptions);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            _logger.LogDebug("POST {Endpoint} com payload: {Payload}", _settings.IntegrationEndpoint, jsonContent);

            var response = await _httpClient.PostAsync(_settings.IntegrationEndpoint, httpContent, cancellationToken);
            var duration = DateTime.Now - startTime;

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogDebug("Resposta da API VIDA: {Content}", content);
                
                var resultResponse = JsonSerializer.Deserialize<VidaResultResponse>(content, _jsonOptions);
                
                if (resultResponse != null)
                {
                    var processedCount = resultResponse.Data?.Count ?? 0;
                    var failedCount = request.Results.Count - processedCount;
                    
                    if (failedCount > 0)
                    {
                        _logger.LogWarning("⚠️ {ProcessedCount}/{TotalCount} resultados processados para etiqueta {TagId}. {FailedCount} falharam.", 
                            processedCount, request.Results.Count, request.TagId, failedCount);
                    }
                    else
                    {
                        _logger.LogInformation("✅ Todos os {ResultCount} resultados enviados com sucesso para etiqueta {TagId}", 
                            processedCount, request.TagId);
                    }
                    
                    return ApiResponse<VidaResultResponse>.SuccessResult(resultResponse, (int)response.StatusCode, duration);
                }
                else
                {
                    _logger.LogWarning("Resposta da API VIDA não contém dados válidos para etiqueta {TagId}", request.TagId);
                    return ApiResponse<VidaResultResponse>.ErrorResult("Resposta inválida da API", (int)response.StatusCode, duration);
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                var errorMessage = $"API retornou {response.StatusCode}: {errorContent}";
                
                _logger.LogError("Erro ao enviar resultados para etiqueta {TagId}. Status: {StatusCode}, Erro: {Error}", 
                    request.TagId, response.StatusCode, errorContent);

                return ApiResponse<VidaResultResponse>.ErrorResult(errorMessage, (int)response.StatusCode, duration);
            }
        }
        catch (Exception ex)
        {
            var duration = DateTime.Now - startTime;
            _logger.LogError(ex, "Erro ao enviar resultados para etiqueta {TagId} na API VIDA", request?.TagId ?? "unknown");
            return ApiResponse<VidaResultResponse>.ErrorResult($"Erro: {ex.Message}", 500, duration);
        }
    }

    private void ConfigureHttpClient()
    {
        if (!string.IsNullOrEmpty(_settings.BaseUrl))
        {
            _httpClient.BaseAddress = new Uri(_settings.BaseUrl);
        }

        _httpClient.Timeout = TimeSpan.FromSeconds(_settings.TimeoutSeconds);

        // Add authentication headers
        if (!string.IsNullOrEmpty(_settings.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", _settings.ApiKey);
        }

        // Add common headers
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "PKL-Bridge/1.0");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

        _logger.LogInformation("HTTP client configured for VIDA API: {BaseUrl}", _settings.BaseUrl);
    }
}

// Extension class for additional HTTP client configuration
public static class VidaApiHttpClientExtensions
{
    public static IServiceCollection AddVidaApiHttpClient(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection(VidaApiSettings.SectionName).Get<VidaApiSettings>();
        
        if (settings == null)
        {
            throw new InvalidOperationException("VidaApi settings not found in configuration");
        }

        services.AddHttpClient<VidaApiClient>(client =>
        {
            if (!string.IsNullOrEmpty(settings.BaseUrl))
            {
                client.BaseAddress = new Uri(settings.BaseUrl);
            }
            
            client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
        });

        return services;
    }
}
