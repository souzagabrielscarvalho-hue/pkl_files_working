namespace PklBridge.Core.Models;

/// <summary>
/// Resposta da API VIDA ao buscar exames por etiqueta
/// GET /api/integration/pkl-125
/// </summary>
public class VidaExamResponse
{
    public string Message { get; set; } = string.Empty;
    public List<VidaExamData> Data { get; set; } = new();
}

/// <summary>
/// Dados de um exame retornado pela API VIDA
/// Inclui informações do paciente e detalhes do exame
/// </summary>
public class VidaExamData
{
    [System.Text.Json.Serialization.JsonPropertyName("exam_code")]
    public string ExamCode { get; set; } = string.Empty;
    
    [System.Text.Json.Serialization.JsonPropertyName("test")]
    public string Test { get; set; } = string.Empty;
    
    [System.Text.Json.Serialization.JsonPropertyName("patient_name")]
    public string PatientName { get; set; } = string.Empty;
    
    [System.Text.Json.Serialization.JsonPropertyName("birth_date")]
    public string BirthDate { get; set; } = string.Empty;
    
    [System.Text.Json.Serialization.JsonPropertyName("gender")]
    public string Gender { get; set; } = string.Empty;
    
    [System.Text.Json.Serialization.JsonPropertyName("age")]
    public int Age { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("sample_type")]
    public List<string> SampleType { get; set; } = new();
}

/// <summary>
/// Request para enviar resultados para API VIDA
/// POST /api/integration/pkl-125
/// </summary>
public class VidaResultRequest
{
    public string FranchiseCredentialId { get; set; } = string.Empty;
    public string TagId { get; set; } = string.Empty;
    public List<VidaResultData> Results { get; set; } = new();
}

/// <summary>
/// Dados de um resultado a ser enviado para API VIDA
/// </summary>
public class VidaResultData
{
    public string ExamCode { get; set; } = string.Empty;
    public string Test { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// Resposta da API VIDA ao enviar resultados
/// </summary>
public class VidaResultResponse
{
    public string Message { get; set; } = string.Empty;
    public List<VidaResultProcessed> Data { get; set; } = new();
}

/// <summary>
/// Dados de um resultado processado pela API VIDA
/// </summary>
public class VidaResultProcessed
{
    public int ProcedureResultId { get; set; }
    public string ExamCode { get; set; } = string.Empty;
    public string Test { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// Request para solicitar exames (usado internamente)
/// </summary>
public class ExamRequest
{
    public string TagId { get; set; } = string.Empty;
    public string PatientId { get; set; } = string.Empty;
    public List<VidaExamData> Tests { get; set; } = new();
    public DateTime RequestedAt { get; set; } = DateTime.Now;
}
