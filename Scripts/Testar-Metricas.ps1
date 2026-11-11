param([Parameter(Mandatory=$true)][string]$PackagePath)
$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true') { throw 'Este teste usa um perfil temporário de configuração e deve executar apenas no runner isolado do GitHub Actions.' }
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
Write-Host ('UIAutomation apartment: ' + [Threading.Thread]::CurrentThread.GetApartmentState())
$root = Join-Path $env:TEMP ('ServiceDesk-Metricas-Test-' + [guid]::NewGuid().ToString('N'))
$configDir = Join-Path $env:USERPROFILE '.servicedesk'
$config = Join-Path $configDir 'application.properties'
$existia = Test-Path $config
$original = if ($existia) { [IO.File]::ReadAllBytes($config) } else { $null }
New-Item -ItemType Directory -Path $root, $configDir -Force | Out-Null
$tcp = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
$tcp.Start(); $porta = $tcp.LocalEndpoint.Port; $tcp.Stop()
$server = Start-Job -ArgumentList $porta, $root -ScriptBlock {
    param($porta, $root)
    $http = [Net.HttpListener]::new()
    $http.Prefixes.Add("http://127.0.0.1:$porta/")
    $http.Start()
    Set-Content (Join-Path $root 'pronto') 'ok'
    try {
        while ($http.IsListening) {
            $espera = $http.GetContextAsync()
            while (!$espera.IsCompleted -and !(Test-Path (Join-Path $root 'parar'))) { Start-Sleep -Milliseconds 100 }
            if (Test-Path (Join-Path $root 'parar')) { break }
            try { $context = $espera.GetAwaiter().GetResult() }
            catch { if (!$http.IsListening) { break }; continue }
            try {
                if ($context.Request.HttpMethod -ne 'GET' -or $context.Request.Headers['api_access_token'] -ne 'TOKEN-FICTICIO-UI') {
                    $context.Response.StatusCode = 403; $context.Response.Close(); continue
                }
                $path = $context.Request.Url.AbsolutePath
                $agora = [DateTimeOffset]::UtcNow
                $epoch = $agora.ToUnixTimeSeconds()
                $date = $agora.ToString('O')
                if ($path -eq '/api/v1/profile') { $json = '{"id":42}' }
                elseif ($path.EndsWith('/reporting_events')) {
                    if (Test-Path (Join-Path $root 'atrasar')) {
                        Set-Content (Join-Path $root 'atrasando') '1'
                        Start-Sleep -Seconds 5
                        Remove-Item (Join-Path $root 'atrasando') -Force -ErrorAction SilentlyContinue
                    }
                    $json = @(
                        @{name='first_response'; value=60; value_in_business_hours=0; user_id=42; created_at=$date},
                        @{name='reply_time'; value=120; value_in_business_hours=60; user_id=42; created_at=$date},
                        @{name='conversation_resolved'; value=600; value_in_business_hours=300; user_id=42; created_at=$date}
                    ) | ConvertTo-Json -Depth 8 -Compress
                }
                elseif ($path.EndsWith('/messages')) {
                    $json = @{payload=@(
                        @{id=1;message_type=0;private=$false;created_at=($epoch-120);sender=@{id=20;type='contact'}},
                        @{id=2;message_type=1;private=$false;created_at=($epoch-60);sender=@{id=42;type='user'}}
                    )} | ConvertTo-Json -Depth 8 -Compress
                }
                elseif ($path -eq '/api/v1/accounts/1/conversations') {
                    $tickets = @()
                    if ($context.Request.QueryString['page'] -eq '1' -and $context.Request.QueryString['status'] -ne 'snoozed') {
                        $tickets = @(@{id=123;status='open';created_at=($epoch-600);meta=@{sender=@{name='Contato de teste';additional_attributes=@{company_name='Empresa de teste'}};assignee_type='User';assignee=@{id=42}};
                            applied_sla=@{sla_name='Política de teste';sla_status='active_with_misses';sla_first_response_time_threshold=300;sla_next_response_time_threshold=600;sla_resolution_time_threshold=3600;sla_only_during_business_hours=$true;sla_frt_due_at=($epoch-60);updated_at=$epoch};
                            sla_events=@(@{event_type='frt';created_at=$epoch})})
                    }
                    $json = @{data=@{payload=$tickets}} | ConvertTo-Json -Depth 12 -Compress
                }
                else { $context.Response.StatusCode=404; $json='{}' }
                $bytes = [Text.Encoding]::UTF8.GetBytes($json)
                $context.Response.ContentType = 'application/json; charset=utf-8'
                $context.Response.OutputStream.Write($bytes,0,$bytes.Length)
            } catch { } finally {
                # O cliente cancela e fecha o socket durante o teste. Falha ao
                # fechar essa resposta não pode encerrar o servidor sintético.
                try { $context.Response.Close() } catch { }
            }
        }
    } finally { $http.Close() }
}
$app = $null
function Encontrar([string]$nome) {
    $condition = [System.Windows.Automation.PropertyCondition]::new([System.Windows.Automation.AutomationElement]::NameProperty, $nome)
    return $script:janela.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $condition)
}
function Aguardar([string]$nome) {
    for ($i=0; $i -lt 100; $i++) {
        if ($app.HasExited) { throw "Aplicativo encerrou durante teste: $nome" }
        $elemento = Encontrar $nome
        if ($null -ne $elemento -and $elemento.Current.IsEnabled) { return $elemento }
        Start-Sleep -Milliseconds 100
    }
    throw "Controle não encontrado ou desabilitado: $nome"
}
function Clicar([string]$nome) {
    Write-Host "Acionando: $nome"
    $elemento = Aguardar $nome
    # Controles no ScrollViewer continuam acionáveis por acessibilidade;
    # o foco solicita sua rolagem para a área visível antes de Invoke.
    if ($elemento.Current.IsOffscreen) { $elemento.SetFocus() }
    $padrao = $null
    if ($elemento.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$padrao)) {
        try { $padrao.Invoke() }
        catch {
            # A navegação pode remover o peer UIA antes de Invoke retornar.
            # Aceite apenas essa invalidação esperada; o próximo Aguardar verifica
            # o destino real. Outras exceções continuam falhando o teste.
            $transicao = $nome -in @('Métricas', 'Voltar à fila', 'Fechar') -or $nome.StartsWith('Ver detalhes do ticket ')
            if ($transicao -and $_.Exception.InnerException -is [System.Windows.Automation.ElementNotAvailableException]) {
                Write-Host "Peer substituído na navegação: $nome; verificando a tela de destino."
            } else { throw }
        }
    }
    elseif ($elemento.TryGetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern, [ref]$padrao)) { $padrao.Toggle() }
    else { throw "Controle não acionável: $nome" }
}
try {
    for ($i=0; $i -lt 50 -and !(Test-Path (Join-Path $root 'pronto')); $i++) { Start-Sleep -Milliseconds 100 }
    if (!(Test-Path (Join-Path $root 'pronto'))) { throw 'Servidor sintético não iniciou.' }
    @("chatwoot.base-url=http://127.0.0.1:$porta", 'chatwoot.api-access-token=TOKEN-FICTICIO-UI', 'chatwoot.account-id=1', 'chatwoot.inbox-id=1') | Set-Content $config -Encoding utf8
    $env:SERVICEDESK_DIAGNOSTICO = '1'
    $app = Start-Process (Join-Path $PackagePath 'ServiceDesk.exe') -WorkingDirectory $PackagePath -PassThru
    $handleFile = Join-Path $env:TEMP "ServiceDesk-window-$($app.Id).txt"
    for ($i=0; $i -lt 100 -and !(Test-Path $handleFile); $i++) { Start-Sleep -Milliseconds 100 }
    if (!(Test-Path $handleFile)) { throw 'Janela não iniciou.' }
    $script:janela = [System.Windows.Automation.AutomationElement]::FromHandle([IntPtr][long](Get-Content $handleFile))
    Clicar 'Todos (1)'
    Clicar 'Métricas'
    Aguardar 'Métricas e SLA' | Out-Null
    Aguardar '1min 0s' | Out-Null
    Clicar 'Ver detalhes do ticket 123'
    Aguardar 'Política de teste · Em acompanhamento · com violação' | Out-Null
    Clicar 'Fechar'
    Clicar 'Histórico'
    Aguardar 'Atualizar' | Out-Null
    Clicar 'Hoje'
    Aguardar 'Atualizar' | Out-Null
    Aguardar '1min 0s' | Out-Null
    # Exercita cancelamento de uma consulta real ao servidor sintético.
    Set-Content (Join-Path $root 'atrasar') '1'
    Clicar 'Atualizar'
    Clicar 'Cancelar consulta'
    Aguardar 'Consulta cancelada. Os resultados anteriores, se houver, permanecem identificados abaixo.' | Out-Null
    Remove-Item (Join-Path $root 'atrasar') -Force
    for ($i=0; $i -lt 60 -and (Test-Path (Join-Path $root 'atrasando')); $i++) { Start-Sleep -Milliseconds 100 }
    Clicar 'Voltar à fila'
    $todos = Aguardar 'Todos (1)'
    $toggle = $todos.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
    if ($toggle.Current.ToggleState -ne [System.Windows.Automation.ToggleState]::On) { throw 'Retorno perdeu o filtro selecionado da fila.' }
    Clicar 'Métricas'
    Aguardar 'Métricas e SLA' | Out-Null
    Aguardar 'Atualizar' | Out-Null
    Aguardar '1min 0s' | Out-Null
    Clicar 'Voltar à fila'
    Write-Host 'OK: navegação fila/métricas/fila, histórico e Hoje, médias oficiais, detalhes de SLA, cancelamento e filtro preservado; somente API sintética local.'
} catch {
    Write-Host $_.ScriptStackTrace
    Write-Host $_.Exception.ToString()
    Receive-Job $server -Keep -ErrorAction Continue | Out-String | Write-Host
    if ($null -ne $app -and !$app.HasExited -and $null -ne $script:janela) {
        $elementos = $script:janela.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
        for ($i=0; $i -lt [Math]::Min(80,$elementos.Count); $i++) {
            try { Write-Host ('UI: ' + $elementos[$i].Current.Name) } catch { }
        }
    }
    Get-WinEvent -FilterHashtable @{LogName='Application';ProviderName=@('.NET Runtime','Application Error'); StartTime=(Get-Date).AddMinutes(-4)} -ErrorAction SilentlyContinue | Select-Object -First 5 Message | Format-List | Out-String | Write-Host
    Get-Content (Join-Path $env:TEMP 'ServiceDesk-inicializacao.log') -Tail 40 -ErrorAction SilentlyContinue | Write-Host
    throw
} finally {
    if ($null -ne $app -and !$app.HasExited) { Stop-Process -Id $app.Id -Force }
    Set-Content (Join-Path $root 'parar') '1'
    Wait-Job $server -Timeout 10 | Out-Null
    Stop-Job $server -ErrorAction SilentlyContinue
    Remove-Job $server -Force -ErrorAction SilentlyContinue
    if ($existia) { [IO.File]::WriteAllBytes($config, $original) } else { Remove-Item $config -Force -ErrorAction SilentlyContinue }
    Remove-Item $root -Force -Recurse -ErrorAction SilentlyContinue
}
