using System.IO.Pipes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PklBridge.Core.Configuration;
using PklBridge.Core.Interfaces;

namespace PklBridge.Infrastructure.Serial;

public class PklNamedPipeServer : INamedPipeServer, IDisposable
{
    private readonly ILogger<PklNamedPipeServer> _logger;
    private readonly BridgeSettings _settings;
    private NamedPipeServerStream? _pipeServer;
    private CancellationTokenSource? _cancellationTokenSource;
    private Task? _serverTask;
    private bool _disposed = false;

    public bool IsConnected => _pipeServer?.IsConnected == true;

    public event EventHandler<ClientConnectedEventArgs>? ClientConnected;
    public event EventHandler<ClientDisconnectedEventArgs>? ClientDisconnected;
    public event EventHandler<DataReceivedEventArgs>? DataReceived;

    public PklNamedPipeServer(ILogger<PklNamedPipeServer> logger, IOptions<BridgeSettings> settings)
    {
        _logger = logger;
        _settings = settings.Value;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_serverTask != null)
        {
            _logger.LogWarning("Named pipe server is already running");
            return;
        }

        _logger.LogInformation("Starting Named Pipe Server with pipe name: {PipeName}", _settings.PipeName);

        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _serverTask = RunServerLoopAsync(_cancellationTokenSource.Token);

        _logger.LogInformation("Named Pipe Server started successfully");
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_serverTask == null)
        {
            _logger.LogWarning("Named pipe server is not running");
            return;
        }

        _logger.LogInformation("Stopping Named Pipe Server");

        _cancellationTokenSource?.Cancel();

        try
        {
            await _serverTask.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Named pipe server did not stop gracefully within timeout");
        }

        _pipeServer?.Dispose();
        _pipeServer = null;
        _serverTask = null;
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;

        _logger.LogInformation("Named Pipe Server stopped");
    }

    public async Task<byte[]> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (_pipeServer == null || !_pipeServer.IsConnected)
        {
            throw new InvalidOperationException("Pipe is not connected");
        }

        var buffer = new byte[_settings.BufferSize];
        int bytesRead = await _pipeServer.ReadAsync(buffer, cancellationToken);
        
        if (bytesRead == 0)
        {
            return Array.Empty<byte>();
        }

        var result = new byte[bytesRead];
        Array.Copy(buffer, result, bytesRead);

        _logger.LogDebug("Read {BytesRead} bytes from pipe: {Data}", 
            bytesRead, Convert.ToHexString(result));

        DataReceived?.Invoke(this, new DataReceivedEventArgs(result));
        
        return result;
    }

    public async Task WriteAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        if (_pipeServer == null || !_pipeServer.IsConnected)
        {
            throw new InvalidOperationException("Pipe is not connected");
        }

        await _pipeServer.WriteAsync(data, cancellationToken);
        await _pipeServer.FlushAsync(cancellationToken);

        _logger.LogDebug("Wrote {BytesWritten} bytes to pipe: {Data}", 
            data.Length, Convert.ToHexString(data));
    }

    private async Task RunServerLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogDebug("Creating new named pipe server instance");
                
                _pipeServer = new NamedPipeServerStream(
                    _settings.PipeName,
                    PipeDirection.InOut,
                    1, // Maximum number of server instances
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous,
                    _settings.BufferSize,
                    _settings.BufferSize
                );

                _logger.LogInformation("Waiting for client connection on pipe: {PipeName}", _settings.PipeName);

                await _pipeServer.WaitForConnectionAsync(cancellationToken);

                _logger.LogInformation("Client connected to named pipe");
                ClientConnected?.Invoke(this, new ClientConnectedEventArgs("HLAB Client"));

                // Keep the connection alive until client disconnects or cancellation is requested
                await HandleClientConnectionAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Named pipe server operation cancelled");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in named pipe server loop");
                
                // Wait before retrying
                if (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
            }
            finally
            {
                if (_pipeServer != null)
                {
                    if (_pipeServer.IsConnected)
                    {
                        _logger.LogInformation("Client disconnected from named pipe");
                        ClientDisconnected?.Invoke(this, new ClientDisconnectedEventArgs("Connection ended"));
                    }

                    _pipeServer.Dispose();
                    _pipeServer = null;
                }
            }
        }
    }

    private async Task HandleClientConnectionAsync(CancellationToken cancellationToken)
    {
        var buffer = new byte[_settings.BufferSize];

        try
        {
            while (_pipeServer != null && _pipeServer.IsConnected && !cancellationToken.IsCancellationRequested)
            {
                // This is a simple keep-alive loop
                // The actual data reading will be done by the bridge calling ReadAsync
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling client connection");
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _cancellationTokenSource?.Cancel();
        _pipeServer?.Dispose();
        _cancellationTokenSource?.Dispose();

        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
