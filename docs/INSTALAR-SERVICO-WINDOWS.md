# Como Instalar o PKL Bridge como Serviço do Windows

## ✅ Status: PRONTO PARA INSTALAÇÃO

O projeto **PklBridge.Service** já está configurado para funcionar como um Windows Service.

## 📋 Pré-requisitos

- Windows 10/11 ou Windows Server
- .NET 8.0 Runtime instalado
- Permissões de Administrador

## 🔧 Configuração Atual

O serviço já possui:

- ✅ Pacote `Microsoft.Extensions.Hosting.WindowsServices` instalado
- ✅ Configuração `.UseWindowsService()` no Program.cs
- ✅ Nome do serviço: **"PKL Bridge Service"**
- ✅ Logs configurados (Console, Arquivo e Event Log do Windows)
- ✅ Health checks implementados

## 📦 Passo 1: Publicar o Projeto

Clone o repositório (caso ainda não tenha o código local):

```powershell
git clone https://github.com/Vida-Exames/pkl_integrator.git
```

Abra o PowerShell como **Administrador** e navegue até a pasta `PklBridge.Service` do projeto. Publique o projeto:

```powershell
dotnet publish -c Release -r win-x64 --self-contained false -o "C:\PklBridge"
```

**Parâmetros:**

- `-c Release`: Compilação em modo Release
- `-r win-x64`: Runtime para Windows 64-bit
- `--self-contained false`: Requer .NET Runtime instalado (menor tamanho)
- `-o "C:\PklBridge"`: Pasta de destino

> **Nota:** Se preferir um executável independente (não requer .NET instalado), use `--self-contained true`

## 🔨 Passo 2: Instalar o Serviço

### Opção A: Usando sc.exe (Nativo do Windows)

```powershell
sc.exe create "PKLBridgeService" binPath= "C:\PklBridge\PklBridge.Service.exe" start= auto DisplayName= "PKL Bridge Service"
```

**Parâmetros:**

- `binPath=`: Caminho completo do executável (ATENÇÃO: espaço após o `=`)
- `start= auto`: Inicia automaticamente com o Windows
- `DisplayName=`: Nome exibido no Gerenciador de Serviços

### Opção B: Usando PowerShell (Recomendado)

```powershell
New-Service -Name "PKLBridgeService" `
    -BinaryPathName "C:\PklBridge\PklBridge.Service.exe" `
    -DisplayName "PKL Bridge Service" `
    -Description "Serviço de integração entre equipamento PKL-125 e API VIDA" `
    -StartupType Automatic
```

## ▶️ Passo 3: Iniciar o Serviço

```powershell
Start-Service -Name "PKLBridgeService"
```

Ou usando sc.exe:

```powershell
sc.exe start PKLBridgeService
```

## 🔍 Passo 4: Verificar Status

```powershell
Get-Service -Name "PKLBridgeService"
```

Ou abra o **Gerenciador de Serviços** do Windows:

1. Pressione `Win + R`
2. Digite `services.msc`
3. Procure por "PKL Bridge Service"

## 📝 Logs do Serviço

Os logs são gravados em:

- **Arquivo:** `C:\PklBridge\logs\pkl-bridge-YYYY-MM-DD.log`
- **Event Viewer:** Application Log (eventos Warning e Error)

Para visualizar logs no Event Viewer:

1. Pressione `Win + R`
2. Digite `eventvwr.msc`
3. Navegue até: **Windows Logs → Application**
4. Filtre por fonte: "PKL Bridge Service"

## 🛑 Comandos Úteis

### Parar o Serviço

```powershell
Stop-Service -Name "PKLBridgeService"
```

### Reiniciar o Serviço

```powershell
Restart-Service -Name "PKLBridgeService"
```

### Desinstalar o Serviço

```powershell
# Primeiro, pare o serviço
Stop-Service -Name "PKLBridgeService"

# Depois, remova
sc.exe delete PKLBridgeService
```

### Alterar Tipo de Inicialização

```powershell
# Manual
Set-Service -Name "PKLBridgeService" -StartupType Manual

# Automático
Set-Service -Name "PKLBridgeService" -StartupType Automatic

# Desabilitado
Set-Service -Name "PKLBridgeService" -StartupType Disabled
```

## ⚙️ Configuração (appsettings.json)

Antes de instalar, configure o arquivo `C:\PklBridge\appsettings.json`:

```json
{
  "BridgeSettings": {
    "TransportMode": "Tcp",
    "TcpPort": 8081
  },
  "VidaApi": {
    "BaseUrl": "https://apoio.internal.vidaexame.com",
    "IntegrationEndpoint": "/api/integration/pkl-125",
    "FranchiseCredentialId": "88cf9273-5044-47f4-b8f6-01160345a190",
    "ApiKey": "",
    "TimeoutSeconds": 30
  }
}
```

> Para o JSON completo com todos os campos (Serial, Polly, AstmSettings, HealthCheckSettings, etc.), ver [PklBridge.Service/appsettings.json](../PklBridge.Service/appsettings.json) e o README principal.

## 🔐 Permissões

O serviço roda por padrão com a conta **Local System**. Se precisar de permissões específicas:

```powershell
sc.exe config PKLBridgeService obj= "DOMINIO\Usuario" password= "senha"
```

## 🚨 Troubleshooting

### Serviço não inicia

1. Verifique os logs em `C:\PklBridge\logs\`
2. Verifique o Event Viewer
3. Teste o executável manualmente:

```powershell
cd C:\PklBridge
.\PklBridge.Service.exe
```

### Porta TCP já em uso

Altere a porta no `appsettings.json`:

```json
{
  "BridgeSettings": {
    "TcpPort": 8082
  }
}
```

### Erro de permissão

Execute o PowerShell como **Administrador** e reinstale o serviço.

## 📊 Monitoramento

Health checks rodam internamente (intervalo configurável em `HealthCheckSettings`). Os resultados aparecem nos logs marcados com `[Health]` ou `HealthCheck`. **Não há endpoint HTTP exposto** — para verificar o estado do serviço, consulte os logs em `C:\PklBridge\logs\` ou o Event Viewer.

## 🔄 Atualização do Serviço

Para atualizar:

1. Pare o serviço
2. Publique a nova versão
3. Substitua os arquivos em `C:\PklBridge`
4. Inicie o serviço

```powershell
Stop-Service -Name "PKLBridgeService"
dotnet publish -c Release -r win-x64 --self-contained false -o "C:\PklBridge"
Start-Service -Name "PKLBridgeService"
```

## ✅ Checklist de Instalação

- [ ] .NET 8.0 Runtime instalado
- [ ] Projeto publicado em `C:\PklBridge`
- [ ] `appsettings.json` configurado
- [ ] Serviço criado com `sc.exe` ou `New-Service`
- [ ] Serviço iniciado
- [ ] Status verificado (Running)
- [ ] Logs verificados
- [ ] Teste de conexão realizado

## 📞 Suporte

Em caso de problemas, verifique:

1. Logs em `C:\PklBridge\logs\pkl-bridge-YYYY-MM-DD.log` (procure por `[Health]` para estado do serviço)
2. Event Viewer → Application Log (filtrar por fonte "PKL Bridge Service")
3. Status do serviço: `Get-Service -Name "PKLBridgeService"`
