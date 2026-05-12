using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PklBridge.Core.Configuration;
using PklBridge.Core.Interfaces;

namespace PklBridge.Infrastructure.Serial;

public sealed class TcpAstmTransport : IAstmTransport, IDisposable
{
    private readonly ILogger<TcpAstmTransport> _logger;
    private readonly TcpServer _tcpServer;
    private readonly BridgeSettings _settings;
    private bool _subscribed;

    public TcpAstmTransport(
        ILogger<TcpAstmTransport> logger,
        TcpServer tcpServer,
        IOptions<BridgeSettings> settings)
    {
        _logger = logger;
        _tcpServer = tcpServer;
        _settings = settings.Value;
    }

    public string Name => "Tcp";

    public bool IsRunning => _tcpServer.IsRunning;

    public event EventHandler<AstmDataReceivedEventArgs>? DataReceived;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!_subscribed)
        {
            _tcpServer.DataReceived += OnTcpDataReceived;
            _subscribed = true;
        }

        _logger.LogInformation("[TcpAstmTransport] Iniciando TCP server na porta {Port}", _settings.TcpPort);
        await _tcpServer.StartAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_subscribed)
        {
            _tcpServer.DataReceived -= OnTcpDataReceived;
            _subscribed = false;
        }

        await _tcpServer.StopAsync(cancellationToken);
    }

    public Task SendAsync(string endpoint, byte[] data, CancellationToken cancellationToken = default)
    {
        return _tcpServer.SendToClientAsync(endpoint, data, cancellationToken);
    }

    private void OnTcpDataReceived(object? sender, TcpDataReceivedEventArgs e)
    {
        DataReceived?.Invoke(this, new AstmDataReceivedEventArgs(e.Data, e.ClientEndpoint));
    }

    public void Dispose()
    {
        if (_subscribed)
        {
            _tcpServer.DataReceived -= OnTcpDataReceived;
            _subscribed = false;
        }
    }
}
