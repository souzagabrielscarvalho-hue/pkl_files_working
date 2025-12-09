using PklBridge.Core.Models;

namespace PklBridge.Core.Interfaces;

public interface IVidaApiClient
{
    // Métodos originais (mantidos para compatibilidade)
    Task<ApiResponse<bool>> SendResultsAsync(ExamBatch batch, CancellationToken cancellationToken = default);
    Task<ApiResponse<PatientData>> GetPatientAsync(string patientId, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<ExamOrder>>> GetPendingOrdersAsync(CancellationToken cancellationToken = default);
    Task<ApiResponse<List<ExamOrder>>> GetOrdersForPatientAsync(string patientId, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> UpdateOrderStatusAsync(string orderId, string status, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> TestConnectionAsync(CancellationToken cancellationToken = default);
    
    // Novos métodos para API VIDA real
    Task<ApiResponse<VidaExamResponse>> GetExamsByTagAsync(string franchiseCredentialId, string tagId, CancellationToken cancellationToken = default);
    Task<ApiResponse<VidaResultResponse>> SendResultsByTagAsync(VidaResultRequest request, CancellationToken cancellationToken = default);
}

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? ErrorMessage { get; set; }
    public int StatusCode { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public TimeSpan Duration { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();

    public static ApiResponse<T> SuccessResult(T data, int statusCode = 200, TimeSpan duration = default)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Data = data,
            StatusCode = statusCode,
            Duration = duration
        };
    }

    public static ApiResponse<T> ErrorResult(string errorMessage, int statusCode = 500, TimeSpan duration = default)
    {
        return new ApiResponse<T>
        {
            Success = false,
            ErrorMessage = errorMessage,
            StatusCode = statusCode,
            Duration = duration
        };
    }
}

public class ExamOrder
{
    public string OrderId { get; set; } = string.Empty;
    public string PatientId { get; set; } = string.Empty;
    public string SpecimenId { get; set; } = string.Empty;
    public List<string> TestCodes { get; set; } = new();
    public DateTime OrderDateTime { get; set; }
    public string Priority { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Dictionary<string, object> CustomFields { get; set; } = new();
}

public interface INamedPipeServer
{
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
    bool IsConnected { get; }
    Task<byte[]> ReadAsync(CancellationToken cancellationToken = default);
    Task WriteAsync(byte[] data, CancellationToken cancellationToken = default);
    event EventHandler<ClientConnectedEventArgs>? ClientConnected;
    event EventHandler<ClientDisconnectedEventArgs>? ClientDisconnected;
    event EventHandler<DataReceivedEventArgs>? DataReceived;
}

public class ClientConnectedEventArgs : EventArgs
{
    public DateTime ConnectedAt { get; } = DateTime.Now;
    public string ClientInfo { get; }

    public ClientConnectedEventArgs(string clientInfo = "")
    {
        ClientInfo = clientInfo;
    }
}

public class ClientDisconnectedEventArgs : EventArgs
{
    public DateTime DisconnectedAt { get; } = DateTime.Now;
    public string Reason { get; }

    public ClientDisconnectedEventArgs(string reason = "")
    {
        Reason = reason;
    }
}

public class DataReceivedEventArgs : EventArgs
{
    public byte[] Data { get; }
    public DateTime ReceivedAt { get; } = DateTime.Now;
    public int Length { get; }

    public DataReceivedEventArgs(byte[] data)
    {
        Data = data;
        Length = data.Length;
    }
}
