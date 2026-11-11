# Assinatura própria — Rafael Stolf Silva

O certificado próprio usa o nome Rafael Stolf Silva e destina-se à assinatura de código.
O link do publicador permanece https://github.com/RafaStolf/ServiceDesk.
Ele não garante reputação no SmartScreen.

## Material gerado neste ambiente

Os arquivos ficam fora do Git, em `/workspace/servicedesk-signing`:

- `Rafael-Stolf-Silva.cer`: certificado público; pode ser entregue ao colega.
- `ServiceDesk.pfx`: certificado com chave privada, protegido por senha.
- `password.txt`: senha do PFX; guardar em local protegido, separado do PFX.
- `private-key.pem`: cópia criptografada da chave privada; não distribuir.

Faça backup seguro antes de descartar o ambiente. Nunca envie PFX, chave privada
ou senha ao colega, nem os inclua em releases ou commits.

## Ordem de geração no Windows

1. Compilar a versão final em `publish` conforme o workflow Windows.
2. Executar o script abaixo sobre `publish/ServiceDesk.exe` e `publish/ServiceDesk.dll`.
3. Gerar o ZIP e o instalador a partir da pasta já assinada.
4. Executar o script sobre `ServiceDesk-Setup-win-x64.exe`.
5. Gerar SHA256SUMS.txt somente depois das assinaturas; os hashes mudam ao assinar.
6. Testar instalação, abertura e atualização antes de publicar.

```powershell
./Scripts/Assinar-Arquivos.ps1 -Certificado C:\Assinatura\ServiceDesk.pfx -ArquivoSenha C:\Assinatura\password.txt -Arquivos ./publish/ServiceDesk.exe,./publish/ServiceDesk.dll
./Scripts/Assinar-Arquivos.ps1 -Certificado C:\Assinatura\ServiceDesk.pfx -ArquivoSenha C:\Assinatura\password.txt -Arquivos ./ServiceDesk-Setup-win-x64.exe
```

O script exige carimbo de tempo e verifica a presença do certificado esperado.
No computador do colega, conferir a impressão digital SHA-256 do certificado por
um canal conhecido antes de importá-lo para Pessoas Confiáveis do usuário atual.
Não importar a chave privada. Essa confiança local não elimina a avaliação
independente de reputação do SmartScreen. O desinstalador gerado pelo Inno Setup
precisa de configuração própria de SignTool para também receber assinatura.

O workflow atual ainda não recebe essa chave e continua gerando releases sem assinatura.
Automatizar a assinatura exige configurar o PFX e sua senha em secrets privados do CI.
