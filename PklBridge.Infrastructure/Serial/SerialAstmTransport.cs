using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PklBridge.Core.Configuration;
using PklBridge.Core.Interfaces;

namespace PklBridge.Infrastructure.Serial;

public sealed class SerialAstmTransport : IAstmTransport, IDisposable
{
    private readonly ILogger<SerialAstmTransport> _logger;
    private readonly SerialPortClient _serialPortClient;
    private readonly BridgeSettings _settings;
    private bool _subscribed;

    public SerialAstmTransport(
        ILogger<SerialAstmTransport> logger,
        SerialPortClient serialPortClient,
        IOptions<BridgeSettings> settings)
    {
        _logger = logger;
        _serialPortClient = serialPortClient;
        _settings = settings.Value;
    }

    public string Name => "Serial";

    public bool IsRunning => _serialPortClient.IsConnected;

    public event EventHandler<AstmDataReceivedEventArgs>? DataReceived;

    private string EndpointName => _serialPortClient.PortName ?? _settings.RealComPort ?? "COM?";

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.RealComPort))
        {
            throw new InvalidOperationException(
                "TransportMode=Serial requer BridgeSettings.RealComPort configurado (ex.: \"COM2\").");
        }

        if (!_subscribed)
        {
            _serialPortClient.DataReceived += OnSerialDataReceived;
            _subscribed = true;
        }

        _logger.LogInformation(
            "[SerialAstmTransport] Abrindo porta {Port} a {BaudRate} baud, {DataBits}{Parity}{StopBits}",
            _settings.RealComPort,
            _settings.Serial.BaudRate,
            _settings.Serial.DataBits,
            _settings.Serial.Parity,
            _settings.Serial.StopBits);

        await _serialPortClient.OpenAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_subscribed)
        {
            _serialPortClient.DataReceived -= OnSerialDataReceived;
            _subscribed = false;
        }

        await _serialPortClient.CloseAsync(cancellationToken);
    }

    public Task SendAsync(string endpoint, byte[] data, CancellationToken cancellationToken = default)
    {
        return _serialPortClient.WriteAsync(data, cancellationToken);
    }

    private void OnSerialDataReceived(object? sender, CustomSerialDataReceivedEventArgs e)
    {
        DataReceived?.Invoke(this, new AstmDataReceivedEventArgs(e.Data, EndpointName));
    }

    public void Dispose()
    {
        if (_subscribed)
        {
            _serialPortClient.DataReceived -= OnSerialDataReceived;
            _subscribed = false;
        }
    }
}
