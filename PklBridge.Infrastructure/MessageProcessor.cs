using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PklBridge.Core.Configuration;
using PklBridge.Core.Interfaces;
using PklBridge.Core.Models;
using System.Collections.Concurrent;

namespace PklBridge.Infrastructure;

public class MessageProcessor : IMessageProcessor
{
    private readonly ILogger<MessageProcessor> _logger;
    private readonly ProcessingSettings _settings;
    private readonly IAstmParser _astmParser;
    private readonly IVidaApiClient _vidaClient;
    
    private readonly ConcurrentDictionary<string, ExamBatch> _activeBatches = new();
    private readonly ConcurrentQueue<ExamBatch> _processingQueue = new();
    private readonly SemaphoreSlim _processingSemaphore;
    private readonly Timer _batchTimeoutTimer;
    
    public event EventHandler<ProcessingCompletedEventArgs>? ProcessingCompleted;
    public event EventHandler<ProcessingErrorEventArgs>? ProcessingError;

    public MessageProcessor(
        ILogger<MessageProcessor> logger,
        IOptions<ProcessingSettings> settings,
        IAstmParser astmParser,
        IVidaApiClient vidaClient)
    {
        _logger = logger;
        _settings = settings.Value;
        _astmParser = astmParser;
        _vidaClient = vidaClient;
        
        _processingSemaphore = new SemaphoreSlim(_settings.MaxConcurrentProcessing, _settings.MaxConcurrentProcessing);
        _batchTimeoutTimer = new Timer(CheckBatchTimeouts, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    }

    public async Task<ProcessingResult> ProcessMessagesAsync(List<AstmMessage> messages, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.Now;
        
        try
        {
            if (messages == null || messages.Count == 0)
            {
                return new ProcessingResult
                {
                    Success = true,
                    ProcessingTime = DateTime.Now - startTime
                };
            }

            _logger.LogDebug("Processing {MessageCount} ASTM messages", messages.Count);

            // Group messages into batches by patient/specimen
            var batches = GroupMessagesByBatch(messages);
            
            var processedBatches = new List<ProcessingResult>();
            
            foreach (var batch in batches)
            {
                var result = await ProcessBatchAsync(batch, cancellationToken);
                processedBatches.Add(result);
            }

            var overallSuccess = processedBatches.All(r => r.Success);
            var totalProcessingTime = DateTime.Now - startTime;

            var processingResult = new ProcessingResult
            {
                Success = overallSuccess,
                ProcessingTime = totalProcessingTime,
                Metadata = new Dictionary<string, object>
                {
                    ["ProcessedBatches"] = processedBatches.Count,
                    ["SuccessfulBatches"] = processedBatches.Count(r => r.Success),
                    ["FailedBatches"] = processedBatches.Count(r => !r.Success)
                }
            };

            _logger.LogInformation("Completed processing {MessageCount} messages in {Duration}ms. Success: {Success}", 
                messages.Count, totalProcessingTime.TotalMilliseconds, overallSuccess);

            return processingResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing ASTM messages");
            
            ProcessingError?.Invoke(this, new ProcessingErrorEventArgs(ex, "Error processing messages"));
            
            return new ProcessingResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                ProcessingTime = DateTime.Now - startTime
            };
        }
    }

    public async Task<ProcessingResult> ProcessBatchAsync(ExamBatch batch, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.Now;
        
        if (!await _processingSemaphore.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken))
        {
            var timeoutResult = new ProcessingResult
            {
                Success = false,
                ErrorMessage = "Processing timeout - too many concurrent batches",
                ProcessingTime = DateTime.Now - startTime
            };
            
            ProcessingError?.Invoke(this, new ProcessingErrorEventArgs(
                new TimeoutException("Processing semaphore timeout"), 
                "Too many concurrent batches", batch));
            
            return timeoutResult;
        }

        try
        {
            _logger.LogDebug("Processing batch {BatchId} with {ResultCount} results", 
                batch.BatchId, batch.Results.Count);

            batch.Status = BatchStatus.Processing;
            batch.ProcessedAt = DateTime.Now;

            // Validate batch
            if (!ValidateBatch(batch))
            {
                batch.Status = BatchStatus.Failed;
                batch.ErrorMessage = "Batch validation failed";
                
                var validationResult = new ProcessingResult
                {
                    Success = false,
                    ErrorMessage = batch.ErrorMessage,
                    ProcessingTime = DateTime.Now - startTime
                };

                ProcessingError?.Invoke(this, new ProcessingErrorEventArgs(
                    new InvalidOperationException(batch.ErrorMessage), 
                    batch.ErrorMessage, batch));
                
                return validationResult;
            }

            // Check for duplicates if enabled
            if (_settings.EnableDeduplication && await IsDuplicateBatch(batch, cancellationToken))
            {
                _logger.LogInformation("Batch {BatchId} is duplicate, skipping processing", batch.BatchId);
                
                batch.Status = BatchStatus.Completed;
                
                return new ProcessingResult
                {
                    Success = true,
                    ProcessingTime = DateTime.Now - startTime,
                    Metadata = new Dictionary<string, object> { ["Duplicate"] = true }
                };
            }

            // Send to VIDA API
            var apiResponse = await _vidaClient.SendResultsAsync(batch, cancellationToken);
            
            if (apiResponse.Success)
            {
                batch.Status = BatchStatus.Completed;
                
                var result = new ProcessingResult
                {
                    Success = true,
                    ProcessingTime = DateTime.Now - startTime,
                    Metadata = new Dictionary<string, object>
                    {
                        ["ApiStatusCode"] = apiResponse.StatusCode,
                        ["ApiDuration"] = apiResponse.Duration.TotalMilliseconds
                    }
                };

                _logger.LogInformation("Successfully processed batch {BatchId} in {Duration}ms", 
                    batch.BatchId, result.ProcessingTime.TotalMilliseconds);

                ProcessingCompleted?.Invoke(this, new ProcessingCompletedEventArgs(batch, result, result.ProcessingTime));
                
                return result;
            }
            else
            {
                batch.Status = BatchStatus.Failed;
                batch.ErrorMessage = apiResponse.ErrorMessage;
                batch.RetryCount++;

                var result = new ProcessingResult
                {
                    Success = false,
                    ErrorMessage = apiResponse.ErrorMessage,
                    ProcessingTime = DateTime.Now - startTime,
                    Metadata = new Dictionary<string, object>
                    {
                        ["ApiStatusCode"] = apiResponse.StatusCode,
                        ["RetryCount"] = batch.RetryCount
                    }
                };

                _logger.LogError("Failed to process batch {BatchId}. Error: {Error}. Retry count: {RetryCount}", 
                    batch.BatchId, apiResponse.ErrorMessage, batch.RetryCount);

                ProcessingError?.Invoke(this, new ProcessingErrorEventArgs(
                    new Exception(apiResponse.ErrorMessage ?? "Unknown API error"), 
                    apiResponse.ErrorMessage ?? "Unknown API error", batch));

                // Queue for retry if under retry limit
                if (batch.RetryCount < 3) // Could make this configurable
                {
                    _processingQueue.Enqueue(batch);
                    _logger.LogInformation("Batch {BatchId} queued for retry ({RetryCount}/3)", 
                        batch.BatchId, batch.RetryCount);
                }

                return result;
            }
        }
        catch (Exception ex)
        {
            batch.Status = BatchStatus.Failed;
            batch.ErrorMessage = ex.Message;
            
            _logger.LogError(ex, "Unexpected error processing batch {BatchId}", batch.BatchId);
            
            ProcessingError?.Invoke(this, new ProcessingErrorEventArgs(ex, "Unexpected processing error", batch));
            
            return new ProcessingResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                ProcessingTime = DateTime.Now - startTime
            };
        }
        finally
        {
            _processingSemaphore.Release();
        }
    }

    public async Task<bool> ShouldProcessMessageAsync(AstmMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            // Basic validation
            if (!_astmParser.ValidateMessage(message))
            {
                _logger.LogDebug("Message failed validation: {MessageType}", message.Type);
                return false;
            }

            // Only process result messages for now
            if (message.Type != "R")
            {
                _logger.LogDebug("Ignoring non-result message: {MessageType}", message.Type);
                return false;
            }

            // Additional business logic could go here
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if message should be processed");
            return false;
        }
    }

    private List<ExamBatch> GroupMessagesByBatch(List<AstmMessage> messages)
    {
        var batches = new List<ExamBatch>();
        var messageGroups = new Dictionary<string, List<AstmMessage>>();

        // Group messages by patient/specimen
        foreach (var message in messages)
        {
            var batchKey = GetBatchKey(message);
            
            if (!messageGroups.ContainsKey(batchKey))
            {
                messageGroups[batchKey] = new List<AstmMessage>();
            }
            
            messageGroups[batchKey].Add(message);
        }

        // Create batches from groups
        foreach (var group in messageGroups)
        {
            var batch = CreateBatchFromMessages(group.Key, group.Value);
            if (batch.Results.Count > 0)
            {
                batches.Add(batch);
            }
        }

        return batches;
    }

    private string GetBatchKey(AstmMessage message)
    {
        // Create a unique key for grouping related messages
        // This could be patient ID + specimen ID + timestamp window
        
        var patientId = "";
        var specimenId = "";
        
        if (message is AstmPatientRecord patient)
        {
            patientId = patient.PatientId;
        }
        else if (message is AstmOrderRecord order)
        {
            specimenId = order.SpecimenId;
        }
        else if (message is AstmResultRecord result)
        {
            // Extract from result if possible
            specimenId = result.InstrumentId; // Or another identifier
        }

        // Use timestamp window for grouping (e.g., group messages within same minute)
        var timeWindow = message.Timestamp.ToString("yyyy-MM-dd-HH-mm");
        
        return $"{patientId}_{specimenId}_{timeWindow}";
    }

    private ExamBatch CreateBatchFromMessages(string batchKey, List<AstmMessage> messages)
    {
        var batch = new ExamBatch
        {
            BatchId = batchKey,
            CreatedAt = DateTime.Now,
            Status = BatchStatus.Pending
        };

        // Extract patient data
        batch.Patient = _astmParser.ExtractPatientData(messages);

        // Extract results
        batch.Results = _astmParser.ExtractResults(messages);

        return batch;
    }

    private bool ValidateBatch(ExamBatch batch)
    {
        if (batch == null) return false;
        if (string.IsNullOrEmpty(batch.BatchId)) return false;
        if (batch.Results.Count == 0) return false;
        if (batch.Results.Count > _settings.MaxBatchSize) return false;

        // Validate individual results
        foreach (var result in batch.Results)
        {
            if (string.IsNullOrEmpty(result.TestCode) || string.IsNullOrEmpty(result.Value))
            {
                return false;
            }
        }

        return true;
    }

    private async Task<bool> IsDuplicateBatch(ExamBatch batch, CancellationToken cancellationToken)
    {
        if (!_settings.EnableDeduplication)
            return false;

        // Simple in-memory deduplication based on batch ID and time window
        var cutoffTime = DateTime.Now.AddMinutes(-_settings.DeduplicationWindowMinutes);
        
        return _activeBatches.Values.Any(b => 
            b.BatchId == batch.BatchId && 
            b.CreatedAt > cutoffTime && 
            b.Status == BatchStatus.Completed);
    }

    private void CheckBatchTimeouts(object? state)
    {
        try
        {
            var timeoutCutoff = DateTime.Now.AddSeconds(-_settings.BatchTimeoutSeconds);
            var timedOutBatches = _activeBatches.Values
                .Where(b => b.Status == BatchStatus.Processing && b.CreatedAt < timeoutCutoff)
                .ToList();

            foreach (var batch in timedOutBatches)
            {
                _logger.LogWarning("Batch {BatchId} timed out after {TimeoutSeconds} seconds", 
                    batch.BatchId, _settings.BatchTimeoutSeconds);
                
                batch.Status = BatchStatus.Failed;
                batch.ErrorMessage = "Batch processing timeout";
                
                ProcessingError?.Invoke(this, new ProcessingErrorEventArgs(
                    new TimeoutException("Batch processing timeout"), 
                    "Batch processing timeout", batch));
            }

            // Clean up old completed batches
            var cleanupCutoff = DateTime.Now.AddHours(-1);
            var batchesToRemove = _activeBatches
                .Where(kvp => kvp.Value.CreatedAt < cleanupCutoff && 
                             (kvp.Value.Status == BatchStatus.Completed || kvp.Value.Status == BatchStatus.Failed))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var batchId in batchesToRemove)
            {
                _activeBatches.TryRemove(batchId, out _);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking batch timeouts");
        }
    }

    public void Dispose()
    {
        _batchTimeoutTimer?.Dispose();
        _processingSemaphore?.Dispose();
    }
}
