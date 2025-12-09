# Tech Context - Interface PKL Hemograma

## Stack Tecnológico

### Framework Principal
- **.NET 8**: Framework moderno com suporte LTS
- **C#**: Linguagem principal
- **Microsoft.Extensions.Hosting**: Para Windows Service
- **Microsoft.Extensions.DependencyInjection**: IoC Container
- **Microsoft.Extensions.Configuration**: Gerenciamento de configuração

### Comunicação Serial
- **System.IO.Ports**: Biblioteca nativa para comunicação serial
- **Protocolo ASTM E1394-97**: Padrão para equipamentos laboratoriais
- **Encoding ASCII**: Codificação de caracteres padrão
- **Checksum**: Verificação de integridade hexadecimal

### Comunicação HTTP
- **HttpClient**: Cliente HTTP nativo
- **System.Text.Json**: Serialização JSON moderna
- **Authentication**: Bearer token ou Basic Auth
- **Retry Policy**: Polly para resiliência

### Logging
- **Microsoft.Extensions.Logging**: Interface padrão
- **Serilog**: Implementação estruturada
- **Sinks**: File, Console, EventLog
- **Structured Logging**: JSON format

### Configuração
- **appsettings.json**: Configuração base
- **appsettings.Production.json**: Configuração produção
- **Environment Variables**: Configuração sensível
- **Options Pattern**: Tipagem forte

## Protocolo ASTM E1394-97

### Caracteres de Controle
```csharp
public static class ControlCodes
{
    public const byte STX = 0x02;  // Start of Text
    public const byte ETX = 0x03;  // End of Text
    public const byte EOT = 0x04;  // End of Transmission
    public const byte ENQ = 0x05;  // Enquiry
    public const byte ACK = 0x06;  // Acknowledge
    public const byte NAK = 0x15;  // Not Acknowledged
    public const byte CR = 0x0D;   // Carriage Return
    public const byte LF = 0x0A;   // Line Feed
}
```

### Estrutura de Mensagem
```
[STX][FN][TEXT][ETX][CH][CL][CR][LF]
```

### Tipos de Registro
- **H**: Header Record - Cabeçalho da mensagem
- **P**: Patient Record - Informações do paciente
- **O**: Order Record - Solicitação de exame
- **R**: Result Record - Resultado do exame
- **Q**: Query Record - Consulta de informações
- **L**: Terminator Record - Terminador da mensagem

### Cálculo de Checksum
```csharp
public static string CalculateChecksum(string message)
{
    int sum = 0;
    for (int i = 1; i < message.Length; i++) // Pula STX
    {
        sum += (int)message[i];
        if (message[i] == 0x03) break; // Para no ETX
    }
    byte checksum = (byte)(sum % 256);
    return checksum.ToString("X2");
}
```

## Configurações Seriais

### Padrões PKL 125
```json
{
  "Serial": {
    "PortName": "COM2",
    "BaudRate": 19200,
    "DataBits": 8,
    "Parity": "None",
    "StopBits": "One",
    "Handshake": "None",
    "ReadTimeout": 5000,
    "WriteTimeout": 5000
  }
}
```

### Configurações Alternativas
- **BaudRate**: 4800, 9600, 19200, 57600
- **DataBits**: 7, 8
- **Parity**: None, Even, Odd
- **StopBits**: 1, 2

## API Sistema VIDA

### Endpoints Esperados
```csharp
public class VidaApiEndpoints
{
    public const string GetTestOrders = "/api/test-orders";
    public const string PostResults = "/api/results";
    public const string GetPatientInfo = "/api/patients/{id}";
    public const string UpdateOrderStatus = "/api/test-orders/{id}/status";
}
```

### Modelos de Dados
```csharp
public class TestOrderRequest
{
    public string PatientId { get; set; }
    public string SampleId { get; set; }
    public List<string> Tests { get; set; }
    public DateTime RequestedDate { get; set; }
}

public class TestResult
{
    public string SampleId { get; set; }
    public string TestCode { get; set; }
    public string Value { get; set; }
    public string Unit { get; set; }
    public string ReferenceRange { get; set; }
    public string Flag { get; set; }
    public DateTime CompletedDate { get; set; }
}
```

## Dependências NuGet

### Essenciais
```xml
<PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Hosting.WindowsServices" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Configuration" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Configuration.Json" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Logging" Version="8.0.0" />
<PackageReference Include="System.IO.Ports" Version="8.0.0" />
```

### Logging
```xml
<PackageReference Include="Serilog.Extensions.Hosting" Version="8.0.0" />
<PackageReference Include="Serilog.Sinks.File" Version="5.0.0" />
<PackageReference Include="Serilog.Sinks.Console" Version="5.0.0" />
<PackageReference Include="Serilog.Sinks.EventLog" Version="3.1.0" />
```

### HTTP e Resiliência
```xml
<PackageReference Include="Microsoft.Extensions.Http" Version="8.0.0" />
<PackageReference Include="Polly.Extensions.Http" Version="3.0.0" />
```

## Estrutura de Pastas
```
InterfacePKL/
├── src/
│   ├── InterfacePKL.Service/
│   │   ├── Services/
│   │   ├── Models/
│   │   ├── Configuration/
│   │   └── Program.cs
│   ├── InterfacePKL.Core/
│   │   ├── Interfaces/
│   │   ├── Models/
│   │   └── Enums/
│   └── InterfacePKL.Infrastructure/
│       ├── Serial/
│       ├── Http/
│       └── Logging/
├── tests/
│   ├── InterfacePKL.Tests/
│   └── InterfacePKL.IntegrationTests/
└── docs/
    └── README.md
```

## Configuração de Ambiente

### Desenvolvimento
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft": "Information"
    }
  },
  "Serial": {
    "PortName": "COM2",
    "BaudRate": 19200
  },
  "VidaApi": {
    "BaseUrl": "https://api-dev.vida.com",
    "Timeout": 30000
  }
}
```

### Produção
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  },
  "Serial": {
    "PortName": "COM2",
    "BaudRate": 19200
  },
  "VidaApi": {
    "BaseUrl": "https://api.vida.com",
    "Timeout": 30000
  }
}
```

## Testes Suportados

### Códigos de Teste PKL 125
```csharp
public static class TestCodes
{
    public const string BUN = "BUN";      // Blood Urea Nitrogen
    public const string CREA = "CREA";    // Creatinina
    public const string GLUC = "GLUC";    // Glicose
    public const string URIC = "URIC";    // Ácido Úrico
    public const string CHOL = "CHOL";    // Colesterol Total
    public const string TG = "TG";        // Triglicerídeos
    public const string HDL = "HDL";      // HDL Colesterol
    public const string LDL = "LDL";      // LDL Colesterol
    public const string TP = "TP";        // Proteína Total
    public const string ALB = "ALB";      // Albumina
    public const string TB = "TB";        // Bilirrubina Total
    public const string DB = "DB";        // Bilirrubina Direta
    public const string AST = "AST";      // Aspartato Aminotransferase
    public const string ALT = "ALT";      // Alanina Aminotransferase
    public const string ALP = "ALP";      // Fosfatase Alcalina
}
```

## Instalação e Deployment

### Instalação do Serviço
```bash
sc create "InterfacePKL" binPath="C:\Services\InterfacePKL\InterfacePKL.Service.exe"
sc description "InterfacePKL" "Interface PKL Hemograma - Sistema VIDA"
sc config "InterfacePKL" start=auto
sc start "InterfacePKL"
```

### Configuração de Firewall
- **Porta Serial**: Física, não requer firewall
- **API VIDA**: HTTPS (443) outbound
- **Logs**: Acesso local ao sistema de arquivos
