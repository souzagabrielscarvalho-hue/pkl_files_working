using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PklBridge.Core.Configuration;
using PklBridge.Core.Interfaces;

namespace PklBridge.Infrastructure.Serial;

public class SerialBridge : ISerialBridge, IDisposable
{
    private readonly ILogger<SerialBridge> _logger;
    private readonly BridgeSettings _settings;
    private readonly IMessageProcessor _messageProcessor;
    private readonly PklNamedPipeServer _namedPipeServer;
    private readonly SerialPortClient _serialPortClient;
    private readonly TcpServer _tcpServer;
    private readonly IAstmParser _astmParser;
    private readonly IVidaApiClient _vidaClient;

    private bool _isRunning = false;
    private readonly object _lockObject = new object();

    public bool IsRunning 
    { 
        get 
        { 
            lock (_lockObject)
            {
                return _isRunning;
            }
        } 
        private set
        {
            lock (_lockObject)
            {
                _isRunning = value;
            }
        }
    }

    public event EventHandler<MessageReceivedEventArgs>? MessageReceived;
    public event EventHandler<MessageSentEventArgs>? MessageSent;
    public event EventHandler<BridgeErrorEventArgs>? ErrorOccurred;

    public SerialBridge(
        ILogger<SerialBridge> logger,
        IOptions<BridgeSettings> settings,
        PklNamedPipeServer namedPipeServer,
        SerialPortClient serialPortClient,
        IAstmParser astmParser,
        IMessageProcessor messageProcessor,
        IVidaApiClient vidaClient)
    {
        _logger = logger;
        _settings = settings.Value;
        _namedPipeServer = namedPipeServer;
        _serialPortClient = serialPortClient;
        _astmParser = astmParser;
        _messageProcessor = messageProcessor;
        _vidaClient = vidaClient;

        // Subscribe to events
        _namedPipeServer.ClientConnected += OnPipeClientConnected;
        _namedPipeServer.ClientDisconnected += OnPipeClientDisconnected;
        _namedPipeServer.DataReceived += OnPipeDataReceived;

        _serialPortClient.DataReceived += OnSerialDataReceived;
        _serialPortClient.ErrorOccurred += OnSerialError;

        _messageProcessor.ProcessingCompleted += OnProcessingCompleted;
        _messageProcessor.ProcessingError += OnProcessingError;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (IsRunning)
        {
            _logger.LogWarning("Serial bridge is already running");
            return;
        }

        _logger.LogInformation("Starting PKL Serial Bridge");

        try
        {
            // Start Named Pipe Server (for HLAB connection)
            await _namedPipeServer.StartAsync(cancellationToken);

            // Start Serial Port Client (if enabled)
            if (_settings.EnableHardwareBridge)
            {
                await _serialPortClient.OpenAsync(cancellationToken);
            }

            IsRunning = true;
            _logger.LogInformation("PKL Serial Bridge started successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start PKL Serial Bridge");
            ErrorOccurred?.Invoke(this, new BridgeErrorEventArgs("SerialBridge", ex, "Failed to start bridge"));
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (!IsRunning)
        {
            _logger.LogWarning("Serial bridge is not running");
            return;
        }

        _logger.LogInformation("Stopping PKL Serial Bridge");

        try
        {
            // Stop Named Pipe Server
            await _namedPipeServer.StopAsync(cancellationToken);

            // Stop Serial Port Client
            await _serialPortClient.CloseAsync(cancellationToken);

            IsRunning = false;
            _logger.LogInformation("PKL Serial Bridge stopped successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping PKL Serial Bridge");
        }
    }


    private async Task RunBridgeLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Main bridge logic runs in event handlers
            // This loop just keeps the service alive and handles periodic tasks
            
            // Check for any pending data from serial port
            if (_settings.EnableHardwareBridge && _serialPortClient.IsConnected)
            {
                var serialData = await _serialPortClient.ReadAsync(100, cancellationToken);
                if (serialData.Length > 0)
                {
                    await HandleSerialToPipeDataAsync(serialData, cancellationToken);
                }
            }

            // Small delay to prevent excessive CPU usage
            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in bridge loop");
            ErrorOccurred?.Invoke(this, new BridgeErrorEventArgs("BridgeLoop", ex, "Bridge loop error"));
            
            // Wait before retrying
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }
    }

    private async void OnPipeClientConnected(object? sender, ClientConnectedEventArgs e)
    {
        _logger.LogInformation("HLAB client connected to named pipe");
    }

    private async void OnPipeClientDisconnected(object? sender, ClientDisconnectedEventArgs e)
    {
        _logger.LogInformation("HLAB client disconnected from named pipe: {Reason}", e.Reason);
    }

    private async void OnPipeDataReceived(object? sender, DataReceivedEventArgs e)
    {
        try
        {
            _logger.LogDebug("Received {BytesCount} bytes from HLAB via pipe", e.Data.Length);
            
            if (_settings.LogAllTraffic)
            {
                _logger.LogInformation("HLAB → Bridge: {Data}", Convert.ToHexString(e.Data));
            }

            MessageReceived?.Invoke(this, new MessageReceivedEventArgs(e.Data, "HLAB"));

            await HandlePipeToSerialDataAsync(e.Data, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling pipe data");
            ErrorOccurred?.Invoke(this, new BridgeErrorEventArgs("PipeDataHandler", ex, "Error processing pipe data"));
        }
    }

    private async void OnSerialDataReceived(object? sender, CustomSerialDataReceivedEventArgs e)
    {
        try
        {
            _logger.LogDebug("Received {BytesCount} bytes from serial port", e.Data.Length);

            if (_settings.LogAllTraffic)
            {
                _logger.LogInformation("Serial → Bridge: {Data}", Convert.ToHexString(e.Data));
            }

            MessageReceived?.Invoke(this, new MessageReceivedEventArgs(e.Data, "Serial"));

            await HandleSerialToPipeDataAsync(e.Data, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling serial data");
            ErrorOccurred?.Invoke(this, new BridgeErrorEventArgs("SerialDataHandler", ex, "Error processing serial data"));
        }
    }

    private async Task HandlePipeToSerialDataAsync(byte[] data, CancellationToken cancellationToken)
    {
        try
        {
            // 1. Parse ASTM messages for potential processing
            await ProcessAstmMessagesAsync(data, cancellationToken);

            // 2. Forward data to serial port if hardware bridge is enabled
            if (_settings.EnableHardwareBridge && _serialPortClient.IsConnected)
            {
                await _serialPortClient.WriteAsync(data, cancellationToken);
                
                if (_settings.LogAllTraffic)
                {
                    _logger.LogInformation("Bridge → Serial: {Data}", Convert.ToHexString(data));
                }

                MessageSent?.Invoke(this, new MessageSentEventArgs(data, "Serial"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error forwarding pipe data to serial");
            throw;
        }
    }

    private async Task HandleSerialToPipeDataAsync(byte[] data, CancellationToken cancellationToken)
    {
        try
        {
            // 1. Parse ASTM messages for processing
            await ProcessAstmMessagesAsync(data, cancellationToken);

            // 2. Forward data to named pipe if HLAB is connected
            if (_namedPipeServer.IsConnected)
            {
                await _namedPipeServer.WriteAsync(data, cancellationToken);
                
                if (_settings.LogAllTraffic)
                {
                    _logger.LogInformation("Bridge → HLAB: {Data}", Convert.ToHexString(data));
                }

                MessageSent?.Invoke(this, new MessageSentEventArgs(data, "HLAB"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error forwarding serial data to pipe");
            throw;
        }
    }

    private async Task ProcessAstmMessagesAsync(byte[] data, CancellationToken cancellationToken)
    {
        try
        {
            // Parse ASTM messages
            var messages = _astmParser.Parse(data);
            
            if (messages.Count == 0)
            {
                return;
            }

            _logger.LogDebug("Parsed {MessageCount} ASTM messages from data", messages.Count);

            // Process messages through the processor
            await _messageProcessor.ProcessMessagesAsync(messages, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing ASTM messages");
            // Don't rethrow - this shouldn't break the bridge operation
        }
    }

    private void OnSerialError(object? sender, CustomSerialErrorEventArgs e)
    {
        _logger.LogError(e.Exception, "Serial port error: {Message}", e.Message);
        ErrorOccurred?.Invoke(this, new BridgeErrorEventArgs("SerialPort", e.Exception, e.Message));
    }

    private void OnProcessingCompleted(object? sender, ProcessingCompletedEventArgs e)
    {
        _logger.LogInformation("Message processing completed for batch {BatchId} in {Duration}ms", 
            e.Batch.BatchId, e.Duration.TotalMilliseconds);
    }

    private void OnProcessingError(object? sender, ProcessingErrorEventArgs e)
    {
        _logger.LogError(e.Exception, "Message processing error: {Message}", e.ErrorMessage);
        ErrorOccurred?.Invoke(this, new BridgeErrorEventArgs("MessageProcessor", e.Exception, e.ErrorMessage));
    }

    public void Dispose()
    {
        try
        {
            // Unsubscribe from events
            _namedPipeServer.ClientConnected -= OnPipeClientConnected;
            _namedPipeServer.ClientDisconnected -= OnPipeClientDisconnected;
            _namedPipeServer.DataReceived -= OnPipeDataReceived;

            _serialPortClient.DataReceived -= OnSerialDataReceived;
            _serialPortClient.ErrorOccurred -= OnSerialError;

            _messageProcessor.ProcessingCompleted -= OnProcessingCompleted;
            _messageProcessor.ProcessingError -= OnProcessingError;

            // Dispose components
            _namedPipeServer.Dispose();
            _serialPortClient.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during SerialBridge disposal");
        }
    }
}
