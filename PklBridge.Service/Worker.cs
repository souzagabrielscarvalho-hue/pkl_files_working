using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PklBridge.Core.Configuration;
using PklBridge.Core.Interfaces;
using PklBridge.Infrastructure.Serial;
using System.Diagnostics;

namespace PklBridge.Service;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly BridgeSettings _bridgeSettings;
    private readonly VidaApiSettings _vidaSettings;
    private readonly SerialBridge _serialBridge;
    private readonly IVidaApiClient _vidaClient;

    public Worker(
        ILogger<Worker> logger,
        IOptions<BridgeSettings> bridgeSettings,
        IOptions<VidaApiSettings> vidaSettings,
        SerialBridge serialBridge,
        IVidaApiClient vidaClient)
    {
        _logger = logger;
        _bridgeSettings = bridgeSettings.Value;
        _vidaSettings = vidaSettings.Value;
        _serialBridge = serialBridge;
        _vidaClient = vidaClient;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("PKL Bridge Worker is starting");

        // Log configuration summary
        LogConfigurationSummary();

        // Test VIDA API connection
        await TestVidaApiConnection(cancellationToken);

        await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("PKL Bridge Worker is stopping");
        
        try
        {
            if (_serialBridge.IsRunning)
            {
                await _serialBridge.StopAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping serial bridge");
        }

        await base.StopAsync(cancellationToken);
        
        _logger.LogInformation("PKL Bridge Worker stopped");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("PKL Bridge Worker is running");

        try
        {
            // Start the serial bridge - this will handle all communication
            await _serialBridge.StartAsync(stoppingToken);

            // Subscribe to bridge events for monitoring
            _serialBridge.MessageReceived += OnMessageReceived;
            _serialBridge.MessageSent += OnMessageSent;
            _serialBridge.ErrorOccurred += OnErrorOccurred;

            // Keep the service alive
            while (!stoppingToken.IsCancellationRequested)
            {
                // Periodic health check
                await PerformHealthCheck(stoppingToken);

                // Wait before next health check
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
            _logger.LogInformation("PKL Bridge Worker execution was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in PKL Bridge Worker");
            
            // Re-throw to cause the service to stop
            throw;
        }
        finally
        {
            // Unsubscribe from events
            _serialBridge.MessageReceived -= OnMessageReceived;
            _serialBridge.MessageSent -= OnMessageSent;
            _serialBridge.ErrorOccurred -= OnErrorOccurred;
        }
    }

    private void LogConfigurationSummary()
    {
        _logger.LogInformation("PKL Bridge Configuration Summary:");
        _logger.LogInformation("  Named Pipe: {PipeName}", _bridgeSettings.PipeName);
        _logger.LogInformation("  Real COM Port: {ComPort}", _bridgeSettings.RealComPort ?? "Not configured");
        _logger.LogInformation("  Hardware Bridge: {Enabled}", _bridgeSettings.EnableHardwareBridge ? "Enabled" : "Disabled");
        _logger.LogInformation("  Log All Traffic: {Enabled}", _bridgeSettings.LogAllTraffic ? "Enabled" : "Disabled");
        _logger.LogInformation("  Serial Settings: {BaudRate}, {DataBits}, {Parity}, {StopBits}", 
            _bridgeSettings.Serial.BaudRate, 
            _bridgeSettings.Serial.DataBits, 
            _bridgeSettings.Serial.Parity, 
            _bridgeSettings.Serial.StopBits);
        _logger.LogInformation("  VIDA API: {BaseUrl}", _vidaSettings.BaseUrl);
        _logger.LogInformation("  API Timeout: {TimeoutSeconds}s", _vidaSettings.TimeoutSeconds);
        _logger.LogInformation("  Retry Attempts: {MaxRetryAttempts}", _vidaSettings.MaxRetryAttempts);
        _logger.LogInformation("  Circuit Breaker: {Enabled}", _vidaSettings.EnableCircuitBreaker ? "Enabled" : "Disabled");
    }

    private async Task TestVidaApiConnection(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("VIDA API connection test (mock mode)...");
            
            // Mock mode - skip real API test during development
            if (_vidaSettings.BaseUrl.Contains("hospital.local") || _vidaSettings.BaseUrl.Contains("localhost"))
            {
                _logger.LogInformation("VIDA API connection test successful (mocked)");
                return;
            }
            
            var result = await _vidaClient.TestConnectionAsync(cancellationToken);
            
            if (result.Success)
            {
                _logger.LogInformation("VIDA API connection test successful");
            }
            else
            {
                _logger.LogWarning("VIDA API connection test failed: {Error}", result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("VIDA API connection test failed (will continue in mock mode): {Error}", ex.Message);
        }
    }

    private async Task PerformHealthCheck(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Performing periodic health check");

            var healthData = new
            {
                Timestamp = DateTime.Now,
                SerialBridgeRunning = _serialBridge.IsRunning,
                MemoryUsage = GC.GetTotalMemory(false) / (1024 * 1024), // MB
                UptimeMinutes = (DateTime.Now - Process.GetCurrentProcess().StartTime).TotalMinutes
            };

            _logger.LogInformation("Health Check: Bridge={BridgeStatus}, Memory={MemoryMB}MB, Uptime={UptimeMinutes:F1}min",
                healthData.SerialBridgeRunning ? "Running" : "Stopped",
                healthData.MemoryUsage,
                healthData.UptimeMinutes);

            // Restart bridge if it's not running
            if (!_serialBridge.IsRunning)
            {
                _logger.LogWarning("Serial bridge is not running, attempting to restart");
                
                try
                {
                    await _serialBridge.StartAsync(cancellationToken);
                    _logger.LogInformation("Serial bridge restarted successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to restart serial bridge");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during health check");
        }
    }

    private void OnMessageReceived(object? sender, MessageReceivedEventArgs e)
    {
        _logger.LogDebug("Message received from {Source}: {BytesCount} bytes", 
            e.Source, e.Data.Length);

        // Additional processing or monitoring can be added here
    }

    private void OnMessageSent(object? sender, MessageSentEventArgs e)
    {
        _logger.LogDebug("Message sent to {Destination}: {BytesCount} bytes", 
            e.Destination, e.Data.Length);

        // Additional processing or monitoring can be added here
    }

    private void OnErrorOccurred(object? sender, BridgeErrorEventArgs e)
    {
        _logger.LogError(e.Exception, "Bridge error from {Source}: {Message}", 
            e.Source, e.Message);

        // Could implement additional error handling, alerting, etc.
    }

    public override void Dispose()
    {
        try
        {
            _serialBridge?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disposing serial bridge");
        }

        base.Dispose();
    }
}
