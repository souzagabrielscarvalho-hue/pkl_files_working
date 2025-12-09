using Microsoft.Extensions.Logging;
using PklBridge.Core.Interfaces;
using PklBridge.Core.Models;
using System.Text;

namespace PklBridge.Infrastructure;

public class AstmMessageBuilder : IAstmMessageBuilder
{
    private readonly ILogger<AstmMessageBuilder> _logger;
    private int _frameNumber = 1;

    public AstmMessageBuilder(ILogger<AstmMessageBuilder> logger)
    {
        _logger = logger;
    }

    public byte[] BuildOrderMessage(ExamOrder order, PatientData patient)
    {
        try
        {
            _logger.LogInformation("Construindo mensagem ASTM de solicitação para paciente {PatientId}", patient.Id);

            var messages = new List<byte[]>();

            // 1. ENQ para iniciar comunicação
            messages.Add(new byte[] { 0x05 }); // ENQ

            // 2. Header Record (H)
            var headerFrame = BuildHeaderFrame();
            messages.Add(headerFrame);

            // 3. Patient Record (P)
            var patientFrame = BuildPatientFrame(patient);
            messages.Add(patientFrame);

            // 4. Order Records (O) - um para cada teste
            foreach (var testCode in order.TestCodes)
            {
                var orderFrame = BuildOrderFrame(order, testCode);
                messages.Add(orderFrame);
            }

            // 5. Terminator Record (L)
            var terminatorFrame = BuildTerminatorFrame();
            messages.Add(terminatorFrame);

            // 6. EOT para finalizar
            messages.Add(new byte[] { 0x04 }); // EOT

            // Combinar todas as mensagens
            var totalLength = messages.Sum(m => m.Length);
            var result = new byte[totalLength];
            var offset = 0;

            foreach (var message in messages)
            {
                Array.Copy(message, 0, result, offset, message.Length);
                offset += message.Length;
            }

            _logger.LogInformation("Mensagem ASTM construída com {FrameCount} frames, total {TotalBytes} bytes", 
                messages.Count, totalLength);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao construir mensagem ASTM de solicitação");
            throw;
        }
    }

    public byte[] BuildQueryMessage(string patientId, string specimenId)
    {
        try
        {
            _logger.LogInformation("Construindo mensagem ASTM de query para paciente {PatientId}", patientId);

            var messages = new List<byte[]>();

            // 1. ENQ
            messages.Add(new byte[] { 0x05 });

            // 2. Header Record (H)
            var headerFrame = BuildHeaderFrame();
            messages.Add(headerFrame);

            // 3. Query Record (Q)
            var queryFrame = BuildQueryFrame(patientId, specimenId);
            messages.Add(queryFrame);

            // 4. Terminator Record (L)
            var terminatorFrame = BuildTerminatorFrame();
            messages.Add(terminatorFrame);

            // 5. EOT
            messages.Add(new byte[] { 0x04 });

            // Combinar mensagens
            var totalLength = messages.Sum(m => m.Length);
            var result = new byte[totalLength];
            var offset = 0;

            foreach (var message in messages)
            {
                Array.Copy(message, 0, result, offset, message.Length);
                offset += message.Length;
            }

            _logger.LogInformation("Mensagem ASTM de query construída com {TotalBytes} bytes", totalLength);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao construir mensagem ASTM de query");
            throw;
        }
    }

    public byte CalculateChecksum(byte[] data, int startIndex, int endIndex)
    {
        int sum = 0;
        for (int i = startIndex; i <= endIndex; i++)
        {
            sum += data[i];
        }
        return (byte)(sum % 256);
    }

    public bool ValidateChecksum(byte[] message)
    {
        try
        {
            // Encontrar STX e ETX
            int stxIndex = -1, etxIndex = -1;
            
            for (int i = 0; i < message.Length; i++)
            {
                if (message[i] == 0x02) stxIndex = i; // STX
                if (message[i] == 0x03) etxIndex = i;  // ETX
            }

            if (stxIndex == -1 || etxIndex == -1 || etxIndex <= stxIndex + 1)
                return false;

            // Calcular checksum dos dados entre FN e ETX (inclusive)
            var calculatedChecksum = CalculateChecksum(message, stxIndex + 1, etxIndex);

            // Extrair checksum da mensagem (2 bytes após ETX)
            if (etxIndex + 2 >= message.Length)
                return false;

            var checksumHex = Encoding.ASCII.GetString(message, etxIndex + 1, 2);
            if (!byte.TryParse(checksumHex, System.Globalization.NumberStyles.HexNumber, null, out var receivedChecksum))
                return false;

            var isValid = calculatedChecksum == receivedChecksum;
            
            _logger.LogDebug("Validação checksum: calculado={CalculatedChecksum:X2}, recebido={ReceivedChecksum:X2}, válido={IsValid}",
                calculatedChecksum, receivedChecksum, isValid);

            return isValid;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao validar checksum ASTM");
            return false;
        }
    }

    private byte[] BuildHeaderFrame()
    {
        var frameNum = GetNextFrameNumber();
        var content = $"H|\\^&|||PPC 125|||||Host||1|{DateTime.Now:yyyyMMddHHmmss}";
        return BuildFrame(frameNum, content);
    }

    private byte[] BuildPatientFrame(PatientData patient)
    {
        var frameNum = GetNextFrameNumber();
        var age = patient.BirthDate.HasValue ? 
            (DateTime.Now.Year - patient.BirthDate.Value.Year).ToString() : "";
        var content = $"P|1||{patient.ExternalId}||{patient.FirstName} {patient.LastName}|||{patient.Gender}||||||{age}^Y";
        return BuildFrame(frameNum, content);
    }

    private byte[] BuildOrderFrame(ExamOrder order, string testCode)
    {
        var frameNum = GetNextFrameNumber();
        var specimenType = GetSpecimenType(testCode);
        // Formato correto segundo log.txt: ID^^^^Type (4 separadores vazios)
        var specimenId = $"{order.SpecimenId}^^^^N";
        var content = $"O|2|{specimenId}||^^^{testCode}|{order.Priority}|{order.OrderDateTime:yyyyMMddHHmmss}|||||||||{specimenType}||||||||||O";
        return BuildFrame(frameNum, content);
    }

    private byte[] BuildQueryFrame(string patientId, string specimenId)
    {
        var frameNum = GetNextFrameNumber();
        var content = $"Q|1|{specimenId}^^1^10^N||ALL||||||||O";
        return BuildFrame(frameNum, content);
    }

    private byte[] BuildTerminatorFrame()
    {
        var frameNum = GetNextFrameNumber();
        var content = "L|1|N";
        return BuildFrame(frameNum, content);
    }

    private byte[] BuildFrame(int frameNumber, string content)
    {
        // Formato: [STX] FN Content [ETX] CH CL [CR] [LF]
        var frameData = $"{frameNumber}{content}";
        var frameBytes = Encoding.ASCII.GetBytes(frameData);
        
        // Calcular checksum (FN + content + ETX)
        var checksumData = new List<byte>();
        checksumData.AddRange(frameBytes);
        checksumData.Add(0x03); // ETX
        
        var checksum = CalculateChecksum(checksumData.ToArray(), 0, checksumData.Count - 1);
        var checksumHex = checksum.ToString("X2");

        // Construir frame completo
        var frame = new List<byte>();
        frame.Add(0x02); // STX
        frame.AddRange(frameBytes);
        frame.Add(0x03); // ETX
        frame.AddRange(Encoding.ASCII.GetBytes(checksumHex));
        frame.Add(0x0D); // CR
        frame.Add(0x0A); // LF

        _logger.LogDebug("Frame {FrameNumber} construído: {Content} (checksum: {Checksum})", 
            frameNumber, content, checksumHex);

        return frame.ToArray();
    }

    private int GetNextFrameNumber()
    {
        var current = _frameNumber;
        _frameNumber = (_frameNumber % 8) + 1; // 1-7, depois 0, depois 1...
        if (_frameNumber == 8) _frameNumber = 0;
        return current;
    }

    private string GetSpecimenType(string testCode)
    {
        // Mapear códigos de teste para tipos de amostra
        var urineTests = new[] { "PROTEIN", "GLUCOSE", "KETONES", "BLOOD", "NITRITE", "LEUKOCYTES", "SG", "PH", "UROBILINOGEN", "BILIRUBIN" };
        
        if (urineTests.Contains(testCode.ToUpper()))
            return "2"; // Urine
            
        return "1"; // Serum (padrão)
    }
}
