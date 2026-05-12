namespace PklBridge.Core.Interfaces;

public interface IAstmTransport
{
    string Name { get; }
    bool IsRunning { get; }

    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);

    Task SendAsync(string endpoint, byte[] data, CancellationToken cancellationToken = default);

    event EventHandler<AstmDataReceivedEventArgs>? DataReceived;
}

public sealed class AstmDataReceivedEventArgs : EventArgs
{
    public byte[] Data { get; }
    public string Endpoint { get; }
    public DateTime Timestamp { get; }

    public AstmDataReceivedEventArgs(byte[] data, string endpoint)
    {
        Data = data;
        Endpoint = endpoint;
        Timestamp = DateTime.Now;
    }
}
