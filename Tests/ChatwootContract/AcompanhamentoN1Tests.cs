using ServiceDesk.Models;

internal static class AcompanhamentoN1Tests
{
    public static void Executar()
    {
        var inicio = DateTimeOffset.Parse("2026-11-16T13:00:00-03:00");
        var ticket = new AtendimentoMonitorado
        {
            Status = "open", EsperaInformadaPeloChatwoot = true, ClienteAguardaDesde = inicio,
            UltimaMensagemClienteEm = inicio.AddMinutes(3), UltimaMensagemPublicaEm = inicio.AddMinutes(3)
        };
        var espera = AcompanhamentoN1.DoTicket(ticket)!;
        Testar(espera is { Estado: EstadoN1.ClienteAguarda } && espera.Referencia == inicio,
            "mensagens adicionais não reiniciam a espera oficial");
        Testar(espera.Descrever(inicio.AddMinutes(8), 10, 2) is
            { Texto: "Aguardando resposta · há 8 min", Destaque: DestaqueN1.Aviso }, "aviso prévio baseado na espera");
        Testar(espera.Descrever(inicio.AddMinutes(10).AddSeconds(-1), 10, 0) is
            { Texto: "Aguardando resposta · há 9 min", Destaque: DestaqueN1.Normal }, "um segundo antes do prazo ainda está aguardando");
        Testar(espera.Descrever(inicio.AddMinutes(10), 10, 0) is
            { Texto: "Resposta em atraso · há 10 min", Destaque: DestaqueN1.PrazoAtingido }, "o texto muda no segundo exato do vencimento");
        Testar(espera.Descrever(inicio.AddMinutes(9), 1, 0).Destaque == DestaqueN1.Normal
            && espera.Descrever(inicio.AddMinutes(10), 15, 0).Destaque == DestaqueN1.Normal,
            "o limite respeita o mínimo de dez minutos e preferências maiores");

        ticket.ClienteAguardaDesde = null;
        ticket.UltimaMensagemPublicaEm = ticket.UltimaRespostaPublicaEm = inicio.AddMinutes(12);
        var retorno = AcompanhamentoN1.DoTicket(ticket)!;
        Testar(retorno is { Estado: EstadoN1.AcompanharRetorno }
            && retorno.Descrever(inicio.AddMinutes(13), 20, 2).Texto == "Respondido · há 1 min",
            "o relógio mostra o tempo desde a resposta pública do agente");
        Testar(retorno.Descrever(inicio.AddMinutes(22), 20, 2) is
            { Destaque: DestaqueN1.PrazoAtingido, Texto: "Respondido · há 10 min" },
            "o limite de acompanhamento muda o destaque sem trocar o relógio");

        ticket.UltimaMensagemPublicaEm = inicio.AddMinutes(13);
        Testar(AcompanhamentoN1.DoTicket(ticket) is null, "atividade de outro tipo não cria contagem de retorno");
        ticket.UltimaMensagemPublicaEm = ticket.UltimaMensagemClienteEm = inicio.AddMinutes(14);
        Testar(AcompanhamentoN1.DoTicket(ticket) is null, "campo de espera vazio e explícito prevalece");
        ticket.EsperaInformadaPeloChatwoot = false;
        Testar(AcompanhamentoN1.DoTicket(ticket) is { Estado: EstadoN1.ClienteAguarda },
            "instalações sem o campo de espera usam a última mensagem pública do cliente");
        var novaMensagem = new AtendimentoMonitorado
        {
            Status = "open", UltimaRespostaPublicaEm = ticket.UltimaRespostaPublicaEm,
            UltimaMensagemClienteEm = inicio.AddMinutes(16), UltimaMensagemPublicaEm = inicio.AddMinutes(16)
        };
        AcompanhamentoN1.PreservarEsperaObservada([novaMensagem], [ticket]);
        Testar(AcompanhamentoN1.DoTicket(novaMensagem)?.Referencia == inicio.AddMinutes(14),
            "mensagem adicional sem waiting_since mantém o início observado");
        novaMensagem.ClienteAguardaDesde = null;
        novaMensagem.UltimaRespostaPublicaEm = inicio.AddMinutes(15);
        AcompanhamentoN1.PreservarEsperaObservada([novaMensagem], [ticket]);
        Testar(AcompanhamentoN1.DoTicket(novaMensagem)?.Referencia == inicio.AddMinutes(16),
            "uma resposta pública entre duas esperas reinicia o relógio");

        ticket.Status = "snoozed";
        ticket.AdiadoAte = inicio.AddHours(2);
        var adiado = AcompanhamentoN1.DoTicket(ticket)!;
        Testar(adiado.Descrever(inicio, 10, 2).Texto == $"Adiado · Retoma em {ticket.AdiadoAte.Value.ToLocalTime():dd'/'MM 'às' HH:mm}",
            "adiamento mostra o horário local da retomada");
        Testar(adiado.Descrever(inicio.AddHours(2), 10, 2).Destaque == DestaqueN1.PrazoAtingido, "retomada vencida permanece visível até a sincronização");
        ticket.AdiadoAte = null;
        Testar(AcompanhamentoN1.DoTicket(ticket) is { Estado: EstadoN1.AdiadoAteResposta, Referencia: null },
            "adiamento até resposta não inventa horário");
        ticket.Status = "resolved";
        Testar(AcompanhamentoN1.DoTicket(ticket) is null, "resolvidos não têm acompanhamento ativo");
        Testar(AcompanhamentoN1.DescreverResolucao(inicio, inicio.AddHours(2).AddMinutes(3)) == "Resolvido · há 2 h 3 min"
            && AcompanhamentoN1.DescreverResolucao(null, inicio) == "Resolvido",
            "resolvidos só exibem o relógio quando a resolução é conhecida");
    }

    private static void Testar(bool condicao, string nome)
    {
        if (!condicao) throw new InvalidOperationException(nome);
        Console.WriteLine("OK: acompanhamento automático — " + nome);
    }
}
