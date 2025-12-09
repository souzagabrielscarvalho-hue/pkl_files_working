using PklBridge.Core.Models;

namespace PklBridge.Core.Interfaces;

public interface ISerialBridge
{
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
    bool IsRunning { get; }
    event EventHandler<MessageReceivedEventArgs>? MessageReceived;
    event EventHandler<MessageSentEventArgs>? MessageSent;
    event EventHandler<BridgeErrorEventArgs>? ErrorOccurred;
}

public class MessageReceivedEventArgs : EventArgs
{
    public byte[] Data { get; }
    public string Source { get; }
    public DateTime Timestamp { get; }
    
    public MessageReceivedEventArgs(byte[] data, string source)
    {
        Data = data;
        Source = source;
        Timestamp = DateTime.Now;
    }
}

public class MessageSentEventArgs : EventArgs
{
    public byte[] Data { get; }
    public string Destination { get; }
    public DateTime Timestamp { get; }
    
    public MessageSentEventArgs(byte[] data, string destination)
    {
        Data = data;
        Destination = destination;
        Timestamp = DateTime.Now;
    }
}

public class BridgeErrorEventArgs : EventArgs
{
    public string Source { get; }
    public Exception Exception { get; }
    public string Message { get; }
    public DateTime Timestamp { get; }
    
    public BridgeErrorEventArgs(string source, Exception exception, string message = "")
    {
        Source = source;
        Exception = exception;
        Message = message;
        Timestamp = DateTime.Now;
    }
}
