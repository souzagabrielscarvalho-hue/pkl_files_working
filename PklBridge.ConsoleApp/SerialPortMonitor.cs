using System.IO.Ports;
using Microsoft.Extensions.Logging;

namespace PklBridge.ConsoleApp;

public class SerialPortMonitor : IDisposable
{
    private readonly ILogger<SerialPortMonitor> _logger;
    private SerialPort? _serialPort;
    private bool _isMonitoring = false;

    public SerialPortMonitor(ILogger<SerialPortMonitor> logger)
    {
        _logger = logger;
    }

    public bool StartMonitoring(string portName = "COM2", int baudRate = 19200)
    {
        try
        {
            _logger.LogInformation("🔍 Iniciando monitoramento da porta {PortName}...", portName);

            _serialPort = new SerialPort
            {
                PortName = portName,
                BaudRate = baudRate,
                DataBits = 8,
                Parity = Parity.None,
                StopBits = StopBits.One,
                Handshake = Handshake.None,
                ReadTimeout = 500,
                WriteTimeout = 500
            };

            _serialPort.DataReceived += OnDataReceived;
            _serialPort.ErrorReceived += OnErrorReceived;

            _serialPort.Open();
            _isMonitoring = true;

            _logger.LogInformation("✅ Monitoramento da porta {PortName} iniciado com sucesso!", portName);
            _logger.LogInformation("📡 Aguardando dados na {PortName} ({BaudRate}, 8, N, 1)...", portName, baudRate);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao iniciar monitoramento da porta {PortName}: {Error}", portName, ex.Message);
            return false;
        }
    }

    public void StopMonitoring()
    {
        try
        {
            if (_serialPort?.IsOpen == true)
            {
                _serialPort.DataReceived -= OnDataReceived;
                _serialPort.ErrorReceived -= OnErrorReceived;
                _serialPort.Close();
            }

            _isMonitoring = false;
            _logger.LogInformation("⏹️ Monitoramento da porta serial parado");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "⚠️ Erro ao parar monitoramento: {Error}", ex.Message);
        }
    }

    private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            if (_serialPort?.IsOpen != true) return;

            var bytesToRead = _serialPort.BytesToRead;
            if (bytesToRead > 0)
            {
                var buffer = new byte[bytesToRead];
                var bytesRead = _serialPort.Read(buffer, 0, bytesToRead);
                
                _logger.LogInformation("📨 DADOS RECEBIDOS na {PortName}: {BytesCount} bytes", 
                    _serialPort.PortName, bytesRead);
                
                // Mostrar dados em hex
                var hexData = Convert.ToHexString(buffer, 0, bytesRead);
                _logger.LogInformation("📊 Dados (HEX): {HexData}", hexData);
                
                // Mostrar dados como texto (se possível)
                try
                {
                    var textData = System.Text.Encoding.ASCII.GetString(buffer, 0, bytesRead)
                        .Replace('\r', '↵').Replace('\n', '↓').Replace('\0', '∅');
                    _logger.LogInformation("📝 Dados (TXT): {TextData}", textData);
                }
                catch
                {
                    _logger.LogInformation("📝 Dados (TXT): [dados binários não convertíveis]");
                }

                Console.WriteLine($"\n🚨 ATENÇÃO: Dados detectados na {_serialPort.PortName}!");
                Console.WriteLine($"   Bytes: {bytesRead} | Hex: {hexData}");
                Console.WriteLine("   O HLAB está enviando para COM2, mas o PKL Bridge espera Named Pipe!");
                Console.WriteLine();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao processar dados recebidos: {Error}", ex.Message);
        }
    }

    private void OnErrorReceived(object sender, SerialErrorReceivedEventArgs e)
    {
        _logger.LogWarning("⚠️ Erro na porta serial: {ErrorType}", e.EventType);
    }

    public bool IsMonitoring => _isMonitoring && _serialPort?.IsOpen == true;

    public void Dispose()
    {
        StopMonitoring();
        _serialPort?.Dispose();
    }
}
