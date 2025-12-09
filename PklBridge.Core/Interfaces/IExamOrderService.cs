using PklBridge.Core.Models;

namespace PklBridge.Core.Interfaces;

public interface IExamOrderService
{
    /// <summary>
    /// Solicita exames para um paciente específico
    /// </summary>
    Task<ProcessingResult> RequestExamsForPatientAsync(string patientId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Envia uma solicitação de exame específica para o equipamento
    /// </summary>
    Task<ProcessingResult> SendExamOrderToEquipmentAsync(ExamOrder order, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Processa resultados recebidos do equipamento
    /// </summary>
    Task<ProcessingResult> ProcessExamResultsAsync(List<ExamResult> results, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Simula um exame de urina completo para teste
    /// </summary>
    Task<ProcessingResult> SimulateUrineExamAsync(string patientId, CancellationToken cancellationToken = default);
}

public interface IAstmMessageBuilder
{
    /// <summary>
    /// Constrói uma mensagem ASTM de solicitação de exame
    /// </summary>
    byte[] BuildOrderMessage(ExamOrder order, PatientData patient);
    
    /// <summary>
    /// Constrói uma mensagem ASTM de query para solicitar informações
    /// </summary>
    byte[] BuildQueryMessage(string patientId, string specimenId);
    
    /// <summary>
    /// Calcula o checksum ASTM correto
    /// </summary>
    byte CalculateChecksum(byte[] data, int startIndex, int endIndex);
    
    /// <summary>
    /// Valida o checksum de uma mensagem recebida
    /// </summary>
    bool ValidateChecksum(byte[] message);
}
