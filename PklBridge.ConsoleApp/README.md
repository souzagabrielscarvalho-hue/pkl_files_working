# PKL Bridge - Aplicação Console para Testes

Esta é a versão console do PKL Bridge, ideal para **testes e desenvolvimento**. Diferentemente da versão Service (Windows Service), esta aplicação roda em modo console e permite interação direta.

## 🚀 Como Executar

### Método 1: Script Automático
```bash
# Executa o script que compila e roda automaticamente
iniciar-teste.bat
```

### Método 2: Comandos Manuais
```bash
# Compilar
dotnet build --configuration Release

# Executar
dotnet run --configuration Release
```

## 🎮 Como Usar

1. **Inicie a aplicação** - ela mostrará:
   ```
   🚀 PKL Bridge - Aplicação de Console para Testes
   ===============================================
   
   ✅ PKL Bridge iniciado com sucesso!
   📡 Named Pipe ativo: \\.\pipe\pkl_serial
   🔌 Aguardando conexão do HLAB...
   
   Pressione 'q' + ENTER para sair
   Pressione qualquer outra tecla + ENTER para status
   ```

2. **Configure o HLAB** para enviar dados para:
   ```
   \\.\pipe\pkl_serial
   ```

3. **Monitore os logs** no console - você verá:
   - ✅ Conexões do HLAB
   - 📨 Mensagens recebidas 
   - 🔄 Processamento ASTM
   - 📤 Envio para API VIDA (mockado)

4. **Para sair**: Digite `q` + ENTER

## 📊 Status e Monitoramento

### Logs em Tempo Real
- **Console**: Todos os eventos importantes
- **Arquivo**: `logs/pkl-bridge-console-YYYY-MM-DD.log`

### Health Check
Pressione qualquer tecla + ENTER para ver:
```
⏰ Status: 14:30:15 - PKL Bridge rodando
💾 Memória: 45MB
```

### Eventos Importantes
- `📨 Mensagem recebida de HLAB: X bytes` - Dados chegaram
- `🔄 Parsed X ASTM messages` - Mensagens processadas  
- `MOCK: Sending batch X with Y results` - Enviando para API (mock)
- `💚 Health Check - Bridge: Rodando` - Sistema saudável

## ⚙️ Configuração

### appsettings.json
```json
{
  "BridgeSettings": {
    "PipeName": "pkl_serial",           // Nome do pipe
    "LogAllTraffic": true,              // Log detalhado
    "EnableHardwareBridge": false       // COM2 desabilitado
  },
  "VidaApi": {
    "BaseUrl": "https://localhost:5000/api"  // API mockada
  }
}
```

## 🔧 Diferenças da Versão Service

| Recurso | Console App | Service |
|---------|-------------|---------|
| **Execução** | Manual, interativa | Automática, background |
| **Logs** | Console + arquivo | Arquivo + EventLog |
| **API** | Mock (teste) | Real (produção) |
| **Shutdown** | 'q' + ENTER | sc stop |
| **Startup** | Manual | Automático |

## 🧪 Testes

### Teste 1: Verificar Named Pipe
1. Execute a aplicação
2. Veja a mensagem: `📡 Named Pipe ativo: \\.\pipe\pkl_serial`
3. Status deve mostrar `Bridge: Rodando`

### Teste 2: Simular Dados ASTM
Use uma ferramenta como `PipeTest.exe` ou `echo` para enviar dados:
```bash
echo "teste" > \\.\pipe\pkl_serial
```

### Teste 3: Monitorar Processamento
- Configure HLAB para usar `\\.\pipe\pkl_serial`
- Execute um exame na PKL 125
- Observe os logs no console
- Verifique se aparecem mensagens `MOCK: Sending batch`

## ❌ Solução de Problemas

### "Erro: Named Pipe não pode ser criado"
- Verifique se já existe outra instância rodando
- Execute como Administrador se necessário

### "Nenhuma mensagem recebida"
- Verifique se HLAB está configurado corretamente
- Confirme o nome do pipe: `\\.\pipe\pkl_serial`
- Verifique se o protocolo é ASTM E1394-97

### "Erro de compilação"
- Certifique-se que .NET 8 SDK está instalado
- Execute `dotnet restore` primeiro

## 🎯 Próximos Passos

1. **Teste Local** ✅ - Use esta versão console
2. **Configurar HLAB** - Alterar saída para Named Pipe
3. **Testar com Dados Reais** - Executar exames na PKL 125
4. **Migrar para Service** - Quando tudo estiver funcionando
5. **Configurar API Real** - Substituir mock pela API VIDA

## 💡 Dicas

- **Mantenha o console aberto** durante os testes
- **Use LogAllTraffic=true** para debug detalhado
- **Monitore a pasta logs/** para histórico
- **Teste primeiro sem HLAB** usando ferramentas pipe
