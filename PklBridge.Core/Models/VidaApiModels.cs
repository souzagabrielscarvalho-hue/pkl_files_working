namespace PklBridge.Core.Models;

/// <summary>
/// Resposta da API VIDA ao buscar exames por etiqueta
/// GET /api/integration/pkl-125
/// </summary>
public class VidaExamResponse
{
    public string Message { get; set; } = string.Empty;
    public List<VidaExamData> Data { get; set; } = new();
    public int RackPosition { get; set; } = 1;
    public int PositionNumber { get; set; } = 1;
}

/// <summary>
/// Dados de um exame retornado pela API VIDA
/// </summary>
public class VidaExamData
{
    public string ExamCode { get; set; } = string.Empty;
    public string Test { get; set; } = string.Empty;
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
    public string ProcedureResultId { get; set; } = string.Empty;
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
