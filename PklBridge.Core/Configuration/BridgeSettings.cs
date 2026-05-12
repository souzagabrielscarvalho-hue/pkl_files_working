using System.Text.Json.Serialization;

namespace PklBridge.Core.Configuration;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TransportMode
{
    Tcp,
    Serial
}

public class BridgeSettings
{
    public const string SectionName = "BridgeSettings";

    public TransportMode TransportMode { get; set; } = TransportMode.Tcp;
    public string PipeName { get; set; } = "pkl_serial";
    public string? RealComPort { get; set; }
    public SerialSettings Serial { get; set; } = new();
    public int BufferSize { get; set; } = 4096;
    public int TimeoutMs { get; set; } = 5000;
    public bool EnableHardwareBridge { get; set; } = false;
    public bool EnableTcpServer { get; set; } = false;
    public int TcpPort { get; set; } = 8080;
    public bool LogAllTraffic { get; set; } = true;
}

public class SerialSettings
{
    public int BaudRate { get; set; } = 19200;
    public int DataBits { get; set; } = 8;
    public string Parity { get; set; } = "None";
    public string StopBits { get; set; } = "One";
    public string Handshake { get; set; } = "None";
    public int ReadTimeout { get; set; } = 5000;
    public int WriteTimeout { get; set; } = 5000;
    public bool DtrEnable { get; set; } = false;
    public bool RtsEnable { get; set; } = false;
}

public class VidaApiSettings
{
    public const string SectionName = "VidaApi";
    
    public string BaseUrl { get; set; } = string.Empty;
    public string ResultEndpoint { get; set; } = "/api/exams/results";
    public string PatientEndpoint { get; set; } = "/api/patients";
    public string AuthEndpoint { get; set; } = "/api/auth/token";
    public string? ApiKey { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxRetryAttempts { get; set; } = 3;
    public int RetryDelayMs { get; set; } = 1000;
    public bool EnableCircuitBreaker { get; set; } = true;
    public int CircuitBreakerThreshold { get; set; } = 5;
    public int CircuitBreakerTimeoutMinutes { get; set; } = 5;
    
    // Configurações específicas da API VIDA Siqueira
    public string FranchiseCredentialId { get; set; } = "88cf9273-5044-47f4-b8f6-01160345a190";
    public string IntegrationEndpoint { get; set; } = "/api/integration/pkl-125";
}

public class AstmSettings
{
    public const string SectionName = "AstmSettings";
    
    public bool ValidateChecksum { get; set; } = true;
    public bool StrictParsing { get; set; } = true;
    public char FieldSeparator { get; set; } = '|';
    public char ComponentSeparator { get; set; } = '^';
    public char RepeatSeparator { get; set; } = '\\';
    public char EscapeSeparator { get; set; } = '&';
    public string DateTimeFormat { get; set; } = "yyyyMMddHHmmss";
    public List<string> SupportedRecordTypes { get; set; } = new() { "H", "P", "O", "R", "Q", "L" };
}

public class ProcessingSettings
{
    public const string SectionName = "ProcessingSettings";
    
    public int MaxBatchSize { get; set; } = 100;
    public int BatchTimeoutSeconds { get; set; } = 300;
    public int MaxConcurrentProcessing { get; set; } = 5;
    public bool EnableDeduplication { get; set; } = true;
    public int DeduplicationWindowMinutes { get; set; } = 60;
    public bool EnableMessageQueue { get; set; } = true;
    public int QueueMaxSize { get; set; } = 1000;
}

public class HealthCheckSettings
{
    public const string SectionName = "HealthCheckSettings";
    
    public bool EnableHealthChecks { get; set; } = true;
    public int CheckIntervalSeconds { get; set; } = 30;
    public int PipeConnectionTimeoutMs { get; set; } = 5000;
    public int SerialPortTimeoutMs { get; set; } = 3000;
    public int ApiTimeoutMs { get; set; } = 10000;
    public string HealthCheckEndpoint { get; set; } = "/health";
}
