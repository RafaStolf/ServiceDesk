using ServiceDesk.Services;

internal static class AgendaNotificacoesAtrasoTests
{
    private static readonly DateTimeOffset Inicio = new(2026, 11, 17, 10, 0, 0, TimeSpan.Zero);

    internal static void Executar(Action<bool, string> verificar)
    {
        EntregaInicial(verificar);
        RetentativaPorEpisodio(verificar);
        RepeticaoConsolidada(verificar);
        RetentativaDoResumo(verificar);
        MudancaDeIntervalo(verificar);
        SuspensaoDeEntrega(verificar);
        RetornoDaSuspensaoDoWindows(verificar);
    }

    private static void EntregaInicial(Action<bool, string> verificar)
    {
        var agenda = new AgendaNotificacoesAtraso();
        HashSet<long> abertos = [1, 2];

        var primeiro = agenda.Planejar([1, 2], abertos, Inicio, 0, true);
        verificar(primeiro?.Episodio == 1, "Windows prioriza o ticket com espera mais antiga");
        agenda.RegistrarResultado(primeiro!.Value, Inicio, null);
        verificar(agenda.Planejar([1, 2], abertos, Inicio.AddSeconds(7), 0, true) is null,
            "Windows mantém oito segundos entre os envios iniciais");
        var segundo = agenda.Planejar([1, 2], abertos, Inicio.AddSeconds(8), 0, true);
        verificar(segundo?.Episodio == 2, "Windows envia o próximo ticket após o intervalo global");
        agenda.RegistrarResultado(segundo!.Value, Inicio.AddSeconds(8), null);

        agenda.Planejar([], abertos, Inicio.AddSeconds(9), 0, true);
        verificar(agenda.Planejar([1, 2], abertos, Inicio.AddSeconds(16), 0, true) is null,
            "baixar o limite de atraso não repete o aviso inicial de episódios ainda abertos");
        verificar(agenda.Planejar([3], new HashSet<long> { 3 }, Inicio.AddSeconds(17), 0, true)?.Episodio == 3,
            "um novo episódio pode gerar aviso inicial");
    }

    private static void RetentativaPorEpisodio(Action<bool, string> verificar)
    {
        var agenda = new AgendaNotificacoesAtraso();
        HashSet<long> abertos = [1, 2];

        var primeiro = agenda.Planejar([1, 2], abertos, Inicio, 0, true)!.Value;
        agenda.RegistrarResultado(primeiro, Inicio, "Falha do episódio 1");
        var segundo = agenda.Planejar([1, 2], abertos, Inicio.AddSeconds(8), 0, true);
        verificar(segundo?.Episodio == 2, "a retentativa de um ticket não bloqueia o próximo episódio");
        agenda.RegistrarResultado(segundo!.Value, Inicio.AddSeconds(8), null);
        verificar(agenda.ObterErro([1, 2]) == "Falha do episódio 1",
            "o diagnóstico mantém a falha do episódio que aguarda nova tentativa");

        verificar(agenda.Planejar([1, 2], abertos, Inicio.AddSeconds(299), 0, true) is null,
            "o envio inicial com falha aguarda cinco minutos");
        var retentativa = agenda.Planejar([1, 2], abertos, Inicio.AddMinutes(5), 0, true);
        verificar(retentativa?.Episodio == 1, "o envio inicial é tentado após cinco minutos");
        agenda.RegistrarResultado(retentativa!.Value, Inicio.AddMinutes(5), null);
        verificar(agenda.ObterErro([1, 2]) is null, "o diagnóstico desaparece após a retentativa bem-sucedida");
    }

    private static void RepeticaoConsolidada(Action<bool, string> verificar)
    {
        var agenda = new AgendaNotificacoesAtraso();
        HashSet<long> abertos = [1, 2, 3];
        var primeiro = agenda.Planejar([1], abertos, Inicio, 1, true)!.Value;
        agenda.RegistrarResultado(primeiro, Inicio, null);
        agenda.Planejar([1], abertos, Inicio.AddSeconds(20), 1, true);
        agenda.Planejar([1], abertos, Inicio.AddSeconds(40), 1, true);
        verificar(agenda.Planejar([1], abertos, Inicio.AddSeconds(54), 1, true) is null,
            "o primeiro resumo aguarda o intervalo completo");

        var segundo = agenda.Planejar([1, 2], abertos, Inicio.AddSeconds(55), 1, true)!.Value;
        agenda.RegistrarResultado(segundo, Inicio.AddSeconds(55), null);
        verificar(agenda.Planejar([1, 2], abertos, Inicio.AddMinutes(1), 1, true) is null,
            "o resumo também respeita o espaçamento de oito segundos");
        var resumo = agenda.Planejar([1, 2, 3], abertos, Inicio.AddSeconds(63), 1, true);
        verificar(resumo is { Episodio: null }, "o resumo vencido é enviado uma vez antes dos próximos avisos iniciais");
        agenda.RegistrarResultado(resumo!.Value, Inicio.AddSeconds(63), null);
        var terceiro = agenda.Planejar([1, 2, 3], abertos, Inicio.AddSeconds(71), 1, true);
        verificar(terceiro?.Episodio == 3, "o resumo preserva o aviso inicial pendente");
        agenda.RegistrarResultado(terceiro!.Value, Inicio.AddSeconds(71), null);

        agenda.Planejar([1, 2, 3], abertos, Inicio.AddSeconds(90), 1, true);
        agenda.Planejar([1, 2, 3], abertos, Inicio.AddSeconds(110), 1, true);
        verificar(agenda.Planejar([1, 2, 3], abertos, Inicio.AddSeconds(122), 1, true) is null,
            "o próximo prazo conta desde o envio real do resumo");
        verificar(agenda.Planejar([1, 2, 3], abertos, Inicio.AddSeconds(123), 1, true) is { Episodio: null },
            "o próximo intervalo gera um único resumo mesmo com vários tickets");
    }

    private static void RetentativaDoResumo(Action<bool, string> verificar)
    {
        var agenda = new AgendaNotificacoesAtraso();
        HashSet<long> abertos = [1, 2];
        var primeiro = agenda.Planejar([1], abertos, Inicio, 1, true)!.Value;
        agenda.RegistrarResultado(primeiro, Inicio, null);
        agenda.Planejar([1], abertos, Inicio.AddSeconds(20), 1, true);
        agenda.Planejar([1], abertos, Inicio.AddSeconds(40), 1, true);
        var resumo = agenda.Planejar([1], abertos, Inicio.AddMinutes(1), 1, true)!.Value;
        agenda.RegistrarResultado(resumo, Inicio.AddMinutes(1), "Falha do resumo");

        var segundo = agenda.Planejar([1, 2], abertos, Inicio.AddSeconds(68), 1, true);
        verificar(segundo?.Episodio == 2, "a retentativa do resumo não bloqueia novos avisos iniciais");
        agenda.RegistrarResultado(segundo!.Value, Inicio.AddSeconds(68), null);
        verificar(agenda.ObterErro([1, 2]) == "Falha do resumo", "a falha do resumo aparece no diagnóstico");
        var interrompeuEspera = false;
        for (var segundos = 90; segundos < 360; segundos += 20)
            interrompeuEspera |= agenda.Planejar([1, 2], abertos, Inicio.AddSeconds(segundos), 1, true) is not null;
        verificar(!interrompeuEspera, "o resumo com falha aguarda o prazo próprio de retentativa");
        verificar(agenda.Planejar([1, 2], abertos, Inicio.AddMinutes(6), 1, true) is { Episodio: null },
            "a retentativa do resumo ocorre cinco minutos após a falha");
    }

    private static void MudancaDeIntervalo(Action<bool, string> verificar)
    {
        var agenda = new AgendaNotificacoesAtraso();
        HashSet<long> abertos = [1];
        var primeiro = agenda.Planejar([1], abertos, Inicio, 1, true)!.Value;
        agenda.RegistrarResultado(primeiro, Inicio, null);
        agenda.Planejar([1], abertos, Inicio.AddSeconds(20), 1, true);
        agenda.Planejar([1], abertos, Inicio.AddSeconds(40), 0, true);
        verificar(agenda.Planejar([1], abertos, Inicio.AddSeconds(60), 0, true) is null,
            "intervalo zero cancela o resumo já agendado");

        agenda.Planejar([1], abertos, Inicio.AddSeconds(80), 2, true);
        for (var segundos = 100; segundos < 200; segundos += 20)
            agenda.Planejar([1], abertos, Inicio.AddSeconds(segundos), 2, true);
        verificar(agenda.Planejar([1], abertos, Inicio.AddSeconds(200), 2, true) is { Episodio: null },
            "a reativação da repetição usa o novo intervalo a partir da configuração");
    }

    private static void SuspensaoDeEntrega(Action<bool, string> verificar)
    {
        var agenda = new AgendaNotificacoesAtraso();
        HashSet<long> abertos = [1, 2];
        var primeiro = agenda.Planejar([1], abertos, Inicio, 1, true)!.Value;
        agenda.RegistrarResultado(primeiro, Inicio, null);
        agenda.Planejar([1], abertos, Inicio.AddSeconds(20), 1, true);
        agenda.Planejar([1], abertos, Inicio.AddSeconds(40), 1, true);
        verificar(agenda.Planejar([1, 2], abertos, Inicio.AddSeconds(60), 1, false) is null,
            "a suspensão impede avisos iniciais e consolidados");
        var segundo = agenda.Planejar([1, 2], abertos, Inicio.AddSeconds(65), 1, true);
        verificar(segundo?.Episodio == 2, "a retomada preserva a entrega inicial que já ocorreu");
        agenda.RegistrarResultado(segundo!.Value, Inicio.AddSeconds(65), null);
        agenda.Planejar([1, 2], abertos, Inicio.AddSeconds(85), 1, true);
        agenda.Planejar([1, 2], abertos, Inicio.AddSeconds(105), 1, true);
        verificar(agenda.Planejar([1, 2], abertos, Inicio.AddSeconds(124), 1, true) is null,
            "a suspensão descarta o prazo anterior do resumo");
        verificar(agenda.Planejar([1, 2], abertos, Inicio.AddSeconds(125), 1, true) is { Episodio: null },
            "a retomada volta a contar um intervalo completo para o resumo");
    }

    private static void RetornoDaSuspensaoDoWindows(Action<bool, string> verificar)
    {
        var agenda = new AgendaNotificacoesAtraso();
        HashSet<long> abertos = [1];
        var primeiro = agenda.Planejar([1], abertos, Inicio, 1, true)!.Value;
        agenda.RegistrarResultado(primeiro, Inicio, null);
        agenda.Planejar([1], abertos, Inicio.AddSeconds(20), 1, true);
        verificar(agenda.Planejar([1], abertos, Inicio.AddHours(1), 1, true) is null,
            "a volta após uma pausa do Windows não acumula resumos vencidos");

        agenda.Planejar([1], abertos, Inicio.AddHours(1).AddSeconds(20), 1, true);
        agenda.Planejar([1], abertos, Inicio.AddHours(1).AddSeconds(40), 1, true);
        verificar(agenda.Planejar([1], abertos, Inicio.AddHours(1).AddMinutes(1), 1, true) is { Episodio: null },
            "a agenda de resumos volta a funcionar após a pausa do Windows");
    }
}
