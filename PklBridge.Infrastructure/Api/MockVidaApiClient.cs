using Microsoft.Extensions.Logging;
using PklBridge.Core.Interfaces;
using PklBridge.Core.Models;

namespace PklBridge.Infrastructure.Api;

public class MockVidaApiClient : IVidaApiClient
{
    private readonly ILogger<MockVidaApiClient> _logger;
    
    // Mock database de pacientes e exames
    private readonly Dictionary<string, PatientData> _mockPatients;
    private readonly Dictionary<string, List<ExamOrder>> _mockOrders;

    public MockVidaApiClient(ILogger<MockVidaApiClient> logger)
    {
        _logger = logger;
        _mockPatients = InitializeMockPatients();
        _mockOrders = InitializeMockOrders();
    }

    public async Task<ApiResponse<bool>> SendResultsAsync(ExamBatch batch, CancellationToken cancellationToken = default)
    {
        // REMOVIDO DELAY - Resposta instantânea para HLAB
        await Task.CompletedTask; // Mantém async mas sem delay

        _logger.LogInformation("MOCK: Sending batch {BatchId} with {ResultCount} results", 
            batch.BatchId, batch.Results.Count);

        // Log the results for debugging
        foreach (var result in batch.Results)
        {
            _logger.LogInformation("MOCK: Result - Patient: {PatientId}, Test: {TestCode} = {Value} {Units}", 
                result.PatientId, result.TestCode, result.Value, result.Units);
        }

        return ApiResponse<bool>.SuccessResult(true, 200, TimeSpan.FromMilliseconds(100));
    }

    public async Task<ApiResponse<PatientData>> GetPatientAsync(string patientId, CancellationToken cancellationToken = default)
    {
        // REMOVIDO DELAY - Resposta instantânea para HLAB
        await Task.CompletedTask; // Mantém async mas sem delay

        _logger.LogInformation("MOCK: Getting patient {PatientId}", patientId);

        // Buscar paciente no mock database
        if (_mockPatients.TryGetValue(patientId, out var patient))
        {
            return ApiResponse<PatientData>.SuccessResult(patient, 200, TimeSpan.FromMilliseconds(50));
        }

        // Se não encontrar, criar um paciente padrão
        var mockPatient = new PatientData
        {
            Id = patientId,
            FirstName = "João",
            LastName = "Silva",
            BirthDate = new DateTime(1980, 5, 15),
            Gender = "M",
            Location = "Enfermaria 1",
            ExternalId = patientId
        };

        return ApiResponse<PatientData>.SuccessResult(mockPatient, 200, TimeSpan.FromMilliseconds(50));
    }

    public async Task<ApiResponse<List<ExamOrder>>> GetPendingOrdersAsync(CancellationToken cancellationToken = default)
    {
        // REMOVIDO DELAY - Resposta instantânea para HLAB
        await Task.CompletedTask; // Mantém async mas sem delay

        _logger.LogInformation("MOCK: Getting pending orders");

        // Retornar todos os pedidos pendentes do mock database
        var allOrders = _mockOrders.Values.SelectMany(orders => orders).ToList();
        
        return ApiResponse<List<ExamOrder>>.SuccessResult(allOrders, 200, TimeSpan.FromMilliseconds(75));
    }

    public async Task<ApiResponse<List<ExamOrder>>> GetOrdersForPatientAsync(string patientId, CancellationToken cancellationToken = default)
    {
        // REMOVIDO DELAY - Resposta instantânea para HLAB
        await Task.CompletedTask; // Mantém async mas sem delay

        _logger.LogInformation("MOCK: Getting orders for patient {PatientId}", patientId);

        if (_mockOrders.TryGetValue(patientId, out var orders))
        {
            return ApiResponse<List<ExamOrder>>.SuccessResult(orders, 200, TimeSpan.FromMilliseconds(50));
        }

        return ApiResponse<List<ExamOrder>>.SuccessResult(new List<ExamOrder>(), 200, TimeSpan.FromMilliseconds(50));
    }

    public async Task<ApiResponse<bool>> UpdateOrderStatusAsync(string orderId, string status, CancellationToken cancellationToken = default)
    {
        // REMOVIDO DELAY - Resposta instantânea para HLAB
        await Task.CompletedTask; // Mantém async mas sem delay

        _logger.LogInformation("MOCK: Updating order {OrderId} status to {Status}", orderId, status);

        return ApiResponse<bool>.SuccessResult(true, 200, TimeSpan.FromMilliseconds(50));
    }

    public async Task<ApiResponse<bool>> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        // REMOVIDO DELAY - Resposta instantânea para HLAB
        await Task.CompletedTask; // Mantém async mas sem delay

        _logger.LogInformation("MOCK: API connection test successful");

        return ApiResponse<bool>.SuccessResult(true, 200, TimeSpan.FromMilliseconds(25));
    }

    public async Task<ApiResponse<VidaExamResponse>> GetExamsByTagAsync(string franchiseCredentialId, string tagId, CancellationToken cancellationToken = default)
    {
        // REMOVIDO DELAY - Resposta instantânea para HLAB
        await Task.CompletedTask; // Mantém async mas sem delay

        _logger.LogInformation("MOCK: Buscando exames para etiqueta {TagId}", tagId);

        // Simular resposta da API VIDA com exames mockados
        // IMPORTANTE: Position é um campo separado, não deve ser o mesmo que o tagId
        // A API VIDA real deve retornar a posição física do tubo no rack
        int rackPosition = 1;
        int positionNumber = 7;  // Exemplo: posição 7 no rack (independente do ID do paciente)
        
        // TODO: Quando integrar com API VIDA real, esses valores devem vir do sistema

        var response = new VidaExamResponse
        {
            Message = "Procedimentos encontrados com sucesso!",
            RackPosition = rackPosition,
            PositionNumber = positionNumber,
            Data = new List<VidaExamData>
            {
                new VidaExamData { ExamCode = "TCO", Test = "TCO" },
                new VidaExamData { ExamCode = "PCR", Test = "PCR" },
                new VidaExamData { ExamCode = "GLUC", Test = "GLUC" }
            }
        };

        return ApiResponse<VidaExamResponse>.SuccessResult(response, 200, TimeSpan.FromMilliseconds(50));
    }

    public async Task<ApiResponse<VidaResultResponse>> SendResultsByTagAsync(VidaResultRequest request, CancellationToken cancellationToken = default)
    {
        // REMOVIDO DELAY - Resposta instantânea para HLAB
        await Task.CompletedTask; // Mantém async mas sem delay

        _logger.LogInformation("MOCK: Enviando {ResultCount} resultados para etiqueta {TagId}", 
            request.Results.Count, request.TagId);

        // Simular processamento - todos os resultados são aceitos no mock
        var response = new VidaResultResponse
        {
            Message = "Resultados dos procedimentos atualizados com sucesso!",
            Data = request.Results.Select(r => new VidaResultProcessed
            {
                ProcedureResultId = Guid.NewGuid().ToString(),
                ExamCode = r.ExamCode,
                Test = r.Test,
                Value = r.Value
            }).ToList()
        };

        return ApiResponse<VidaResultResponse>.SuccessResult(response, 200, TimeSpan.FromMilliseconds(100));
    }

    private Dictionary<string, PatientData> InitializeMockPatients()
    {
        return new Dictionary<string, PatientData>
        {
            ["URINA001"] = new PatientData
            {
                Id = "URINA001",
                FirstName = "João",
                LastName = "Silva",
                BirthDate = new DateTime(1980, 5, 15),
                Gender = "M",
                Location = "Enfermaria 1",
                ExternalId = "URINA001"
            },
            ["URINA002"] = new PatientData
            {
                Id = "URINA002",
                FirstName = "Maria",
                LastName = "Santos",
                BirthDate = new DateTime(1975, 8, 22),
                Gender = "F",
                Location = "Enfermaria 2",
                ExternalId = "URINA002"
            },
            ["URINA003"] = new PatientData
            {
                Id = "URINA003",
                FirstName = "Pedro",
                LastName = "Costa",
                BirthDate = new DateTime(1990, 12, 10),
                Gender = "M",
                Location = "UTI",
                ExternalId = "URINA003"
            },
            ["14102025588"] = new PatientData
            {
                Id = "14102025588",
                FirstName = "Paciente",
                LastName = "Teste",
                BirthDate = new DateTime(1985, 10, 14),
                Gender = "M",
                Location = "Laboratório",
                ExternalId = "14102025588"
            },
            ["50"] = new PatientData
            {
                Id = "50",
                FirstName = "Paciente",
                LastName = "Cincuenta",
                BirthDate = new DateTime(1990, 5, 20),
                Gender = "M",
                Location = "Laboratório",
                ExternalId = "50"
            }
        };
    }

    private Dictionary<string, List<ExamOrder>> InitializeMockOrders()
    {
        return new Dictionary<string, List<ExamOrder>>
        {
            ["URINA001"] = new List<ExamOrder>
            {
                new ExamOrder
                {
                    OrderId = "ORD_URINA001_001",
                    PatientId = "URINA001",
                    SpecimenId = "SPEC001",
                    TestCodes = new List<string> { "PROTEIN", "GLUCOSE", "KETONES", "BLOOD", "NITRITE", "LEUKOCYTES", "SG", "PH" },
                    OrderDateTime = DateTime.Now.AddHours(-2),
                    Priority = "ROUTINE",
                    Status = "PENDING"
                }
            },
            ["URINA002"] = new List<ExamOrder>
            {
                new ExamOrder
                {
                    OrderId = "ORD_URINA002_001",
                    PatientId = "URINA002",
                    SpecimenId = "SPEC002",
                    TestCodes = new List<string> { "PROTEIN", "GLUCOSE", "KETONES", "SG", "PH" },
                    OrderDateTime = DateTime.Now.AddHours(-1),
                    Priority = "URGENT",
                    Status = "PENDING"
                }
            },
            ["URINA003"] = new List<ExamOrder>
            {
                new ExamOrder
                {
                    OrderId = "ORD_URINA003_001",
                    PatientId = "URINA003",
                    SpecimenId = "SPEC003",
                    TestCodes = new List<string> { "PROTEIN", "GLUCOSE", "KETONES", "BLOOD", "NITRITE", "LEUKOCYTES", "SG", "PH", "UROBILINOGEN", "BILIRUBIN" },
                    OrderDateTime = DateTime.Now.AddMinutes(-30),
                    Priority = "STAT",
                    Status = "PENDING"
                }
            },
            ["14102025588"] = new List<ExamOrder>
            {
                new ExamOrder
                {
                    OrderId = "ORD_14102025588_001",
                    PatientId = "14102025588",
                    SpecimenId = "SPEC_14102025588",
                    TestCodes = new List<string> { "GLICOSE", "CREAT", "COLESTEROL", "HDL", "LDH", "ALBUMINA", "GAMA_GT", "AST/TGO", "ALT/TGP", "FOSF_ALC" },
                    OrderDateTime = DateTime.Now,
                    Priority = "ROUTINE",
                    Status = "PENDING"
                }
            },
            ["50"] = new List<ExamOrder>
            {
                new ExamOrder
                {
                    OrderId = "ORD_50_001",
                    PatientId = "50",
                    SpecimenId = "SPEC_50",
                    TestCodes = new List<string> { "GLICOSE", "CREAT", "COLESTEROL", "HDL", "LDH", "ALBUMINA", "GAMA_GT", "AST/TGO", "ALT/TGP", "FOSF_ALC" },
                    OrderDateTime = DateTime.Now,
                    Priority = "ROUTINE",
                    Status = "PENDING"
                }
            }
        };
    }
}
