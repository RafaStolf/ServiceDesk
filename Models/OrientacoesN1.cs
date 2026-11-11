namespace ServiceDesk.Models;

/// <summary>Referência operacional; não classifica conversas nem calcula SLA nativo.</summary>
public static class OrientacoesN1
{
    public const string Contexto = "Fluxo N1 · referência operacional. Os prazos abaixo seguem o fluxograma fornecido; não são metas de SLA do Chatwoot. A etapa não é inferida pelo texto ou pelo status do ticket.";
    public static IReadOnlyList<(string Titulo, string Descricao)> Etapas { get; } = new (string, string)[]
    {
        ("Entrada e distribuição", "Com técnico disponível, atribuição automática. Sem técnico disponível, avaliar urgência; se urgente, a coordenação revisa os atendimentos e libera um atendente ou a gestão decide qual atendimento adiar."),
        ("Análise inicial e IA", "Compreender o problema, o impacto e as informações já enviadas. Quando precisar consultar a IA, registrar a orientação em mensagem privada e incluir a etiqueta de IA quando aplicável. Se a IA não ajudar, documentar por e-mail para Robson."),
        ("1 · Técnico sabe dar sequência", "Se não virar problema, resolver o atendimento e aguardar retorno por 10 min; sem resposta, adiar por 30 min úteis. Na reabertura, cobrar retorno e adiar por 2 h úteis; persistindo a ausência, avisar o cliente e finalizar abrindo ticket. Se virar problema: testar em base controlada, registrar evidências em mensagem privada, abrir ticket, converter em problema, avisar o cliente e encerrar."),
        ("2 · Depende do desenvolvimento", "Registrar mensagem privada com resumo da análise e do que será enviado ao desenvolvimento; encaminhar evidências no grupo dedicado. Avisar o cliente e aguardar 10 min; sem retorno do desenvolvimento, adiar 30 min. Na reabertura, avaliar o retorno; se ausente, avisar o cliente e adiar mais 30 min. Quando houver retorno, responder ao cliente."),
        ("3 · Assunto criado pelo N2", "Revisar o ticket e o conteúdo enviado. Retornar ao proprietário do assunto por conversa."),
        ("4 · Aguarda retorno do cliente", "Aguardar 10 min; sem retorno, adiar 30 min. Na reabertura, cobrar retorno e aguardar 10 min; sem retorno, adiar por 2 h úteis. Persistindo a ausência, enviar mensagem de conclusão e encerrar."),
        ("5 · Depende de análise maior", "Se emergencial, registrar evidências em mensagem privada, criar ticket a partir da conversa sem avisar o cliente e atribuir pela fila do N2 usando a macro WN2. Se não emergencial, abrir ticket, avisar o cliente, encerrar e documentar em mensagem privada usando a macro WABERTURA; seguir o fluxo N2."),
    };
    public const string Limites = "Precedência definida: o fluxograma prevalece para esperas e encerramento por falta de retorno; não se impõe uma terceira tentativa por ligação nessa sequência. Somente os prazos explicitamente marcados como úteis são apresentados como úteis. Este painel orienta o analista; não envia mensagens, aplica macros, adia, atribui ou encerra atendimentos.";
}
