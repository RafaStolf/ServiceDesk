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

## Publicação automática no GitHub Actions

O workflow assina `ServiceDesk.exe` e `ServiceDesk.dll` antes do ZIP e do instalador.
O Inno Setup assina o instalador e o desinstalador com SignTool e carimbo de tempo
RFC 3161/SHA-256. A instalação de teste verifica as assinaturas do aplicativo e do
desinstalador. Hashes são gerados depois das assinaturas. A release inclui apenas
`ServiceDesk-Publicador.cer`; nenhuma chave privada ou senha acompanha os downloads.

PRs usam certificado descartável de teste, sem acesso ao certificado do publicador,
e não publicam releases. Publicações exigem os dois secrets abaixo e falham se
estiverem ausentes; não há fallback silencioso para arquivos sem assinatura:

- `SERVICEDESK_CERTIFICATE_PFX_BASE64`
- `SERVICEDESK_CERTIFICATE_PASSWORD`

No seu Windows, instale o GitHub CLI e autentique uma conta com permissão para
administrar os secrets do repositório:

```powershell
winget install --id GitHub.cli
# Reabra o PowerShell se o comando gh ainda não for encontrado.
gh auth login
```

Salve `Scripts/Configurar-SecretsAssinatura.ps1` na pasta local dos certificados e execute:

```powershell
cd "D:\Documentos\Arquivos\Centrium\Projetos\Certificados\ServiceDesk-Assinatura"
.\Configurar-SecretsAssinatura.ps1
```

O script lê `ServiceDesk.pfx` e `password.txt` e envia os valores pela entrada padrão
do GitHub CLI, sem colocá-los em argumentos ou mostrar seus conteúdos. Confira
apenas os nomes em Settings → Secrets and variables → Actions. Não cole valores
em comentários, issues, mensagens ou arquivos versionados.

Os certificados privados são importados apenas no runner Windows descartável;
o PFX temporário é removido após a importação, e o certificado é removido ao final.
O runner confia no certificado próprio apenas para verificar as assinaturas durante
a compilação. Isso não configura confiança nos computadores dos usuários.
