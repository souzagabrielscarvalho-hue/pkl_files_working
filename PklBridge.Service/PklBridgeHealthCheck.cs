using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PklBridge.Core.Configuration;
using PklBridge.Core.Interfaces;
using PklBridge.Infrastructure.Serial;

namespace PklBridge.Service;

public class PklBridgeHealthCheck : IHealthCheck
{
    private readonly ILogger<PklBridgeHealthCheck> _logger;
    private readonly HealthCheckSettings _settings;
    private readonly SerialBridge _serialBridge;
    private readonly IVidaApiClient _vidaClient;

    public PklBridgeHealthCheck(
        ILogger<PklBridgeHealthCheck> logger,
        IOptions<HealthCheckSettings> settings,
        SerialBridge serialBridge,
        IVidaApiClient vidaClient)
    {
        _logger = logger;
        _settings = settings.Value;
        _serialBridge = serialBridge;
        _vidaClient = vidaClient;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var healthData = new Dictionary<string, object>();
            var isHealthy = true;
            var issues = new List<string>();

            // Check Serial Bridge Status
            var bridgeHealthy = CheckSerialBridge(healthData, issues);
            if (!bridgeHealthy) isHealthy = false;

            // Check VIDA API Connection
            var apiHealthy = await CheckVidaApiAsync(healthData, issues, cancellationToken);
            if (!apiHealthy) isHealthy = false;

            // Check System Resources
            CheckSystemResources(healthData);

            var status = isHealthy ? HealthStatus.Healthy : HealthStatus.Unhealthy;
            var description = isHealthy ? "All systems operational" : string.Join("; ", issues);

            _logger.LogDebug("Health check completed. Status: {Status}, Issues: {IssueCount}", 
                status, issues.Count);

            return new HealthCheckResult(status, description, null, healthData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during health check");
            
            return new HealthCheckResult(
                HealthStatus.Unhealthy, 
                $"Health check failed: {ex.Message}", 
                ex);
        }
    }

    private bool CheckSerialBridge(Dictionary<string, object> healthData, List<string> issues)
    {
        try
        {
            var bridgeRunning = _serialBridge.IsRunning;
            healthData["SerialBridgeRunning"] = bridgeRunning;

            if (!bridgeRunning)
            {
                issues.Add("Serial bridge is not running");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking serial bridge health");
            healthData["SerialBridgeError"] = ex.Message;
            issues.Add($"Serial bridge check failed: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> CheckVidaApiAsync(
        Dictionary<string, object> healthData, 
        List<string> issues, 
        CancellationToken cancellationToken)
    {
        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(_settings.ApiTimeoutMs));
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

            var result = await _vidaClient.TestConnectionAsync(combinedCts.Token);
            
            healthData["VidaApiConnected"] = result.Success;
            healthData["VidaApiStatusCode"] = result.StatusCode;
            healthData["VidaApiResponseTime"] = result.Duration.TotalMilliseconds;

            if (!result.Success)
            {
                issues.Add($"VIDA API connection failed: {result.ErrorMessage}");
                return false;
            }

            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Health check was cancelled
            healthData["VidaApiConnected"] = false;
            issues.Add("VIDA API health check was cancelled");
            return false;
        }
        catch (OperationCanceledException)
        {
            // API call timed out
            healthData["VidaApiConnected"] = false;
            healthData["VidaApiTimeout"] = true;
            issues.Add($"VIDA API connection timed out after {_settings.ApiTimeoutMs}ms");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking VIDA API health");
            healthData["VidaApiConnected"] = false;
            healthData["VidaApiError"] = ex.Message;
            issues.Add($"VIDA API check failed: {ex.Message}");
            return false;
        }
    }

    private void CheckSystemResources(Dictionary<string, object> healthData)
    {
        try
        {
            // Memory usage
            var memoryUsageMB = GC.GetTotalMemory(false) / (1024 * 1024);
            healthData["MemoryUsageMB"] = memoryUsageMB;

            // Process uptime
            var process = System.Diagnostics.Process.GetCurrentProcess();
            var uptime = DateTime.Now - process.StartTime;
            healthData["UptimeMinutes"] = uptime.TotalMinutes;

            // Thread count
            healthData["ThreadCount"] = process.Threads.Count;

            // Working set
            healthData["WorkingSetMB"] = process.WorkingSet64 / (1024 * 1024);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error collecting system resource information");
            healthData["SystemResourcesError"] = ex.Message;
        }
    }
}
