using Microsoft.Extensions.Logging;
using PklBridge.Core.Interfaces;
using PklBridge.Core.Models;
using System.Text;
using System.Collections.Concurrent;

namespace PklBridge.Infrastructure.Serial;

/// <summary>
/// Gerencia sessões de comunicação ASTM E1394-97 bidirecionais
/// </summary>
public class AstmSessionManager
{
    private readonly ILogger<AstmSessionManager> _logger;
    private readonly IAstmTransport _transport;
    private readonly SemaphoreSlim _sessionLock = new(1, 1);
    
    // Caracteres de controle ASTM
    private const byte ENQ = 0x05;  // Enquiry - Solicita permissão para enviar
    private const byte ACK = 0x06;  // Acknowledge - Confirma recebimento
    private const byte NAK = 0x15;  // Not Acknowledge - Rejeita mensagem
    private const byte EOT = 0x04;  // End of Transmission - Fim da transmissão
    private const byte STX = 0x02;  // Start of Text - Início do frame
    private const byte ETX = 0x03;  // End of Text - Fim do frame
    private const byte CR = 0x0D;   // Carriage Return
    private const byte LF = 0x0A;   // Line Feed

    private const int MaxRetries = 3;
    private const int AckTimeoutMs = 5000;
    private const int EnqTimeoutMs = 10000;

    // Fila de respostas por cliente para sincronização
    private readonly ConcurrentDictionary<string, TaskCompletionSource<byte>> _pendingResponses = new();

    public AstmSessionManager(ILogger<AstmSessionManager> logger, IAstmTransport transport)
    {
        _logger = logger;
        _transport = transport;

        // Subscrever ao evento de dados recebidos para capturar ACK/NAK
        _transport.DataReceived += OnTransportDataReceived;
    }

    /// <summary>
    /// Envia uma mensagem ASTM completa para um cliente específico
    /// SEGUINDO EXATAMENTE O PADRÃO DO LOG.TXT COM DELAYS
    /// </summary>
    /// <param name="clientEndpoint">Endpoint do cliente</param>
    /// <param name="message">Mensagem ASTM a ser enviada</param>
    /// <param name="isResponseToQuery">Se true, não envia ENQ (HLAB já está aguardando resposta)</param>
    /// <param name="cancellationToken">Token de cancelamento</param>
    public async Task<bool> SendAstmMessageAsync(string clientEndpoint, string message, bool isResponseToQuery = false, CancellationToken cancellationToken = default)
    {
        await _sessionLock.WaitAsync(cancellationToken);
        
        try
        {
            _logger.LogInformation("[Bridge→HLAB] Sessão ASTM iniciada | Cliente: {ClientEndpoint} | Resposta: {IsResponse}", 
                clientEndpoint, isResponseToQuery);

            // 1. Enviar ENQ e aguardar ACK (APENAS se não for resposta a Query)
            if (!isResponseToQuery)
            {
                if (!await SendEnqAndWaitAckAsync(clientEndpoint, cancellationToken))
                {
                    _logger.LogError("[Bridge→HLAB] ENQ não confirmado");
                    return false;
                }
            }
            else
            {
            }

            // 2. Dividir mensagem em frames SEPARADOS (H, P, O, L) e enviar COM DELAYS
            var frames = SplitIntoFramesSeparated(message);

            for (int i = 0; i < frames.Count; i++)
            {
                var frame = frames[i];
                var frameNumber = ((i + 1) % 8).ToString(); // Frame numbers: 1-7, 0, 1-7, 0...
                
                if (!await SendFrameAndWaitAckAsync(clientEndpoint, frameNumber, frame, cancellationToken))
                {
                    _logger.LogError("[Bridge→HLAB] Falha frame {FrameNumber}", frameNumber);
                    return false;
                }
            }

            // 3. Enviar EOT para finalizar
            await SendEotAsync(clientEndpoint, cancellationToken);
            
            _logger.LogInformation("[Bridge→HLAB] Sessão concluída | Frames: {FrameCount}", frames.Count);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ERRO] Sessão ASTM");
            return false;
        }
        finally
        {
            _sessionLock.Release();
        }
    }

    /// <summary>
    /// Processa dados recebidos via transporte (TCP ou Serial) para capturar caracteres de controle
    /// </summary>
    private void OnTransportDataReceived(object? sender, AstmDataReceivedEventArgs e)
    {
        try
        {
            // Verificar se é um caractere de controle único
            if (e.Data.Length == 1)
            {
                var controlChar = e.Data[0];

                if (controlChar == ACK || controlChar == NAK || controlChar == EOT)
                {
                    _logger.LogDebug("📨 Caractere de controle recebido de {Endpoint}: {ControlChar:X2} ({Name})",
                        e.Endpoint,
                        controlChar,
                        controlChar == ACK ? "ACK" : controlChar == NAK ? "NAK" : "EOT");

                    // Notificar quem está aguardando resposta
                    if (_pendingResponses.TryRemove(e.Endpoint, out var tcs))
                    {
                        tcs.TrySetResult(controlChar);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar dados recebidos no AstmSessionManager");
        }
    }

    /// <summary>
    /// Aguarda um caractere de controle específico do cliente
    /// </summary>
    private async Task<byte?> WaitForControlCharAsync(string clientEndpoint, int timeoutMs, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<byte>();
        _pendingResponses[clientEndpoint] = tcs;
        
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeoutMs);
            
            var responseTask = tcs.Task;
            var timeoutTask = Task.Delay(timeoutMs, cts.Token);
            
            var completedTask = await Task.WhenAny(responseTask, timeoutTask);
            
            if (completedTask == responseTask)
            {
                return await responseTask;
            }
            else
            {
                _logger.LogWarning("⏱️ Timeout aguardando resposta de {ClientEndpoint} ({TimeoutMs}ms)", 
                    clientEndpoint, timeoutMs);
                return null;
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("❌ Operação cancelada aguardando resposta de {ClientEndpoint}", clientEndpoint);
            return null;
        }
        finally
        {
            _pendingResponses.TryRemove(clientEndpoint, out _);
        }
    }

    /// <summary>
    /// Envia ENQ e aguarda ACK REAL do HLAB
    /// </summary>
    private async Task<bool> SendEnqAndWaitAckAsync(string clientEndpoint, CancellationToken cancellationToken)
    {
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                _logger.LogInformation("📤 Enviando ENQ para {ClientEndpoint} (tentativa {Attempt}/{MaxRetries})", 
                    clientEndpoint, attempt, MaxRetries);
                
                await _transport.SendAsync(clientEndpoint, new byte[] { ENQ }, cancellationToken);
                
                // Aguardar ACK REAL do HLAB
                var response = await WaitForControlCharAsync(clientEndpoint, EnqTimeoutMs, cancellationToken);
                
                if (response == ACK)
                {
                    _logger.LogInformation("✅ ACK recebido de {ClientEndpoint} para ENQ", clientEndpoint);
                    return true;
                }
                else if (response == NAK)
                {
                    _logger.LogWarning("⚠️ NAK recebido de {ClientEndpoint} para ENQ - tentando novamente", clientEndpoint);
                }
                else
                {
                    _logger.LogWarning("⚠️ Timeout ou resposta inválida de {ClientEndpoint} para ENQ", clientEndpoint);
                }
                
                if (attempt < MaxRetries)
                {
                    await Task.Delay(1000 * attempt, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro na tentativa {Attempt} de enviar ENQ para {ClientEndpoint}", 
                    attempt, clientEndpoint);
                
                if (attempt < MaxRetries)
                {
                    await Task.Delay(1000 * attempt, cancellationToken);
                }
            }
        }
        
        _logger.LogError("❌ Falha ao enviar ENQ após {MaxRetries} tentativas", MaxRetries);
        return false;
    }

    /// <summary>
    /// Envia um frame ASTM e aguarda ACK REAL do HLAB
    /// </summary>
    private async Task<bool> SendFrameAndWaitAckAsync(string clientEndpoint, string frameNumber, string content, CancellationToken cancellationToken)
    {
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                var frame = BuildFrame(frameNumber, content);
                
                _logger.LogInformation("📤 Enviando frame {FrameNumber} para {ClientEndpoint} (tentativa {Attempt}/{MaxRetries}): {FrameLength} bytes", 
                    frameNumber, clientEndpoint, attempt, MaxRetries, frame.Length);
                
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    var frameHex = Convert.ToHexString(frame);
                    var frameText = Encoding.ASCII.GetString(frame)
                        .Replace('\r', '↵')
                        .Replace('\n', '↓')
                        .Replace("\x02", "<STX>")
                        .Replace("\x03", "<ETX>");
                    _logger.LogDebug("📄 Frame {FrameNumber} HEX: {FrameHex}", frameNumber, frameHex);
                    _logger.LogDebug("📄 Frame {FrameNumber} TXT: {FrameText}", frameNumber, frameText);
                }
                
                await _transport.SendAsync(clientEndpoint, frame, cancellationToken);
                
                // Aguardar ACK REAL do HLAB
                var response = await WaitForControlCharAsync(clientEndpoint, AckTimeoutMs, cancellationToken);
                
                if (response == ACK)
                {
                    _logger.LogInformation("✅ ACK recebido de {ClientEndpoint} para frame {FrameNumber}", 
                        clientEndpoint, frameNumber);
                    return true;
                }
                else if (response == NAK)
                {
                    _logger.LogWarning("⚠️ NAK recebido de {ClientEndpoint} para frame {FrameNumber} - reenviando", 
                        clientEndpoint, frameNumber);
                    // Continua no loop para reenviar
                }
                else
                {
                    _logger.LogWarning("⚠️ Timeout ou resposta inválida de {ClientEndpoint} para frame {FrameNumber}", 
                        clientEndpoint, frameNumber);
                }
                
                if (attempt < MaxRetries)
                {
                    await Task.Delay(1000 * attempt, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro na tentativa {Attempt} de enviar frame {FrameNumber} para {ClientEndpoint}", 
                    attempt, frameNumber, clientEndpoint);
                
                if (attempt < MaxRetries)
                {
                    await Task.Delay(1000 * attempt, cancellationToken);
                }
            }
        }
        
        _logger.LogError("❌ Falha ao enviar frame {FrameNumber} após {MaxRetries} tentativas", 
            frameNumber, MaxRetries);
        return false;
    }

    /// <summary>
    /// Envia EOT para finalizar a transmissão
    /// </summary>
    private async Task SendEotAsync(string clientEndpoint, CancellationToken cancellationToken)
    {
        _logger.LogInformation("📤 Enviando EOT para {ClientEndpoint} - finalizando transmissão", clientEndpoint);
        await _transport.SendAsync(clientEndpoint, new byte[] { EOT }, cancellationToken);
    }

    /// <summary>
    /// Constrói um frame ASTM completo com STX, número, conteúdo, CR, ETX, checksum, CR, LF
    /// FORMATO CORRETO: <STX><FN><TEXT><CR><ETX><C1><C2><CR><LF>
    /// </summary>
    private byte[] BuildFrame(string frameNumber, string content)
    {
        // FORMATO CORRETO DO ASTM E1381: <STX><FN><TEXT><CR><ETX><C1><C2><CR><LF>
        // O CR deve estar ANTES do ETX, não depois!
        var frameContent = $"{frameNumber}{content}";
        
        // Calcular checksum (inclui FN, TEXT, CR e ETX)
        var checksum = CalculateChecksum(frameContent);
        
        // Montar frame completo
        var frame = new List<byte>();
        frame.Add(STX);
        frame.AddRange(Encoding.ASCII.GetBytes(frameContent));
        frame.Add(CR);   // CR ANTES do ETX!
        frame.Add(ETX);
        frame.AddRange(Encoding.ASCII.GetBytes(checksum));
        frame.Add(CR);
        frame.Add(LF);
        
        return frame.ToArray();
    }

    /// <summary>
    /// Calcula checksum ASTM (soma módulo 256 em hexadecimal)
    /// O checksum é calculado sobre: FN + TEXT + CR + ETX
    /// </summary>
    private string CalculateChecksum(string content)
    {
        int sum = 0;
        foreach (char c in content)
        {
            sum += (int)c;
        }
        
        // Adicionar CR e ETX ao checksum
        sum += CR;   // CR faz parte do checksum!
        sum += ETX;
        
        byte checksum = (byte)(sum % 256);
        return checksum.ToString("X2");
    }

    /// <summary>
    /// Divide uma mensagem ASTM em frames de até 240 caracteres
    /// </summary>
    private List<string> SplitIntoFrames(string message)
    {
        const int MaxFrameSize = 240; // Tamanho máximo recomendado por frame
        var frames = new List<string>();
        
        // Dividir mensagem em linhas (registros ASTM)
        var lines = message.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        
        var currentFrame = new StringBuilder();
        
        foreach (var line in lines)
        {
            // Se adicionar esta linha ultrapassar o limite, criar novo frame
            if (currentFrame.Length + line.Length + 1 > MaxFrameSize && currentFrame.Length > 0)
            {
                frames.Add(currentFrame.ToString());
                currentFrame.Clear();
            }
            
            if (currentFrame.Length > 0)
            {
                currentFrame.Append('\r');
            }
            
            currentFrame.Append(line);
        }
        
        // Adicionar último frame se houver conteúdo
        if (currentFrame.Length > 0)
        {
            frames.Add(currentFrame.ToString());
        }
        
        return frames;
    }

    /// <summary>
    /// Divide mensagem ASTM em frames SEPARADOS (H, P, O) - SEM incluir L
    /// Seguindo padrão do log.txt onde cada registro é um frame separado
    /// </summary>
    private List<string> SplitIntoFramesSeparated(string message)
    {
        var frames = new List<string>();
        
        // Dividir mensagem em linhas (registros ASTM)
        var lines = message.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var line in lines)
        {
            // Cada linha vira um frame separado (incluindo L - Terminator)
            frames.Add(line);
        }
        
        return frames;
    }

    /// <summary>
    /// Retorna delay em milissegundos baseado no índice do frame
    /// Seguindo padrão do log.txt:
    /// - Frame 1 (H): 0ms (já tem delay de 2s antes de começar)
    /// - Frame 2 (P): 3000ms
    /// - Frame 3 (O): 3000ms
    /// </summary>
    private int GetDelayForFrame(int frameIndex, int totalFrames)
    {
        if (frameIndex == 0)
        {
            return 0; // Primeiro frame, sem delay adicional
        }
        
        // Todos os outros frames: 3 segundos de delay
        return 3000;
    }

    /// <summary>
    /// Envia ACK para confirmar recebimento de um frame
    /// </summary>
    public async Task SendAckAsync(string clientEndpoint, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("📤 Enviando ACK");
        await _transport.SendAsync(clientEndpoint, new byte[] { ACK }, cancellationToken);
    }

    /// <summary>
    /// Envia NAK para rejeitar um frame
    /// </summary>
    public async Task SendNakAsync(string clientEndpoint, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("📤 Enviando NAK");
        await _transport.SendAsync(clientEndpoint, new byte[] { NAK }, cancellationToken);
    }

    /// <summary>
    /// Verifica se os dados recebidos são um caractere de controle
    /// </summary>
    public static bool IsControlCharacter(byte[] data, out byte controlChar)
    {
        controlChar = 0;
        
        if (data == null || data.Length != 1)
            return false;
        
        controlChar = data[0];
        return controlChar == ENQ || controlChar == ACK || controlChar == NAK || controlChar == EOT;
    }

    /// <summary>
    /// Valida checksum de um frame recebido
    /// </summary>
    public static bool ValidateChecksum(byte[] frameData)
    {
        try
        {
            // Encontrar STX e ETX
            int stxIndex = Array.IndexOf(frameData, STX);
            int etxIndex = Array.IndexOf(frameData, ETX);
            
            if (stxIndex == -1 || etxIndex == -1 || etxIndex <= stxIndex)
                return false;
            
            // Extrair conteúdo entre STX e ETX (incluindo número do frame)
            var content = Encoding.ASCII.GetString(frameData, stxIndex + 1, etxIndex - stxIndex - 1);
            
            // Calcular checksum esperado
            int sum = 0;
            foreach (char c in content)
            {
                sum += (int)c;
            }
            sum += ETX;
            byte expectedChecksum = (byte)(sum % 256);
            
            // Extrair checksum recebido (2 bytes após ETX)
            if (etxIndex + 2 >= frameData.Length)
                return false;
            
            var checksumStr = Encoding.ASCII.GetString(frameData, etxIndex + 1, 2);
            if (!byte.TryParse(checksumStr, System.Globalization.NumberStyles.HexNumber, null, out byte receivedChecksum))
                return false;
            
            return expectedChecksum == receivedChecksum;
        }
        catch
        {
            return false;
        }
    }
}
