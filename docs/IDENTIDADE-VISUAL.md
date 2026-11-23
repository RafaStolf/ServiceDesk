# Identidade visual do ServiceDesk

## Janela

- Use o ícone atual (`Assets/AppIcon.ico`) e exiba a versão instalada ao lado do nome ServiceDesk na barra de título do Windows.
- A faixa superior reúne as filas, **Métricas** e **Configurações**. A aba ativa permanece visível. Destinos que excedem a largura ficam em **Mais**. A disponibilidade de **Não atribuídos** segue as permissões do Chatwoot.
- Use controles e tipografia nativos do WinUI. A paleta escura usa fundo `#101722`, lateral `#1D2A3B`, superfícies `#172333` e destaque azul `#6DADFF`. O tema claro usa superfícies brancas e azul `#2166B0` para a seleção. Os recursos de contraste seguem o Windows.
- Use vermelho para dados ausentes, espera acima do limite, violações e atualização disponível. Use âmbar para adiamentos e SLA ativo com violações. Inclua texto ou nome acessível para explicar cada estado.

## Fila e cards

- **Situação de atendimentos** concentra as contagens de abertos e adiados da fila selecionada, inclusive ao filtrar a lista por status. Os títulos dos blocos exibem somente o nome do status.
- A grade usa até cinco colunas de acordo com a largura disponível e uma em janelas estreitas. Os cards mostram contato e interação empilhados, com as ações no canto superior direito. Grupos podem aparecer sem empresa.
- Ao completar o limite de espera do cliente, aplique uma superfície avermelhada a todo o card, em qualquer fila acessível que o exiba. Preserve a leitura do conteúdo e o foco visível nos temas claro, escuro e de contraste. Mostre o estado da espera e sua duração em texto. O destaque do card segue o limite mesmo com os alertas externos desligados.

## Painel de espera

- Posicione um único painel acima das outras janelas no canto e no monitor escolhidos em **Personalização → Painel flutuante**. O padrão usa o canto inferior direito do monitor da janela em uso ao abrir o painel. Preserve o foco no aplicativo em uso e deixe os cliques chegarem à janela atrás do painel.
- Em **Personalização → Painel flutuante**, permita escolher uma fonte instalada no Windows e ajustar a escala do card flutuante de 75% a 150%. O padrão é Segoe UI a 100%. A escala acompanha a densidade do monitor e mantém nome e tempo na mesma linha.
- Mostre o nome do contato e a duração da espera dos três tickets de **Meus tickets** que aguardam há mais tempo entre os que atingiram o limite. Quando houver mais tickets elegíveis, mostre a contagem dos demais.
- Mantenha o painel visível enquanto os dados da fila pessoal estiverem atualizados e houver uma espera elegível, inclusive com a janela principal na bandeja. O painel fica habilitado por padrão e pode ser desligado em **Alertas**.

## Configurações

- Organize as categorias no topo. Em **Personalização**, cada editor reúne campos, ordem, densidade e prévia. **Aplicativo** reúne inicialização, atualizações, autoria e repositório. Os botões **Cancelar** e **Salvar** ficam no rodapé.
- Em **Alertas**, mostre o limite de espera de 10 a 1.440 minutos, a opção de ativar os alertas, a ativação do painel e os controles das notificações do Windows, o som e o intervalo do lembrete. Identifique a opção **Nunca** para o lembrete.

## Métricas e SLA

- Mostre as datas e a base de tempo no mesmo painel. Dê destaque às médias e mostre quantos registros entram em cada cálculo.
- Use barras horizontais com origem comum, rótulos e contagens para os quatro estados oficiais do Chatwoot. Mostre a contagem sem política ou dados em texto. Os detalhes por ticket usam o mesmo rótulo do gráfico.
