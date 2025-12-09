namespace PklBridge.Core.Models;

public class ExamResult
{
    public string PatientId { get; set; } = string.Empty;
    public string SpecimenId { get; set; } = string.Empty;
    public string TestCode { get; set; } = string.Empty;
    public string TestName { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Units { get; set; } = string.Empty;
    public string ReferenceRange { get; set; } = string.Empty;
    public string AbnormalFlag { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime TestDateTime { get; set; }
    public DateTime ProcessedDateTime { get; set; } = DateTime.Now;
    public string InstrumentId { get; set; } = string.Empty;
    public string OriginalMessage { get; set; } = string.Empty;
    
    public Dictionary<string, object> AdditionalData { get; set; } = new();
}

public class PatientData
{
    public string Id { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public DateTime? BirthDate { get; set; }
    public string Gender { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    
    public Dictionary<string, object> CustomFields { get; set; } = new();
}

public class ExamBatch
{
    public string BatchId { get; set; } = string.Empty;
    public PatientData? Patient { get; set; }
    public List<ExamResult> Results { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? ProcessedAt { get; set; }
    public BatchStatus Status { get; set; } = BatchStatus.Pending;
    public string? ErrorMessage { get; set; }
    public int RetryCount { get; set; }
}

public enum BatchStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    Cancelled
}

public class ProcessingResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime ProcessedAt { get; set; } = DateTime.Now;
    public TimeSpan ProcessingTime { get; set; }
    public Dictionary<string, object> Metadata { get; set; } = new();
}
