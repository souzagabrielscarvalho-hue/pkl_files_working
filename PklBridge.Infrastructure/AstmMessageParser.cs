using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PklBridge.Core.Configuration;
using PklBridge.Core.Interfaces;
using PklBridge.Core.Models;
using System.Text;

namespace PklBridge.Infrastructure;

public class AstmMessageParser : IAstmParser
{
    private readonly ILogger<AstmMessageParser> _logger;
    private readonly AstmSettings _settings;

    public AstmMessageParser(ILogger<AstmMessageParser> logger, IOptions<AstmSettings> settings)
    {
        _logger = logger;
        _settings = settings.Value;
    }

    public List<AstmMessage> Parse(byte[] data)
    {
        return Parse(Encoding.ASCII.GetString(data));
    }

    public List<AstmMessage> Parse(string data)
    {
        var messages = new List<AstmMessage>();

        if (string.IsNullOrEmpty(data))
        {
            return messages;
        }

        try
        {
            // Split data by STX to find individual frames
            var frames = SplitIntoFrames(data);
            
            foreach (var frame in frames)
            {
                var message = ParseSingleFrame(frame);
                if (message != null)
                {
                    messages.Add(message);
                }
            }

            _logger.LogDebug("Parsed {MessageCount} ASTM messages from {DataLength} bytes", 
                messages.Count, data.Length);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing ASTM messages from data: {Data}", 
                Convert.ToHexString(Encoding.ASCII.GetBytes(data)));
        }

        return messages;
    }

    private List<string> SplitIntoFrames(string data)
    {
        var frames = new List<string>();
        var stxPositions = new List<int>();

        // Find all STX positions
        for (int i = 0; i < data.Length; i++)
        {
            if (data[i] == '\x02') // STX
            {
                stxPositions.Add(i);
            }
        }

        // Extract frames
        for (int i = 0; i < stxPositions.Count; i++)
        {
            var stxPos = stxPositions[i];
            var nextStxPos = i + 1 < stxPositions.Count ? stxPositions[i + 1] : data.Length;
            
            var frameData = data.Substring(stxPos, nextStxPos - stxPos);
            frames.Add(frameData);
        }

        return frames;
    }

    private AstmMessage? ParseSingleFrame(string frame)
    {
        try
        {
            if (string.IsNullOrEmpty(frame) || !frame.StartsWith("\x02"))
            {
                return null;
            }

            var stxIndex = 0;
            var etxIndex = frame.IndexOf('\x03', stxIndex);
            
            if (etxIndex == -1)
            {
                _logger.LogWarning("Frame without ETX found: {Frame}", Convert.ToHexString(Encoding.ASCII.GetBytes(frame)));
                return null;
            }

            // Extract components
            var sequenceNumber = (int)frame[stxIndex + 1] - 48; // Convert ASCII to number
            var content = frame.Substring(stxIndex + 2, etxIndex - stxIndex - 2);
            var recordType = content.Length > 0 ? content[0].ToString() : "";

            // Extract checksum
            var checksum = "";
            if (etxIndex + 2 < frame.Length)
            {
                checksum = frame.Substring(etxIndex + 1, 2);
            }

            // Validate checksum
            var calculatedChecksum = CalculateChecksum(frame.Substring(stxIndex + 1, etxIndex - stxIndex));
            var isValidChecksum = checksum.Equals(calculatedChecksum, StringComparison.OrdinalIgnoreCase);

            if (_settings.ValidateChecksum && !isValidChecksum)
            {
                _logger.LogWarning("Invalid checksum in frame. Expected: {Expected}, Got: {Actual}", 
                    calculatedChecksum, checksum);
                
                if (_settings.StrictParsing)
                {
                    return null;
                }
            }

            // Create appropriate message type based on record type
            var message = CreateTypedMessage(recordType, content, sequenceNumber, checksum, isValidChecksum);
            
            if (message != null)
            {
                message.Content = content;
                message.Timestamp = DateTime.Now;
            }

            return message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing single frame: {Frame}", Convert.ToHexString(Encoding.ASCII.GetBytes(frame)));
            return null;
        }
    }

    private AstmMessage? CreateTypedMessage(string recordType, string content, int sequenceNumber, string checksum, bool isValidChecksum)
    {
        if (!_settings.SupportedRecordTypes.Contains(recordType))
        {
            _logger.LogDebug("Unsupported record type: {RecordType}", recordType);
            return null;
        }

        var fields = content.Split(_settings.FieldSeparator);

        AstmMessage message = recordType switch
        {
            "H" => CreateHeaderRecord(fields),
            "P" => CreatePatientRecord(fields),
            "O" => CreateOrderRecord(fields),
            "R" => CreateResultRecord(fields),
            "Q" => CreateQueryRecord(fields),
            "L" => new AstmMessage(),
            _ => new AstmMessage()
        };

        message.Type = recordType;
        message.SequenceNumber = sequenceNumber;
        message.Checksum = checksum;
        message.IsValid = isValidChecksum;

        return message;
    }

    private AstmHeaderRecord CreateHeaderRecord(string[] fields)
    {
        return new AstmHeaderRecord
        {
            SenderId = GetFieldValue(fields, 4),
            ReceiverId = GetFieldValue(fields, 9),
            MessageDateTime = ParseDateTime(GetFieldValue(fields, 13)),
            Version = GetFieldValue(fields, 12)
        };
    }

    private AstmPatientRecord CreatePatientRecord(string[] fields)
    {
        return new AstmPatientRecord
        {
            PatientId = GetFieldValue(fields, 2),
            LastName = GetComponent(GetFieldValue(fields, 5), 0),
            FirstName = GetComponent(GetFieldValue(fields, 5), 1),
            BirthDate = ParseDateTimeNullable(GetFieldValue(fields, 7)),
            Gender = GetFieldValue(fields, 8),
            Location = GetFieldValue(fields, 15)
        };
    }

    private AstmOrderRecord CreateOrderRecord(string[] fields)
    {
        return new AstmOrderRecord
        {
            SpecimenId = GetFieldValue(fields, 2),
            UniversalTestId = GetFieldValue(fields, 4),
            Priority = GetFieldValue(fields, 5),
            RequestedDateTime = ParseDateTimeNullable(GetFieldValue(fields, 6)),
            CollectionDateTime = ParseDateTimeNullable(GetFieldValue(fields, 7)),
            ActionCode = GetFieldValue(fields, 11),
            TestCodes = GetFieldValue(fields, 4).Split(_settings.RepeatSeparator).ToList()
        };
    }

    private AstmResultRecord CreateResultRecord(string[] fields)
    {
        return new AstmResultRecord
        {
            UniversalTestId = GetFieldValue(fields, 2),
            TestName = GetComponent(GetFieldValue(fields, 2), 1),
            Value = GetFieldValue(fields, 3),
            Units = GetFieldValue(fields, 4),
            ReferenceRange = GetFieldValue(fields, 5),
            AbnormalFlag = GetFieldValue(fields, 6),
            Status = GetFieldValue(fields, 8),
            TestDateTime = ParseDateTimeNullable(GetFieldValue(fields, 12)) ?? DateTime.Now,
            InstrumentId = GetFieldValue(fields, 18)
        };
    }

    private AstmQueryRecord CreateQueryRecord(string[] fields)
    {
        return new AstmQueryRecord
        {
            StartingRangeId = GetFieldValue(fields, 2),
            EndingRangeId = GetFieldValue(fields, 3),
            UniversalTestId = GetFieldValue(fields, 4),
            NatureOfRequest = GetFieldValue(fields, 5),
            BeginningRequestTime = ParseDateTimeNullable(GetFieldValue(fields, 6)),
            EndingRequestTime = ParseDateTimeNullable(GetFieldValue(fields, 7))
        };
    }

    public List<ExamResult> ExtractResults(List<AstmMessage> messages)
    {
        var results = new List<ExamResult>();
        
        try
        {
            var patientInfo = messages.OfType<AstmPatientRecord>().FirstOrDefault();
            var resultMessages = messages.OfType<AstmResultRecord>().ToList();

            foreach (var result in resultMessages)
            {
                var examResult = new ExamResult
                {
                    PatientId = patientInfo?.PatientId ?? "",
                    SpecimenId = GetSpecimenIdFromMessages(messages),
                    TestCode = ExtractTestCode(result.UniversalTestId),
                    TestName = result.TestName,
                    Value = result.Value,
                    Units = result.Units,
                    ReferenceRange = result.ReferenceRange,
                    AbnormalFlag = result.AbnormalFlag,
                    Status = result.Status,
                    TestDateTime = result.TestDateTime,
                    InstrumentId = result.InstrumentId,
                    OriginalMessage = result.Content
                };

                results.Add(examResult);
            }

            _logger.LogDebug("Extracted {ResultCount} exam results from {MessageCount} messages", 
                results.Count, messages.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting exam results from ASTM messages");
        }

        return results;
    }

    public PatientData? ExtractPatientData(List<AstmMessage> messages)
    {
        try
        {
            var patientRecord = messages.OfType<AstmPatientRecord>().FirstOrDefault();
            if (patientRecord == null)
            {
                return null;
            }

            return new PatientData
            {
                Id = patientRecord.PatientId,
                FirstName = patientRecord.FirstName,
                LastName = patientRecord.LastName,
                BirthDate = patientRecord.BirthDate,
                Gender = patientRecord.Gender,
                Location = patientRecord.Location,
                ExternalId = patientRecord.PatientId
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting patient data from ASTM messages");
            return null;
        }
    }

    public bool ValidateMessage(AstmMessage message)
    {
        if (message == null) return false;
        if (string.IsNullOrEmpty(message.Type)) return false;
        if (string.IsNullOrEmpty(message.Content)) return false;
        
        return !_settings.ValidateChecksum || message.IsValid;
    }

    public string CalculateChecksum(string content)
    {
        int sum = 0;
        foreach (char c in content)
        {
            sum += (int)c;
        }
        return (sum % 256).ToString("X2");
    }

    public bool IsValidFrame(byte[] frame)
    {
        if (frame.Length < 4) return false; // Minimum: STX + sequence + data + ETX
        if (frame[0] != 0x02) return false; // Must start with STX
        
        // Look for ETX
        for (int i = 2; i < frame.Length; i++)
        {
            if (frame[i] == 0x03) return true; // Found ETX
        }
        
        return false;
    }

    private string GetFieldValue(string[] fields, int index)
    {
        return index < fields.Length ? fields[index] : string.Empty;
    }

    private string GetComponent(string field, int componentIndex)
    {
        var components = field.Split(_settings.ComponentSeparator);
        return componentIndex < components.Length ? components[componentIndex] : string.Empty;
    }

    private DateTime ParseDateTime(string dateTimeStr)
    {
        if (string.IsNullOrEmpty(dateTimeStr))
            return DateTime.Now;

        if (DateTime.TryParseExact(dateTimeStr, _settings.DateTimeFormat, null, System.Globalization.DateTimeStyles.None, out var result))
            return result;

        if (DateTime.TryParse(dateTimeStr, out result))
            return result;

        return DateTime.Now;
    }

    private DateTime? ParseDateTimeNullable(string dateTimeStr)
    {
        if (string.IsNullOrEmpty(dateTimeStr))
            return null;

        if (DateTime.TryParseExact(dateTimeStr, _settings.DateTimeFormat, null, System.Globalization.DateTimeStyles.None, out var result))
            return result;

        if (DateTime.TryParse(dateTimeStr, out result))
            return result;

        return null;
    }

    private string GetSpecimenIdFromMessages(List<AstmMessage> messages)
    {
        var orderRecord = messages.OfType<AstmOrderRecord>().FirstOrDefault();
        return orderRecord?.SpecimenId ?? "";
    }

    private string ExtractTestCode(string universalTestId)
    {
        if (string.IsNullOrEmpty(universalTestId))
            return "";

        // Extract test code from universal test ID
        // Format is usually: ^^^TestCode^TestName
        var components = universalTestId.Split(_settings.ComponentSeparator);
        return components.Length > 3 ? components[3] : universalTestId;
    }
}
