using PklBridge.Core.Models;

namespace PklBridge.Core.Interfaces;

public interface IAstmParser
{
    List<AstmMessage> Parse(byte[] data);
    List<AstmMessage> Parse(string data);
    List<ExamResult> ExtractResults(List<AstmMessage> messages);
    PatientData? ExtractPatientData(List<AstmMessage> messages);
    bool ValidateMessage(AstmMessage message);
    string CalculateChecksum(string content);
    bool IsValidFrame(byte[] frame);
}

public interface IMessageProcessor
{
    Task<ProcessingResult> ProcessMessagesAsync(List<AstmMessage> messages, CancellationToken cancellationToken = default);
    Task<ProcessingResult> ProcessBatchAsync(ExamBatch batch, CancellationToken cancellationToken = default);
    Task<bool> ShouldProcessMessageAsync(AstmMessage message, CancellationToken cancellationToken = default);
    event EventHandler<ProcessingCompletedEventArgs>? ProcessingCompleted;
    event EventHandler<ProcessingErrorEventArgs>? ProcessingError;
}

public class ProcessingCompletedEventArgs : EventArgs
{
    public ExamBatch Batch { get; }
    public ProcessingResult Result { get; }
    public TimeSpan Duration { get; }

    public ProcessingCompletedEventArgs(ExamBatch batch, ProcessingResult result, TimeSpan duration)
    {
        Batch = batch;
        Result = result;
        Duration = duration;
    }
}

public class ProcessingErrorEventArgs : EventArgs
{
    public ExamBatch? Batch { get; }
    public Exception Exception { get; }
    public string ErrorMessage { get; }
    public DateTime Timestamp { get; }

    public ProcessingErrorEventArgs(Exception exception, string errorMessage, ExamBatch? batch = null)
    {
        Exception = exception;
        ErrorMessage = errorMessage;
        Batch = batch;
        Timestamp = DateTime.Now;
    }
}
