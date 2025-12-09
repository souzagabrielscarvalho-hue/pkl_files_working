using System.IO.Ports;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PklBridge.Core.Configuration;

namespace PklBridge.Infrastructure.Serial;

public class SerialPortClient : IDisposable
{
    private readonly ILogger<SerialPortClient> _logger;
    private readonly BridgeSettings _settings;
    private SerialPort? _serialPort;
    private bool _disposed = false;

    public bool IsConnected => _serialPort?.IsOpen == true;
    public string? PortName => _serialPort?.PortName;

    public event EventHandler<CustomSerialDataReceivedEventArgs>? DataReceived;
    public event EventHandler<CustomSerialErrorEventArgs>? ErrorOccurred;

    public SerialPortClient(ILogger<SerialPortClient> logger, IOptions<BridgeSettings> settings)
    {
        _logger = logger;
        _settings = settings.Value;
    }

    public async Task OpenAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_settings.RealComPort))
        {
            _logger.LogInformation("No real COM port configured, skipping serial port connection");
            return;
        }

        if (_serialPort?.IsOpen == true)
        {
            _logger.LogWarning("Serial port is already open");
            return;
        }

        try
        {
            _logger.LogInformation("Opening serial port: {PortName}", _settings.RealComPort);

            _serialPort = new SerialPort
            {
                PortName = _settings.RealComPort,
                BaudRate = _settings.Serial.BaudRate,
                DataBits = _settings.Serial.DataBits,
                Parity = Enum.Parse<Parity>(_settings.Serial.Parity, true),
                StopBits = Enum.Parse<StopBits>(_settings.Serial.StopBits, true),
                Handshake = Enum.Parse<Handshake>(_settings.Serial.Handshake, true),
                ReadTimeout = _settings.Serial.ReadTimeout,
                WriteTimeout = _settings.Serial.WriteTimeout,
                DtrEnable = _settings.Serial.DtrEnable,
                RtsEnable = _settings.Serial.RtsEnable
            };

            _serialPort.DataReceived += OnSerialDataReceived;
            _serialPort.ErrorReceived += OnSerialErrorReceived;

            _serialPort.Open();

            _logger.LogInformation("Serial port {PortName} opened successfully. Settings: {BaudRate}, {DataBits}, {Parity}, {StopBits}", 
                _serialPort.PortName, _serialPort.BaudRate, _serialPort.DataBits, _serialPort.Parity, _serialPort.StopBits);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open serial port {PortName}", _settings.RealComPort);
            _serialPort?.Dispose();
            _serialPort = null;
            throw;
        }
    }

    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        if (_serialPort == null)
        {
            _logger.LogWarning("Serial port is not initialized");
            return;
        }

        try
        {
            _logger.LogInformation("Closing serial port {PortName}", _serialPort.PortName);

            if (_serialPort.IsOpen)
            {
                _serialPort.Close();
            }

            _serialPort.DataReceived -= OnSerialDataReceived;
            _serialPort.ErrorReceived -= OnSerialErrorReceived;
            _serialPort.Dispose();
            _serialPort = null;

            _logger.LogInformation("Serial port closed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error closing serial port");
        }
    }

    public async Task<byte[]> ReadAsync(int timeout = 5000, CancellationToken cancellationToken = default)
    {
        if (_serialPort == null || !_serialPort.IsOpen)
        {
            return Array.Empty<byte>();
        }

        try
        {
            var buffer = new List<byte>();
            var startTime = DateTime.Now;

            while ((DateTime.Now - startTime).TotalMilliseconds < timeout && !cancellationToken.IsCancellationRequested)
            {
                if (_serialPort.BytesToRead > 0)
                {
                    var tempBuffer = new byte[_serialPort.BytesToRead];
                    int bytesRead = _serialPort.Read(tempBuffer, 0, tempBuffer.Length);
                    
                    for (int i = 0; i < bytesRead; i++)
                    {
                        buffer.Add(tempBuffer[i]);
                    }

                    // Continue reading if we might have more data coming
                    await Task.Delay(10, cancellationToken);
                }
                else if (buffer.Count > 0)
                {
                    // We have some data and no more is immediately available
                    break;
                }
                else
                {
                    // No data available, wait a bit
                    await Task.Delay(10, cancellationToken);
                }
            }

            var result = buffer.ToArray();
            
            if (result.Length > 0)
            {
                _logger.LogDebug("Read {BytesRead} bytes from serial port: {Data}", 
                    result.Length, Convert.ToHexString(result));
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading from serial port");
                ErrorOccurred?.Invoke(this, new CustomSerialErrorEventArgs(ex, "Read error"));
            return Array.Empty<byte>();
        }
    }

    public async Task WriteAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        if (_serialPort == null || !_serialPort.IsOpen)
        {
            _logger.LogWarning("Cannot write to serial port: port is not open");
            return;
        }

        try
        {
            _logger.LogDebug("Writing {BytesCount} bytes to serial port: {Data}", 
                data.Length, Convert.ToHexString(data));

            _serialPort.Write(data, 0, data.Length);

            // Wait for data to be transmitted
            await Task.Delay(10, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error writing to serial port");
            ErrorOccurred?.Invoke(this, new CustomSerialErrorEventArgs(ex, "Write error"));
            throw;
        }
    }

    public int BytesToRead => _serialPort?.BytesToRead ?? 0;

    private void OnSerialDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        if (_serialPort == null || !_serialPort.IsOpen)
            return;

        try
        {
            var data = new byte[_serialPort.BytesToRead];
            int bytesRead = _serialPort.Read(data, 0, data.Length);

            if (bytesRead > 0)
            {
                var actualData = new byte[bytesRead];
                Array.Copy(data, actualData, bytesRead);

                _logger.LogDebug("Data received from serial port: {BytesReceived} bytes", bytesRead);
                DataReceived?.Invoke(this, new CustomSerialDataReceivedEventArgs(actualData));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing received serial data");
            ErrorOccurred?.Invoke(this, new CustomSerialErrorEventArgs(ex, "Data reception error"));
        }
    }

    private void OnSerialErrorReceived(object sender, SerialErrorReceivedEventArgs e)
    {
        _logger.LogError("Serial port error received: {ErrorType}", e.EventType);
        ErrorOccurred?.Invoke(this, new CustomSerialErrorEventArgs(
            new Exception($"Serial port error: {e.EventType}"), 
            $"Serial error: {e.EventType}"));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        try
        {
            if (_serialPort?.IsOpen == true)
            {
                _serialPort.Close();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during serial port disposal");
        }

        _serialPort?.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}

public class CustomSerialDataReceivedEventArgs : EventArgs
{
    public byte[] Data { get; }
    public DateTime Timestamp { get; }

    public CustomSerialDataReceivedEventArgs(byte[] data)
    {
        Data = data;
        Timestamp = DateTime.Now;
    }
}

public class CustomSerialErrorEventArgs : EventArgs
{
    public Exception Exception { get; }
    public string Message { get; }
    public DateTime Timestamp { get; }

    public CustomSerialErrorEventArgs(Exception exception, string message)
    {
        Exception = exception;
        Message = message;
        Timestamp = DateTime.Now;
    }
}
