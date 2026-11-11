using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using ServiceDesk.Services;

namespace ServiceDesk;

public sealed partial class MainPage
{
    private async void ConfiguracoesButton_Click(object sender, RoutedEventArgs e)
    {
        IReadOnlyDictionary<string, string> valores;
        try { valores = ConfiguracaoLocal.CarregarPropriedades(); }
        catch (InvalidOperationException) { valores = new Dictionary<string, string>(); }
        string Valor(string chave) => valores.TryGetValue(chave, out var valor) ? valor : "";
        var url = new TextBox { Header = "Endereço do Chatwoot", Text = Valor("chatwoot.base-url"), PlaceholderText = "https://chatwoot.suaempresa.com" };
        var conta = new TextBox { Header = "ID da conta", Text = Valor("chatwoot.account-id") };
        var chave = new PasswordBox { Header = "Chave da API", Password = Valor("chatwoot.api-access-token"), PasswordRevealMode = PasswordRevealMode.Hidden };
        var mostrar = new ToggleSwitch { Header = "Mostrar chave da API" };
        mostrar.Toggled += (_, _) => chave.PasswordRevealMode = mostrar.IsOn ? PasswordRevealMode.Visible : PasswordRevealMode.Hidden;
        var usuario = new TextBox { Header = "ID do agente (usuário)", Text = Valor("chatwoot.user-id") };
        var inbox = new TextBox { Header = "ID da caixa de entrada", Text = Valor("chatwoot.inbox-id") };
        var iniciarWindows = new ToggleSwitch { Header = "Iniciar com o Windows", IsOn = InicializacaoWindowsService.IniciarComWindows };
        var segundoPlano = new ToggleSwitch { Header = "Continuar em segundo plano ao fechar a janela", IsOn = InicializacaoWindowsService.ManterEmSegundoPlano };
        var instrucaoGeral = new TextBlock { Text = "Ao entrar no Windows, o ServiceDesk inicia junto ao relógio. Clique no ícone para abrir. Use o menu do ícone → Sair para encerrar completamente.", TextWrapping = TextWrapping.Wrap };
        var ativa = new ToggleSwitch { Header = "Atualizar atendimentos automaticamente", IsOn = _atualizacaoAutomaticaAtiva };
        var intervalo = new NumberBox { Header = "Intervalo de atualização (minutos)", Minimum = 1, Maximum = 120, Value = _intervaloAtualizacaoMinutos, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
        var notificacoes = new ToggleSwitch { Header = "Alertar conversas sem mensagem", IsOn = _notificacoesAtivas };
        var limite = new NumberBox { Header = "Alertar após quantos minutos", Minimum = 1, Maximum = 1440, Value = _limiteNotificacaoMinutos, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Inline };
        var status = new TextBlock { Text = $"Versão instalada: {AtualizacaoAplicativoService.VersaoAtual}", TextWrapping = TextWrapping.Wrap };
        var verificar = new Button { Content = "Verificar atualizações" };
        var atualizar = new Button { Content = "Atualizar", IsEnabled = false };
        var progresso = new ProgressRing { IsActive = false, Width = 24, Height = 24, HorizontalAlignment = HorizontalAlignment.Left };
        var informacao = new TextBlock { Text = "Ao atualizar, o ServiceDesk será fechado, terá os arquivos substituídos e abrirá novamente. As configurações são preservadas. Salve suas alterações antes de atualizar.", TextWrapping = TextWrapping.Wrap };
        StackPanel Painel(params UIElement[] itens)
        {
            var painel = new StackPanel { Spacing = 12, Margin = new Thickness(0, 12, 12, 0) };
            foreach (var item in itens) painel.Children.Add(item);
            return painel;
        }
        PivotItem Aba(string nome, StackPanel painel) => new() { Header = nome, Content = new ScrollViewer { Content = painel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } };
        var abas = new Pivot { Width = 480, Height = 400 };
        abas.Items.Add(Aba("Geral", Painel(iniciarWindows, segundoPlano, instrucaoGeral)));
        abas.Items.Add(Aba("Chatwoot", Painel(url, conta, chave, mostrar, usuario, inbox)));
        abas.Items.Add(Aba("Atendimentos", Painel(ativa, intervalo, notificacoes, limite)));
        abas.Items.Add(Aba("Versão", Painel(status, verificar, atualizar, progresso, informacao)));
        var erro = new TextBlock { TextWrapping = TextWrapping.Wrap };
        var dialogo = new ContentDialog { XamlRoot = XamlRoot, Title = "Configurações", Content = Painel(abas, erro), PrimaryButtonText = "Salvar", CloseButtonText = "Fechar" };
        AtualizacaoAplicativo? disponivel = null;
        var ocupado = false;
        dialogo.Closing += (_, args) => args.Cancel = ocupado;
        verificar.Click += async (_, _) =>
        {
            ocupado = true;
            verificar.IsEnabled = atualizar.IsEnabled = dialogo.IsPrimaryButtonEnabled = false;
            progresso.IsActive = true;
            try
            {
                disponivel = await new AtualizacaoAplicativoService().VerificarAsync(CancellationToken.None);
                status.Text = disponivel is null ? $"Versão {AtualizacaoAplicativoService.VersaoAtual}: não há atualização disponível." : $"Instalada: {AtualizacaoAplicativoService.VersaoAtual}. Disponível: {disponivel.Versao}. Clique em Atualizar para instalar.";
            }
            catch { status.Text = "Não foi possível consultar o GitHub. Confira sua conexão e tente novamente."; disponivel = null; }
            finally { ocupado = false; progresso.IsActive = false; verificar.IsEnabled = dialogo.IsPrimaryButtonEnabled = true; atualizar.IsEnabled = disponivel is not null; }
        };
        atualizar.Click += async (_, _) =>
        {
            if (disponivel is null) return;
            ocupado = true;
            verificar.IsEnabled = atualizar.IsEnabled = dialogo.IsPrimaryButtonEnabled = false;
            progresso.IsActive = true;
            status.Text = "Baixando e verificando a atualização. Aguarde; o aplicativo reiniciará ao concluir.";
            try
            {
                await new AtualizacaoAplicativoService().PrepararAsync(disponivel, CancellationToken.None);
                ocupado = false;
                ((App)Application.Current).Sair();
            }
            catch (Exception ex) { status.Text = $"Não foi possível atualizar: {ex.Message}"; }
            finally { ocupado = false; progresso.IsActive = false; verificar.IsEnabled = dialogo.IsPrimaryButtonEnabled = true; atualizar.IsEnabled = disponivel is not null; }
        };
        dialogo.PrimaryButtonClick += (_, args) =>
        {
            var temChatwoot = new[] { url.Text, conta.Text, chave.Password, usuario.Text, inbox.Text }.Any(x => !string.IsNullOrWhiteSpace(x));
            if (temChatwoot && (!Uri.TryCreate(url.Text.Trim(), UriKind.Absolute, out var endereco) || endereco.Scheme != "https" || !string.IsNullOrEmpty(endereco.UserInfo)
                || string.IsNullOrWhiteSpace(chave.Password) || !long.TryParse(conta.Text, out var contaId) || contaId <= 0
                || !long.TryParse(usuario.Text, out var usuarioId) || usuarioId <= 0 || !long.TryParse(inbox.Text, out var inboxId) || inboxId <= 0))
            { erro.Text = "Informe endereço HTTPS, chave da API e IDs positivos da conta, agente e caixa de entrada."; args.Cancel = true; return; }
            if (double.IsNaN(intervalo.Value) || double.IsNaN(limite.Value))
            { erro.Text = "Informe os intervalos em minutos."; args.Cancel = true; return; }
            try
            {
                InicializacaoWindowsService.Configurar(iniciarWindows.IsOn, segundoPlano.IsOn);
                if (temChatwoot) ConfiguracaoLocal.SalvarPropriedades(new Dictionary<string, string>
                {
                    ["chatwoot.base-url"] = url.Text.Trim().TrimEnd('/'), ["chatwoot.account-id"] = conta.Text.Trim(),
                    ["chatwoot.api-access-token"] = chave.Password, ["chatwoot.user-id"] = usuario.Text.Trim(), ["chatwoot.inbox-id"] = inbox.Text.Trim()
                });
                _configuracaoAtualizacao.Salvar(new ConfiguracaoAtualizacao(ativa.IsOn, Math.Clamp((int)Math.Round(intervalo.Value), 1, 120), notificacoes.IsOn, Math.Clamp((int)Math.Round(limite.Value), 1, 1440)));
            }
            catch (Exception ex) { erro.Text = $"Não foi possível salvar: {ex.Message}"; args.Cancel = true; }
        };
        if (await dialogo.ShowAsync() == ContentDialogResult.Primary)
        {
            CarregarConfiguracaoAtualizacao();
            await AtualizarAsync();
        }
    }
}
