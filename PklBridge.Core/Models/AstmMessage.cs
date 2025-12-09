namespace PklBridge.Core.Models;

public class AstmMessage
{
    public string Type { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int SequenceNumber { get; set; }
    public string Checksum { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public bool IsValid { get; set; }
    
    public Dictionary<string, object> Fields { get; set; } = new();
}

public class AstmHeaderRecord : AstmMessage
{
    public string SenderId { get; set; } = string.Empty;
    public string ReceiverId { get; set; } = string.Empty;
    public DateTime MessageDateTime { get; set; }
    public string Version { get; set; } = string.Empty;
}

public class AstmPatientRecord : AstmMessage
{
    public string PatientId { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public DateTime? BirthDate { get; set; }
    public string Gender { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
}

public class AstmOrderRecord : AstmMessage
{
    public string SpecimenId { get; set; } = string.Empty;
    public string UniversalTestId { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public DateTime? RequestedDateTime { get; set; }
    public DateTime? CollectionDateTime { get; set; }
    public string ActionCode { get; set; } = string.Empty;
    public List<string> TestCodes { get; set; } = new();
}

public class AstmResultRecord : AstmMessage
{
    public string UniversalTestId { get; set; } = string.Empty;
    public string TestName { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Units { get; set; } = string.Empty;
    public string ReferenceRange { get; set; } = string.Empty;
    public string AbnormalFlag { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime TestDateTime { get; set; } = DateTime.Now;
    public string InstrumentId { get; set; } = string.Empty;
}

public class AstmQueryRecord : AstmMessage
{
    public string StartingRangeId { get; set; } = string.Empty;
    public string EndingRangeId { get; set; } = string.Empty;
    public string UniversalTestId { get; set; } = string.Empty;
    public string NatureOfRequest { get; set; } = string.Empty;
    public DateTime? BeginningRequestTime { get; set; }
    public DateTime? EndingRequestTime { get; set; }
}
