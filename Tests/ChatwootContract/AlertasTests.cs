using ServiceDesk.Models;
using ServiceDesk.Services;

internal static class AlertasTests
{
    public static void Executar()
    {
        var inicio = new DateTimeOffset(2026, 11, 12, 9, 0, 0, TimeSpan.Zero);
        var primeiro = Ticket(1, inicio.AddMinutes(-10).AddSeconds(-59));
        var segundo = Ticket(2, inicio.AddMinutes(-10));
        var terceiro = Ticket(3, inicio.AddMinutes(-9).AddSeconds(-55));
        var adiado = Ticket(4, inicio.AddMinutes(-30), "snoozed");
        var resolvido = Ticket(5, inicio.AddMinutes(-30), "resolved");
        var tickets = new[] { primeiro, segundo, terceiro, adiado, resolvido };
        var fila = new FilaAlertasAtendimento();

        var inicial = fila.Atualizar(tickets, inicio, 10, manterAtual: false);
        Verificar(inicial.Elegiveis.Select(t => t.Ticket.Id).SequenceEqual([1L, 2L]) && inicial.Atual?.Ticket.Id == 1,
            "cada ticket aberto elegível gera seu próprio alerta, inclusive em um mesmo ciclo");
        var durante = fila.Atualizar(tickets, inicio.AddSeconds(2), 10, manterAtual: true);
        Verificar(durante.Atual?.Texto(inicio.AddSeconds(2)) == "Resposta em atraso · há 11 min",
            "o relógio do alerta e dos cards continua após atingir o limite");
        var outroElegivel = fila.Atualizar(tickets, inicio.AddSeconds(5), 10, manterAtual: true);
        Verificar(outroElegivel.Elegiveis.Any(atraso => atraso.Ticket.Id == 3) && outroElegivel.Atual?.Ticket.Id == 1,
            "um ticket que atinge o limite durante outro alerta também entra na fila");
        var seguinte = fila.Atualizar(tickets, inicio.AddSeconds(8), 10, manterAtual: true);
        Verificar(seguinte.Atual?.Ticket.Id == 2, "quando o primeiro aviso expira, o próximo é exibido");
        var dispensado = fila.Atualizar(tickets, inicio.AddSeconds(9), 10, manterAtual: false);
        Verificar(dispensado.Atual?.Ticket.Id == 3, "fechar manualmente um aviso permite exibir o seguinte");
        var fim = fila.Atualizar(tickets, inicio.AddSeconds(17), 10, manterAtual: true);
        Verificar(fim.Atual is null && fim.Elegiveis.Count == 3, "o último aviso desaparece sem notificar novamente");
        var episodio = inicial.Elegiveis[0].Episodio;
        primeiro.UltimaMensagemClienteEm = primeiro.UltimaMensagemPublicaEm = inicio.AddSeconds(19);
        Verificar(fila.Atualizar(tickets, inicio.AddSeconds(20), 10, false).Elegiveis[0].Episodio == episodio,
            "mensagens adicionais preservam o episódio com waiting_since oficial");
        var duranteFalha = fila.Atualizar([], inicio.AddSeconds(21), 10, false, dadosRecentes: false);
        Verificar(duranteFalha.Elegiveis.Count == 0 && duranteFalha.EpisodiosAbertos.Contains(episodio),
            "dados vencidos escondem alertas e preservam o episódio");
        var limiteAlterado = fila.Atualizar(tickets, inicio.AddSeconds(22), 15, false);
        var normalizado = fila.Atualizar(tickets, inicio.AddSeconds(23), 10, false);
        Verificar(limiteAlterado.Elegiveis.Count == 0 && normalizado.Atual is null && normalizado.Elegiveis[0].Episodio == episodio,
            "mudar o prazo ou recuperar a conexão não repete o alerta da mesma espera");
        fila.Atualizar(tickets, inicio.AddSeconds(24), 10, false, alertasAtivos: false);
        Verificar(fila.Atualizar(tickets, inicio.AddSeconds(25), 10, false).Atual is null,
            "reativar alertas não repete as esperas já avisadas");
        var esperaAnterior = primeiro.ClienteAguardaDesde;
        primeiro.ClienteAguardaDesde = null;
        var respondido = fila.Atualizar(tickets, inicio.AddSeconds(26), 10, false);
        Verificar(!respondido.EpisodiosAbertos.Contains(episodio), "a resposta confirmada encerra o episódio");
        primeiro.ClienteAguardaDesde = esperaAnterior;
        var reaberto = fila.Atualizar(tickets, inicio.AddSeconds(27), 10, false);
        Verificar(reaberto.Atual?.Ticket.Id == 1 && reaberto.Atual.Episodio != episodio,
            "uma espera confirmada após reabertura pode notificar com a mesma referência");
        primeiro.AgenteAtribuidoId = 43;
        Verificar(fila.Atualizar(tickets, inicio.AddSeconds(28), 10, false).Atual?.Episodio != reaberto.Atual?.Episodio,
            "uma mudança de atribuição tem identidade de alerta própria");

        var aguardando = new AtendimentoMonitorado
        {
            Id = 6,
            Status = "open",
            AgenteAtribuidoId = 42,
            UltimaMensagemClienteEm = inicio,
            UltimaMensagemPublicaEm = inicio
        };
        var semMensagens = new AtendimentoMonitorado { Id = 7, Status = "open", CriadoEm = inicio.AddDays(-1) };
        var filaSemResposta = new FilaAlertasAtendimento();
        var antesDoLimite = filaSemResposta.Atualizar([aguardando, semMensagens], inicio.AddMinutes(9), 10, manterAtual: false);
        Verificar(antesDoLimite.Elegiveis.Count == 0, "ticket aguardando primeira resposta respeita o limite");
        var noLimite = filaSemResposta.Atualizar([aguardando, semMensagens], inicio.AddMinutes(10), 10, manterAtual: false);
        Verificar(noLimite.Elegiveis.SingleOrDefault()?.Ticket.Id == 6 && noLimite.Atual?.Ticket.Id == 6,
            "mensagem do cliente sem resposta gera alerta interno e do Windows ao atingir o limite");
        Verificar(noLimite.Atual?.Texto(inicio.AddMinutes(10)) == "Resposta em atraso · há 10 min",
            "alerta de primeira resposta usa o mesmo texto do card");
        Verificar(FilaAlertasAtendimento.DadosRecentes(inicio, inicio.AddMinutes(5).AddSeconds(-1), null)
            && !FilaAlertasAtendimento.DadosRecentes(inicio, inicio.AddMinutes(5), null)
            && FilaAlertasAtendimento.DadosRecentes(inicio, inicio.AddMinutes(3).AddSeconds(-1), TimeSpan.FromMinutes(2))
            && !FilaAlertasAtendimento.DadosRecentes(inicio, inicio.AddMinutes(3), TimeSpan.FromMinutes(2)),
            "modo manual vence em cinco minutos e automático no intervalo mais um minuto");
    }

    private static AtendimentoMonitorado Ticket(long id, DateTimeOffset interacao, string status = "open") =>
        new() { Id = id, Status = status, EsperaInformadaPeloChatwoot = true, ClienteAguardaDesde = interacao };

    private static void Verificar(bool condicao, string descricao)
    {
        if (!condicao) throw new InvalidOperationException(descricao);
        Console.WriteLine("OK: " + descricao);
    }
}
