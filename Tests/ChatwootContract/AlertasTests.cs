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
        Verificar(inicial.Elegiveis.Select(t => t.Id).SequenceEqual([1L, 2L]) && inicial.Atual?.Id == 1,
            "cada ticket aberto elegível gera seu próprio alerta, inclusive em um mesmo ciclo");
        var durante = fila.Atualizar(tickets, inicio.AddSeconds(2), 10, manterAtual: true);
        Verificar(durante.Atual?.TempoUltimaInteracaoAgente == "há 11min" && primeiro.TempoUltimaInteracao == "Agente: há 11min",
            "o relógio do alerta e dos cards continua após atingir o limite");
        var outroElegivel = fila.Atualizar(tickets, inicio.AddSeconds(5), 10, manterAtual: true);
        Verificar(outroElegivel.Elegiveis.Any(ticket => ticket.Id == 3) && outroElegivel.Atual?.Id == 1,
            "um ticket que atinge o limite durante outro alerta também entra na fila");
        var seguinte = fila.Atualizar(tickets, inicio.AddSeconds(8), 10, manterAtual: true);
        Verificar(seguinte.Atual?.Id == 2, "quando o primeiro aviso expira, o próximo é exibido");
        var dispensado = fila.Atualizar(tickets, inicio.AddSeconds(9), 10, manterAtual: false);
        Verificar(dispensado.Atual?.Id == 3, "fechar manualmente um aviso permite exibir o seguinte");
        var fim = fila.Atualizar(tickets, inicio.AddSeconds(17), 10, manterAtual: true);
        Verificar(fim.Atual is null && fim.Elegiveis.Count == 3, "o último aviso desaparece sem notificar novamente");
        primeiro.UltimaInteracaoAgenteEm = inicio.AddSeconds(18);
        var novaInteracao = fila.Atualizar(tickets, inicio.AddMinutes(11), 10, manterAtual: false);
        Verificar(novaInteracao.Atual?.Id == 1, "uma interação posterior reinicia o alerta daquele ticket");

        var aguardando = new AtendimentoMonitorado
        {
            Id = 6,
            Status = "open",
            AgenteAtribuidoId = 42,
            UltimaMensagemClienteEm = inicio
        };
        var semMensagens = new AtendimentoMonitorado { Id = 7, Status = "open", CriadoEm = inicio.AddDays(-1) };
        var filaSemResposta = new FilaAlertasAtendimento();
        var antesDoLimite = filaSemResposta.Atualizar([aguardando, semMensagens], inicio.AddMinutes(9), 10, manterAtual: false);
        Verificar(antesDoLimite.Elegiveis.Count == 0, "ticket aguardando primeira resposta respeita o limite");
        var noLimite = filaSemResposta.Atualizar([aguardando, semMensagens], inicio.AddMinutes(10), 10, manterAtual: false);
        Verificar(noLimite.Elegiveis.SingleOrDefault()?.Id == 6 && noLimite.Atual?.Id == 6,
            "mensagem do cliente sem resposta gera alerta interno e do Windows ao atingir o limite");
        Verificar(aguardando.DescreverAlerta(inicio.AddMinutes(10)) == "Cliente aguarda resposta há 10min",
            "alerta de primeira resposta informa o tempo de espera do cliente");
    }

    private static AtendimentoMonitorado Ticket(long id, DateTimeOffset interacao, string status = "open") =>
        new() { Id = id, Status = status, UltimaInteracaoAgenteEm = interacao };

    private static void Verificar(bool condicao, string descricao)
    {
        if (!condicao) throw new InvalidOperationException(descricao);
        Console.WriteLine("OK: " + descricao);
    }
}
