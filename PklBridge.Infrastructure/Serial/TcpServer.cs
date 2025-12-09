using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PklBridge.Core.Configuration;
using System.Net;
using System.Net.Sockets;

namespace PklBridge.Infrastructure.Serial;

public class TcpServer : IDisposable
{
    private readonly ILogger<TcpServer> _logger;
    private readonly BridgeSettings _settings;
    private TcpListener? _listener;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isRunning = false;
    private readonly Dictionary<string, TcpClient> _connectedClients = new();
    private readonly object _clientsLock = new object();

    public event EventHandler<TcpDataReceivedEventArgs>? DataReceived;

    public TcpServer(ILogger<TcpServer> logger, IOptions<BridgeSettings> settings)
    {
        _logger = logger;
        _settings = settings.Value;
    }

    public bool IsRunning => _isRunning;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_isRunning)
            {
                _logger.LogWarning("TCP Server já está rodando");
                return;
            }

            _logger.LogInformation("Iniciando TCP Server na porta {Port}...", _settings.TcpPort);

            _cancellationTokenSource = new CancellationTokenSource();
            _listener = new TcpListener(IPAddress.Any, _settings.TcpPort);
            _listener.Start();

            _isRunning = true;

            _logger.LogInformation("✅ TCP Server iniciado com sucesso na porta {Port}", _settings.TcpPort);

            // Aceitar conexões em background
            _ = Task.Run(() => AcceptClientsAsync(_cancellationTokenSource.Token), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao iniciar TCP Server na porta {Port}", _settings.TcpPort);
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_isRunning)
                return;

            _logger.LogInformation("Parando TCP Server...");

            _isRunning = false;
            _cancellationTokenSource?.Cancel();
            _listener?.Stop();

            _logger.LogInformation("TCP Server parado");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao parar TCP Server");
        }
    }

    private async Task AcceptClientsAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _isRunning)
        {
            try
            {
                if (_listener?.Pending() == true)
                {
                    var tcpClient = await _listener.AcceptTcpClientAsync();
                    _logger.LogInformation("🔗 Nova conexão TCP aceita de {RemoteEndPoint}", 
                        tcpClient.Client.RemoteEndPoint);

                    // Processar cliente em background
                    _ = Task.Run(() => HandleClientAsync(tcpClient, cancellationToken), cancellationToken);
                }

                await Task.Delay(100, cancellationToken);
            }
            catch (ObjectDisposedException)
            {
                // Normal quando parando o servidor
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao aceitar conexão TCP");
                await Task.Delay(1000, cancellationToken);
            }
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        var clientEndpoint = client.Client.RemoteEndPoint?.ToString() ?? "Unknown";
        
        try
        {
            // Armazenar cliente conectado para permitir envio de respostas
            lock (_clientsLock)
            {
                _connectedClients[clientEndpoint] = client;
            }

            using (var stream = client.GetStream())
            {
                _logger.LogInformation("📡 Processando dados do cliente {ClientEndpoint}", clientEndpoint);

                var buffer = new byte[_settings.BufferSize];

                while (client.Connected && !cancellationToken.IsCancellationRequested)
                {
                    var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    
                    if (bytesRead == 0)
                    {
                        _logger.LogInformation("Cliente {ClientEndpoint} desconectou", clientEndpoint);
                        break;
                    }

                    var data = new byte[bytesRead];
                    Array.Copy(buffer, data, bytesRead);

                    _logger.LogInformation("📨 Dados recebidos via TCP de {ClientEndpoint}: {BytesCount} bytes", 
                        clientEndpoint, bytesRead);

                    if (_settings.LogAllTraffic)
                    {
                        var hexData = Convert.ToHexString(data);
                        _logger.LogDebug("📨 TCP Data (HEX): {HexData}", hexData);
                        
                        var textData = System.Text.Encoding.ASCII.GetString(data)
                            .Replace('\r', '↵').Replace('\n', '↓').Replace('\0', '∅');
                        _logger.LogDebug("📨 TCP Data (TXT): {TextData}", textData);
                    }

                    // Notificar que dados foram recebidos
                    DataReceived?.Invoke(this, new TcpDataReceivedEventArgs(data, clientEndpoint));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar cliente TCP {ClientEndpoint}", clientEndpoint);
        }
        finally
        {
            // Remover cliente da lista ao desconectar
            lock (_clientsLock)
            {
                _connectedClients.Remove(clientEndpoint);
            }
            
            _logger.LogInformation("🔌 Conexão TCP finalizada para {ClientEndpoint}", clientEndpoint);
            
            client.Dispose();
        }
    }

    public async Task SendToClientAsync(string clientEndpoint, byte[] data, CancellationToken cancellationToken = default)
    {
        try
        {
            TcpClient? client = null;
            
            lock (_clientsLock)
            {
                _connectedClients.TryGetValue(clientEndpoint, out client);
            }

            if (client == null || !client.Connected)
            {
                _logger.LogWarning("Cliente {ClientEndpoint} não está conectado para envio de resposta", clientEndpoint);
                return;
            }

            var stream = client.GetStream();
            await stream.WriteAsync(data, 0, data.Length, cancellationToken);
            await stream.FlushAsync(cancellationToken);

            _logger.LogInformation("📤 Resposta enviada para {ClientEndpoint}: {BytesCount} bytes", 
                clientEndpoint, data.Length);

            if (_settings.LogAllTraffic)
            {
                var hexData = Convert.ToHexString(data);
                _logger.LogDebug("📤 Response HEX: {HexData}", hexData);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao enviar resposta para cliente {ClientEndpoint}", clientEndpoint);
            
            // Remover cliente com erro da lista
            lock (_clientsLock)
            {
                _connectedClients.Remove(clientEndpoint);
            }
        }
    }

    public async Task SendToAllClientsAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        var clientEndpoints = new List<string>();
        
        lock (_clientsLock)
        {
            clientEndpoints.AddRange(_connectedClients.Keys);
        }

        foreach (var endpoint in clientEndpoints)
        {
            await SendToClientAsync(endpoint, data, cancellationToken);
        }
    }

    public void Dispose()
    {
        try
        {
            StopAsync().Wait(5000);
            _cancellationTokenSource?.Dispose();
            _listener?.Stop();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao fazer dispose do TCP Server");
        }
    }
}

public class TcpDataReceivedEventArgs : EventArgs
{
    public byte[] Data { get; }
    public string ClientEndpoint { get; }

    public TcpDataReceivedEventArgs(byte[] data, string clientEndpoint)
    {
        Data = data;
        ClientEndpoint = clientEndpoint;
    }
}
