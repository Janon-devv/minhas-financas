# Decisões do projeto

Registro do que foi decidido ao iniciar o Minhas Finanças, em 13/10/2026, e por quê.
Serve para ninguém (inclusive eu mesmo numa sessão futura) precisar readivinhar isso depois.

## 1. Os dois documentos de contexto descreviam produtos diferentes

Chegaram dois documentos: `CONTEXTO.md` (Minhas Finanças) e
`referencia/CONTEXTO_PROGRAMA_FINANCEIRO_GPT.md` (Minhas Finanças). Não eram duas versões do mesmo
projeto, eram modelos incompatíveis:

| | Minhas Finanças | Documento do GPT |
|---|---|---|
| Tipos de lançamento | 4: Entrada, Saída, Cartão, Economia | 7: Entrada, Conta, Diário, Cartão, Reserva, Resgate, Estorno |
| Situação | não existe | Previsto / Realizado / Cancelado |
| Partida do saldo | âncora (saldo, data) + recalibrar | saldo inicial + data de acompanhamento + dia encerrado |
| Diário | previsto + real por data, 0 diferente de ausente | padrão + complemento `max(0, orçamento − lançado)` |
| Cartão | calcula o vencimento da fatura (R1) | parcelas lançadas na mão |
| Recorrência | nativa (`vezes` = 1 / N / sem fim) | manual, fora do MVP |

**Decisão do dono:** Minhas Finanças como espinha, absorvendo do documento do GPT o que não quebra os
fixtures. O peso da decisão foi que o Radar tem um cenário de referência conferido por duas
implementações independentes (811 dias, 12 meses), e trocar o modelo jogaria essa validação fora.

### O que foi absorvido do documento do GPT

- **Metas com objetivo, prazo, progresso e aporte sugerido** (seção 9 dele), montadas em cima do tipo
  Economia, que no Radar era só um acumulado sem nome. Fórmulas de falta, meses restantes e aporte
  seguem o documento dele.
- **Simulador com entrada e juros compostos** (seção 11 dele). Com taxa zero e sem entrada, o
  comportamento é exatamente o do Radar: parcelas iguais.
- **Aviso de impacto parcial** quando parcelas caem depois do fim do horizonte, em vez de deixar
  parecer que a compra cabe.
- **Backup JSON com `schemaVersion`**, validação antes de substituir e backup automático do estado
  anterior (seção 16 dele).
- **Avisos de cadastro** que não travam o cálculo, como lançamento de cartão sem cartão válido.

### O que ficou de fora

Previsto/Realizado/Cancelado, os 7 tipos, estorno, teto de consumo e complemento diário. No modelo do
Radar, o acerto com a realidade acontece pela recalibração da âncora e pelo diário real, não por
confirmar lançamento a lançamento. Misturar os dois mecanismos dobraria a complexidade e invalidaria
os fixtures.

## 2. Stack

.NET 10 + Blazor Server + SQLite, escolhidos pelo dono entre as opções apresentadas, alinhados ao
ecossistema C#/.NET/SQL dele.

O `CONTEXTO.md` sugeria .NET 8. A máquina tem o SDK **10.0.401**, que é o LTS atual, então o projeto
usa `net10.0`. O próprio documento do GPT recomendava não fixar versões a partir do texto e sim
conferir na hora da inicialização.

Detalhes que valem registro:

- `dotnet new sln` no .NET 10 gera `MinhasFinancas.slnx`, o formato novo, não `MinhasFinancas.slnx`. Quem procurar a
  raiz do repositório por `MinhasFinancas.slnx` não acha (foi assim que os testes dourados falharam na
  primeira execução; hoje eles procuram o próprio arquivo de fixture).
- Aplicações de arquivo único (`dotnet run arquivo.cs`) vêm com configuração de AOT que quebra o EF
  Core. Os utilitários em `tools/` desligam isso com `#:property PublishAot=false`.
- SQLite não aceita `ORDER BY` em `DateTimeOffset`. O histórico de âncoras é ordenado por `Id`, que é
  sequencial e dá no mesmo.

## 3. Resto de centavos nas parcelas sem juros

Os dois documentos discordavam: o Radar manda jogar o resto na **primeira** parcela, o do GPT na
última (exemplo D dele: 33,33 / 33,33 / 33,34).

Seguimos o Radar: R$ 100,00 em 3x dá **33,34 / 33,33 / 33,33**. O motivo é coerência com o resto do
método, que é conservador: o dinheiro sai mais cedo, não mais tarde. Com juros não há escolha, a
prestação é fixa e a última parcela liquida o resíduo.

O fixture tem só uma parcela, então ele não decide essa questão.

## 4. Cartão sem cartão válido

Um lançamento do tipo Cartão sem cartão cadastrado não tem como calcular o vencimento da fatura.
Em vez de ignorar o lançamento, o motor o conta **na data da compra** e emite um aviso visível.

Ignorar deixaria o saldo projetado mais otimista do que a realidade, que é exatamente o erro que o
método existe para evitar. Melhor o dinheiro aparecer saindo cedo demais do que sumir da projeção.

## 5. "Já guardado" e o saldo inicial das metas

A configuração tem um `ja_guardado` global (vem do R7) e cada meta tem o seu próprio saldo inicial.
Os dois não precisam bater: uma meta pode ter sido criada depois, ou cobrir só parte do que você
guarda. A tela de Metas diz isso em uma linha para não parecer inconsistência.

## 6. Nome do projeto

O nome definitivo é **Minhas Finanças | Raul Janon**. O documento de especificação usa Radar do
Saldo, que era o nome de trabalho, e as citações a ele nos textos acima foram mantidas de propósito.

A troca atingiu projetos, namespaces, pasta de dados e textos de interface:

| Antes | Depois |
|---|---|
| `Radar.Domain`, `Radar.Infra`, `Radar.Web` | `MinhasFinancas.Domain`, `.Infra`, `.Web` |
| `RadarDbContext` | `FinancasDbContext` |
| `%LOCALAPPDATA%\RadarDoSaldo\radar.db` | `%LOCALAPPDATA%\MinhasFinancas\financas.db` |
| `Radar:CaminhoBanco` | `MinhasFinancas:CaminhoBanco` |

O banco antigo foi copiado para o caminho novo, preservando a configuração que já existia, e a pasta
antiga continua no lugar como rede de segurança.

## 7. Interface

- **Mobile first, mas com layout próprio de PC.** O check-in diário acontece no iPhone e é de
  segundos, então o celular define a estrutura. A partir de 900px a barra de baixo vira coluna
  lateral, os indicadores passam de dois para quatro por linha e os blocos se dividem em duas
  colunas. É a mesma aplicação, sem código duplicado: só CSS e alguns agrupamentos de blocos.
- **HTTP na rede local**, não HTTPS. Certificado autoassinado faria o Safari reclamar em toda
  abertura. O custo é não ter service worker, ou seja, não ter modo offline. Instalar na tela de
  início funciona assim mesmo, via manifest e meta tags da Apple.
- **Blazor Server com prerender ligado.** O conteúdo chega pronto no primeiro HTML, antes de o
  WebSocket conectar: no celular isso é a diferença entre abrir e ver o saldo ou abrir e ver tela
  vazia. O custo é carregar os dados duas vezes por navegação, o que é irrelevante com algumas
  centenas de linhas.
- **Gráfico em SVG puro**, sem biblioteca. São duas polilinhas e duas linhas de referência; não
  justifica uma dependência.
- **Ícones em SVG inline** em vez de símbolos de fonte ou emoji, que renderizam diferente em cada
  aparelho. O ícone do app é gerado por `tools/gerar-icones.cs`, sem dependência de biblioteca de
  imagem.

## 8. Testes

O dono pediu o mínimo: "eu testo". Ficaram 73 testes, concentrados onde um erro passa despercebido:

- 6 dourados, comparando o motor com `docs/fixtures` dia a dia e mês a mês, em centavos exatos.
- As regras R12 a R17, que nasceram depois dos fixtures e portanto não têm cenário dourado:
  situação da ocorrência, carteiras e a aritmética da fatura (total informado contra soma dos
  itens, detalhamento que não soma duas vezes, pagamento parcial, parcelamento e juros do atraso).
- Casos de borda das regras R1 a R11 que o cenário não cobre: compra no próprio dia do fechamento,
  vencimento 31 em fevereiro de ano bissexto, dia 31 recorrente, diário real zero contra ausência de
  registro, empate de mínimo, âncora no futuro, recalibração.
- O exemplo aritmético público do método (4.500 / 2.876,60 / 105 / 500 / 0 → performance 1.018,40,
  custo de vida 2.981,60, economizado 11%, diário médio 4,38 em 24 dias).
- Leitura de valores digitados: `50`, `50,00`, `1.234,56`, `1234.56`, `R$ 90`. Errar aqui salva um
  valor cem vezes maior sem ninguém notar na hora.

Não há teste de interface. O que foi conferido na interface está no relatório de entrega.

## 9. Fixtures: duas convenções diferentes na mesma pasta

Descoberto ao implementar, vale repetir porque custa tempo:

- `esperado_calendario.csv`: `saidas_centavos` é a saída **total** do dia, com cartão e economias
  dentro. As colunas próprias são detalhamento.
- `esperado_mensal.csv`: `saidas_e_cartao_centavos` **exclui** economias.

Quem comparar os dois com a mesma fórmula vai ver erro onde não tem.

## 10. A conferência com o banco estava comparando unidades diferentes

Encontrado no primeiro uso real do app, não nos testes: os testes cobrem o motor, e o defeito
estava na costura entre o motor e a tela.

`ResumoAgora.SaldoHojeCentavos` é o saldo do **fim** do dia, já com o diário descontado.
`RecalibrarAsync` gravava esse número em `Configuracao.SaldoAncoraCentavos`, que por definição
é o saldo do **início** do dia. Comparar um com o outro e gravar de volta não converge: a cada
recalibração sumia exatamente o diário do dia. Com diário de R$ 4,96, conferir R$ 38,27 fazia o
app passar a dizer R$ 33,31 e apontar uma diferença nova de R$ 4,96, para sempre.

A origem era a R7 da especificação, que mandava gravar `data = hoje`. A seção 7 já tinha a
pista certa ("ou recalibre com data de amanhã"), mas como conselho ao usuário, não como regra.

**Decisão:** o número que o usuário digita é o que o banco mostra **agora**, com os gastos do
dia já dentro dele. Logo ele é o saldo do início de **amanhã**, e a nova âncora é `hoje + 1`.
A R7 foi corrigida junto com o código.

Consequência que precisou de tratamento na borda: com a âncora em amanhã, hoje passa a ser
anterior à âncora e `SaldoHojeCentavos` fica nulo. A tela de conferência sumiria logo após
conferir. `Ajustes` resolve isso lendo a própria âncora nesse caso, que é por definição o saldo
do fim de hoje.

Alternativa descartada: perguntar a cada conferência se o número já inclui os gastos do dia.
Mais exato, um toque a mais toda semana, e o caso "confiro antes de gastar qualquer coisa" é
raro o bastante para não pagar esse preço.


### Desdobramento: o dia da conferência ficava sem saldo

Ancorar em hoje + 1 fechou a aritmética, mas apagou o número principal da tela Hoje pelo resto do
dia em que o dono conferia: a projeção começa amanhã, logo "saldo de hoje" não existia mais.

Isso era conclusão errada tirada de premissa certa. O saldo âncora é o saldo do **início** de
amanhã, que é exatamente o saldo do **fim** de hoje: é o mesmo instante visto de dois lados. Então
hoje tem saldo, e é justamente o número que o dono acabou de ler no banco.

A tela também passa a dizer de onde veio: "Conferido com o banco hoje. Já inclui o que saiu hoje",
em vez de "Projetado para o fim do dia, já contando o diário", que seria falso duas vezes.

Com a âncora mais de um dia à frente continua sem saldo de hoje, porque aí seria preciso projetar
para trás, e projetar para trás é inventar.

## 11. O diário previsto virou opcional

O CONTEXTO tratava o diário previsto como fato: todo dia sem registro desconta o previsto, e a
R4 avisava que dia passado sem registro usa o previsto, "não é zero". A ideia era não prometer
dinheiro que não existe.

Só que ela assume um padrão de gasto que nem todo mundo tem. Quem faz marmita e compra de
semana gasta em blocos, não um pouco por dia: o gasto real já está cadastrado como conta, e o
diário descontava de novo, todo dia, um valor que não sai. Em dois anos de horizonte isso vira
uma montanha inventada, e foi o que fez a primeira projeção do app aparecer em -R$ 18.261,73.

**Decisão:** `diario_previsto_ativo`, ligado por padrão. Desligado, dia sem registro vale zero e
o valor do previsto continua gravado, só como referência. O registro do dia continua mandando
nos dois casos, inclusive o registro de valor zero: desligar o previsto não apaga gasto real.

Padrão `true` nos dois lugares, na classe e na coluna do banco (`HasDefaultValue(true)`). Com o
padrão `false` que o EF gera sozinho, a atualização desligaria o diário de quem já usava o app,
calada, e todo saldo projetado subiria de um dia para o outro sem ninguém ter pedido.

Alternativa descartada: deixar o dono zerar o valor do previsto quando não quisesse contar.
Funciona para a conta, mas perde a informação: ele quer manter o valor de referência à mão e
poder ligar de novo sem ter que lembrar quanto era.

## 12. O primeiro acesso virou guia, e não uma lista de links

A tela final do onboarding mostrava três links ("Cadastre o que entra", "as contas fixas", "seus
cartões") que jogavam o usuário na aplicação inteira liberada. Quem clicava no primeiro saía do
primeiro acesso e perdia os outros dois: a sequência existia no texto, mas não no programa.

**Decisão:** as três etapas passam a acontecer dentro do próprio guia, com formulário embutido,
"Passo N de 6", confirmação do que foi salvo a cada item e "Pular esta etapa" em todas. A tela
final lista o que ficou feito e o que ficou pulado, e só então leva para a tela Hoje.

Quem sai no meio não recomeça do zero: com os números já salvos, `/comecar` volta direto para a
etapa de cadastro, não para a primeira pergunta.


## 13. Pago e não pago é propriedade da ocorrência, não do lançamento

O dono relatou o aluguel atrasado e pediu um sistema de conta paga e não paga. A tentação é um
campo `Pago` no lançamento, e ele estaria errado: o lançamento é a regra ("aluguel todo dia 5"),
que gera ocorrências para sempre. Pagar o aluguel de outubro não diz nada sobre o de novembro.

Daí `SituacaoOcorrencia`, com chave `(lancamento_id, data_prevista)` e registro **só quando a
ocorrência foge do padrão**. Sem registro, tudo se comporta como antes: é o elemento neutro que
mantém os testes dourados passando sem tocar nos fixtures.

**O problema de verdade não era mostrar um selo de "pago".** Era que uma conta vencida antes da
âncora simplesmente não existe para a projeção: a R3 exclui ocorrências anteriores à âncora
porque o saldo de partida veio do extrato e já as contabilizou. Quando a conta **não** foi paga,
essa premissa cai: o dinheiro ainda está lá, a saída ainda vai acontecer, e o app mostra folga
que não existe. Era o caso do dono naquele dia.

**Decisão:** o app não adivinha. O extrato não diz quais contas saíram dele, então a tela Hoje
lista o que venceu nos últimos 45 dias e pergunta. Respondido "ainda não", a saída é trazida
para o primeiro dia projetado em vez de desaparecer. Respondido "paguei", some de vez se saiu
antes da âncora, ou conta no dia em que saiu de verdade, se saiu depois.

De carona, a mesma entidade resolve conta que varia: `valor_centavos` na ocorrência vale só
naquele mês, e os seguintes continuam no previsto. Luz e água deixam de precisar de edição do
lançamento todo mês.

Alternativa descartada: marcar como pago movendo a data do lançamento. Perderia o histórico e
mudaria todas as ocorrências futuras junto.

## 14. Cor de tag é nome de paleta, não hexadecimal

O dono pediu cores diferentes por tag. Campo de cor livre é o caminho curto e quebra o contraste
na primeira escolha: o texto da pílula é escuro, e qualquer fundo escuro reprova os 4,5:1 que o
projeto trata como regra que não regride.

**Decisão:** oito nomes fixos (rosa, laranja, âmbar, verde, turquesa, azul, índigo, roxo), cada
um com par de fundo e texto no `app.css`, medidos entre 6,3:1 e 8,0:1. A cor do texto serve de
preenchimento da barra do Resumo, com no mínimo 7:1 sobre o branco. O nome da tag continua
escrito dentro da pílula, então a cor nunca é a única pista.


## 15. Repetição semanal existe por causa do valor que varia, não apesar dele

O dono faz marmita e compra de mercado uma vez por semana. Cadastrar isso como conta mensal erra
duas vezes: a data, porque o dinheiro sai quatro ou cinco vezes no mês e não uma, e o valor,
porque ele muda a cada compra.

A primeira metade se resolve com `Periodicidade = Semanal`, de 7 em 7 dias a partir da data
original. A segunda **não se resolve no lançamento**, e foi o próprio dono quem apontou: "o valor
varia, não é fixo". Por isso as duas coisas entraram juntas. O lançamento guarda o valor que
costuma dar, e a cada semana a ocorrência é confirmada com o valor de verdade, que fica gravado
só naquela ocorrência (R12).

Semanal não vale para `Cartao`: a data efetiva de um lançamento de cartão é o vencimento da
fatura, e quatro compras na mesma semana caem na **mesma** fatura. Repetir de 7 em 7 dias ali
inventaria vencimentos que não existem. A `Validacao` recusa, em vez de deixar o motor produzir
um número errado em silêncio.

## 16. "Não teve" é um terceiro estado, não um pagamento de zero

Academia trancada no mês, semana sem compra. Marcar como paga de R$ 0,00 confundiria duas coisas
diferentes na hora de olhar para trás: dinheiro que saiu valendo zero contra evento que não
aconteceu. `Pulada` é um campo próprio, a ocorrência some da projeção e da lista, e o lançamento
continua valendo nos outros períodos.

## 17. "Livre depois das contas do mês"

O saldo grande é o do fim de hoje: ele ainda não viu as contas que vencem dia 20, 25, 30. Para
quem tem pouco na conta, esse número sozinho convida a gastar o que já está comprometido.

O indicador novo subtrai dele as saídas que **ainda vão vencer** neste mês e não foram pagas. As
vencidas que o dono marcou em aberto ficam de fora da subtração de propósito: elas já foram
trazidas para dentro da projeção pela R12, e contá-las de novo descontaria duas vezes o mesmo
dinheiro. É o mesmo cuidado da seção 10, agora no outro sentido.


## 18. A conta corrente não virou uma linha da tabela de carteiras

O plano previa uma entidade `Carteira` com um sinalizador `Principal`, e a migração semeando uma
linha "Banco" a partir da `Configuracao`. Na hora de escrever, isso criava duas verdades para o
mesmo número: a conferência com o extrato (R7) grava em `Configuracao`, o onboarding também, e a
tela de carteiras gravaria na linha. Qual das duas vale passaria a depender de qual foi salva por
último, e o erro só apareceria como um saldo estranho semanas depois.

**Decisão:** a tabela guarda só as carteiras **além** da conta corrente. A conta corrente é a
própria `Configuracao`, sintetizada como carteira de id 0 na hora de projetar. Não há seed, não
há migração de dados, e `carteira_id = null` num lançamento antigo já significa exatamente o que
precisa significar.

Isso também simplificou o motor: em vez de arrays de duas dimensões e um acumulador por carteira,
o mesmo núcleo roda uma vez por carteira, com a configuração equivalente dela. Carteira sem
diário projeta com a contagem desligada, que é a R4 que já existia desde a seção 11.

**Projeções independentes, de propósito.** Nada soma os saldos das carteiras num número único.
R$ 190 de vale-alimentação não ajudam a pagar um aluguel de R$ 1.500, e um total de R$ 6.154
esconderia exatamente o aperto que o app existe para mostrar.

## 19. A fatura de cartão aceita o total, os itens, ou os dois

A R1 já fazia a parte difícil: cada compra no cartão sai na data de vencimento da fatura dela, e
compra parcelada sai uma parcela por ciclo. O que faltava era o caminho de quem **não** cadastra
compra por compra, que é todo mundo depois da segunda semana. Olhar o aplicativo do banco e
digitar um número é uma rotina que se mantém; transcrever quarenta linhas não é.

O problema é que os dois caminhos juntos contam duas vezes. Se o total informado é R$ 1.200 e as
compras lançadas somam R$ 890, somar tudo dá R$ 2.090 de dívida que não existe. A especificação já
proibia isso na seção 7, mas proibir não resolve: o dono vai querer ver o que compôs a fatura.

**Decisão:** cada item de cartão carrega um papel. `Detalhamento` só descreve o que já está dentro
do total informado e não soma; `NovoLancamento` entra além dele. Sem total informado, os itens são
a única fonte e todos contam, porque não existe total para eles estarem dentro.

O padrão tinha que ser `NovoLancamento`: todo lançamento de Cartão já gravado soma na projeção
hoje, e qualquer outro padrão mudaria o significado de dado antigo sem avisar.

**A conta fica aberta na tela.** Informado, acrescentado, detalhado, total efetivo, pago e falta
pagar, cada linha com a sua quantidade de itens. Mostrar só o resultado seria pedir confiança num
número que o dono não consegue reconstruir, e um total errado sem decomposição visível vira
desconfiança no app inteiro.

**Pagamento parcial é a regra.** `pago_centavos` acumula, e o que já saiu sai no dia em que saiu,
não no vencimento. A parcela que falta continua marcada no vencimento, porque é quando ela vai
sair. Enquanto nada foi pago, a projeção assume pagamento integral: é a hipótese conservadora da
seção 4.

**Juros só com taxa informada.** Zero é o padrão e significa "não sei", não "não tem". Atraso de
40 dias conta 1 mês cheio, os juros incidem sobre o que sobrou e não sobre o total, e o
arredondamento acontece uma vez no fim: arredondar mês a mês acumularia erro sempre no sentido
favorável ao app, que é justamente o sentido errado.

**Fatura vencida antes da âncora não desaparece.** A R3 exclui o passado porque o saldo do extrato
já o reflete, mas uma fatura não paga não está no extrato: o dinheiro ainda está na conta. Então
ela vem para o primeiro dia projetado, com os juros do atraso, pelo mesmo motivo da seção 13.

**Ciclo sem fatura cadastrada não mudou nada** (R16). É o que mantém os seis dourados intactos: o
cenário dos fixtures não tem fatura nenhuma, logo os 811 dias saem com os mesmos centavos.

## 20. Ganho variável é registro do passado, não previsão do futuro

Renda de aplicativo de corrida muda todo mês. Isso não cabe em `Lancamento`, que existe para o
que se repete com valor conhecido, e cadastrar um lançamento novo a cada mês é rotina que ninguém
mantém depois da segunda vez. O dono foi direto ao ponto: não queria mais uma categoria, queria
**um campo para preencher todo mês**.

A pergunta difícil não era onde guardar, era o que o número significa. Se o dinheiro do Uber já
caiu na conta, ele já está dentro do saldo conferido com o banco, e somá-lo na projeção mostraria
dinheiro que não existe. É a mesma armadilha da seção 13, invertida: lá uma conta não paga sumia
do saldo, aqui um ganho já recebido apareceria duas vezes.

**Decisão do dono, entre três opções apresentadas:** é sempre registro do que **já caiu**. Conta
nos ganhos do mês (R8) e monta o histórico, mas nunca mexe no saldo projetado. Em troca, não serve
para dizer "espero fazer R$ 1.000 em novembro", e foi escolhido sabendo disso.

**Mês em branco vale zero, e o app não chuta média.** Também foi escolha explícita, sobre a
alternativa de projetar a média dos meses preenchidos com um liga/desliga. O preço é conhecido: o
"menor saldo à frente" ignora um ganho que quase sempre vem, e fica mais feio que a realidade. O
preço do contrário seria mostrar dinheiro que pode não vir, e a seção 4 já tinha decidido de que
lado errar.

Zero digitado e mês sem registro são coisas diferentes: "não rendeu nada" contra "ainda não
respondi". Por isso apagar o registro é uma ação própria, e a média de referência ignora os meses
em branco. É a mesma distinção do diário real, que também separa 0 de ausência.

A tela mostra a composição, não só o total: **Entradas R$ 5.447,00 / sendo ganho variável
R$ 1.247,00**. O resto das entradas é projeção e essa parte é número digitado. Somar sem dizer
deixaria o dono sem como reconstruir o número de cima, que é exatamente o que a seção 19 evitou
na fatura.

## 21. "Plano" virou "Lançamentos", e virou a tela única de lançar

O dono disse que o nome não fazia sentido na tela onde se lança as coisas, e que ali deveria
juntar tudo que se lança. Ele estava certo duas vezes.

"Plano de Contas" é o nome do **conceito** herdado da planilha, a lista de lançamentos
programados. Como nome de tela, ele descrevia só uma das coisas que a tela faz, e não dizia o
verbo que o dono usa: lançar.

Pior: fatura de cartão e ganho variável também são coisas que se lançam, e estavam em telas
separadas. Isso obrigava a lembrar em qual tela cada número é preenchido, que é exatamente o tipo
de atrito que faz uma rotina ser abandonada.

**Decisão:** uma tela só, chamada Lançamentos, com três seções: o que entra e o que sai, ganho
variável, faturas de cartão. A tela de faturas deixou de existir como tela e virou um bloco em
`Components/Compartilhado`, que recebe os dados já carregados pela tela e avisa quando algo muda.
Carregar de novo dentro do bloco daria duas projeções por visita e deixaria as listas
desencontradas depois de salvar.

**As rotas antigas continuam.** `/plano` e `/faturas` abrem a mesma tela, porque o app vive num
atalho salvo na tela do iPhone e trocar o endereço quebraria o atalho.

**Um índice no topo.** Com três seções empilhadas, o campo do ganho variável ficava soterrado pela
lista de lançamentos, e campo que ninguém acha é campo que ninguém preenche. O índice leva a cada
seção. O caminho vai inteiro no `href` de propósito: com o `<base href="/">` do Blazor, um href só
com fragmento é resolvido contra a base e joga o dono na tela Hoje.

## 22. Refatoração visual: menos texto, um número por vez

Em 15/10/2026 a interface inteira foi refeita a partir de uma referência de calendário com
cabeçalho verde-petróleo e de outras telas de apps financeiros. Motor, banco e regras não
mudaram: os 73 testes, inclusive os dourados, continuam passando sem tocar em nada do domínio
além do texto das mensagens de erro, que estavam sem acento.

O que foi decidido, e por quê:

- **Cabeçalho petróleo em toda página, grande só onde há um número que manda.** Hoje (saldo
  previsto) e Calendário (saldo no fim do mês) têm o destaque; o resto tem cabeçalho compacto.
  Repetir o bloco grande em Ajustes ou num formulário seria decoração.
- **Barra de status `black-translucent`.** O relógio do iPhone fica branco sobre o petróleo. O
  custo é que nenhuma página pode começar sem `<Cabecalho>`, e a `.faixa-status` cobre a área
  quando o conteúdo rola por baixo.
- **Explicação no (i), regra à vista.** Parágrafos explicativos viraram ajuda que abre por toque
  ou teclado (`Rotulo`, `BotaoAjuda`). O que muda dinheiro continua escrito na tela numa linha:
  "no saldo inicial" na lista, "por mês, 4x" ao lado do valor, "Não muda o saldo previsto" na
  renda variável, o aviso das contas fora da previsão. No destaque da tela Hoje há um (i) só para
  os quatro números: um por número quebrava os rótulos das colunas estreitas.
- **O farol diz a regra.** "Tranquilo" virou "acima do conforto". O aviso verde da tela Hoje só
  aparece quando nada está fora da conta: conta vencida sem resposta ou cadastro com problema
  deixam a previsão incompleta, e aí afirmar que está tudo bem seria falso.
- **Calendário começa em hoje.** Os dias que já passaram no mês ficam a um toque ("Mostrar os 2
  dias anteriores"), para registrar o gasto de ontem. Cada dia é uma linha com data, entrada,
  saída, diário e saldo; o detalhe e a edição do gasto do dia abrem numa folha.
- **Entradas do mês dizem quanto é renda variável**, também no Calendário: ali os dias somam
  zero de entrada e o total do mês não, e sem a composição a conta não fecha (R17).
- **Lançar em três abas com endereço próprio** (`?secao=renda`, `?secao=faturas`). Os atalhos
  antigos `/plano` e `/faturas` continuam abrindo a seção certa.
- **Contas do mês abrem numa folha.** Antes cada linha tinha dois ou três botões; agora a linha
  mostra o estado e as ações ficam juntas, com a regra do saldo inicial escrita quando importa.
  As resolvidas ficam recolhidas.
- **Folha com rodapé fixo acima do teclado.** `app.js` mede a área visível de verdade
  (`visualViewport`) e o modal cabe nela; com 420px de altura o botão de salvar continua a vista.
  O modal também passou a fechar com Esc e a prender o foco, que eram lacunas conhecidas.
- **Excluir pede um segundo toque** em lançamento, fatura, carteira, cartão e tag. Antes um toque
  apagava um lançamento recorrente sem volta.
- **Tag é editada numa folha**, com nome e cor. Renomear passou a existir; antes só dava para
  trocar a cor, com oito quadrados por linha na tela de Ajustes.
- **Repetição semanal aparece como semanal.** A lista mostrava "todo mês, sem fim" para um
  lançamento semanal, porque o texto ignorava a periodicidade.
- **Pagamento de fatura de zero é recusado.** Não mudava nada além da data do último pagamento.
  Valor negativo continua aceito: é o único jeito de corrigir um pagamento lançado a mais.
  (Substituído na seção 23: pagamento errado agora se corrige excluindo o registro.)

## 23. Auditoria de 23/10/2026: dinheiro contado duas vezes, travas e rede

Uma auditoria completa achou bugs que mexiam no saldo, reproduzidos chamando o motor direto, e
lacunas de segurança no uso pela rede. O que mudou e por quê:

### Fatura e compra se casam pelo ciclo, não pela data digitada

`Faturas.Chave` passou a ser o vencimento que as regras do cartão calculam, o mais próximo da data
da fatura. O banco empurra o vencimento que cai em fim de semana e o dono digita a data real;
casando pela data exata, a compra (no vencimento calculado) não achava a fatura e o mesmo gasto
saía duas vezes. A fatura continua saindo da conta na data digitada. Duas faturas no mesmo ciclo
passaram a ser recusadas (`Validacao.Fatura`), porque receberiam as mesmas compras.

### Compra no cartão usa o dia de vencimento do cartão em todas as parcelas

Cartão que vence dia 30 com a primeira fatura em fevereiro grudava as parcelas seguintes no dia
28, fora do vencimento e fora da fatura do ciclo. `Ocorrencias.DiaDeReferencia` devolve o dia de
vencimento do cartão, e `Ocorrencias.Do` é o caminho único para gerar ocorrências de um lançamento.

### Cada pagamento de fatura é um registro, com a própria data

O pago ficava somado na fatura, com a data do último pagamento. Pagar parte antes de conferir com
o banco e o resto depois fazia a projeção descontar tudo na data do último, e a parte anterior,
já fora do saldo conferido, saía de novo. Agora existe `PagamentoFatura` (tabela própria, apagada
junto com a fatura) e só conta na projeção o pagamento a partir da âncora. A migração converte o
pago antigo num pagamento só, na data do último (é tudo que se sabia). Backup subiu para a versão
5; arquivo antigo continua abrindo, com a mesma conversão. Valor negativo deixou de ser aceito:
pagamento errado se exclui na edição da fatura.

### Juros do rotativo param em 100% da dívida

Uma fatura antiga não paga (ou com o ano digitado errado) crescia mês a mês até estourar o `long`,
e como toda tela projeta o saldo, o app inteiro travava, inclusive Ajustes, onde se restaura o
backup. O teto segue a Lei 14.690/2023, que desde 2024 limita juros e encargos do rotativo a 100%
do valor original. Junto vieram faixas em `Validacao`: datas entre 2000 e 2100, valores até
R$ 9.999.999.999,99, juros até 100% ao mês, até 360 repetições.

### Travas e erros

- Botão que grava confere a trava **antes de tudo**. O `disabled` só chega ao iPhone depois de uma
  ida e volta pela rede, e um toque duplo passava: criava lançamento repetido e, no pagamento de
  fatura, somava o pagamento duas vezes.
- Nome repetido de cartão, tag, meta e carteira volta como mensagem (o serviço devolve `string?`),
  sem diferenciar maiúsculas. Antes o índice único do banco recusava com exceção e derrubava a tela.
- `ErrorBoundary` no `MainLayout`: erro inesperado fica na tela, com "Tentar de novo", em vez de
  derrubar a conexão inteira.
- A restauração confere tudo que quebraria a gravação ou o cálculo (código e nome repetidos,
  referência solta, data e valor fora da faixa) e, se o banco ainda recusar, nada muda. Regra de
  tela que não quebra nada continua de fora, para backup antigo seguir abrindo.

### Uso pela rede

- **PIN** (`Seguranca/ControleDeAcesso`). Este computador entra direto; outro aparelho precisa
  do PIN, e sem PIN definido a rede só vê um aviso. Seguro por padrão: o dono decide abrir.
- **Filtro de endereço** (`Seguranca/ProtecaoDeRede`). Só localhost, IP e o nome do PC. Sem isso,
  um site malicioso aberto no navegador do PC podia apontar o próprio domínio para 127.0.0.1 (DNS
  rebinding) e ler o app inteiro, inclusive o backup.
- **Perfil `rede` em Production**, para erro não mostrar detalhes técnicos a outros aparelhos.
  `UseStaticWebAssets` ficou ligado sempre, porque fora de Development o `dotnet run` não servia o
  `blazor.web.js`.

### Backup automático

Cópia diária do banco com `VACUUM INTO`, 30 dias guardados, na pasta `backups` ao lado do banco
(ou em `MinhasFinancas:PastaBackups`). A cópia de antes de migrar passou a usar o mesmo método.
Antes disso o `financas.db` era a única cópia do histórico.

### Chaves estrangeiras no banco

Situação, diário, fatura e carteira do lançamento passaram a ter chave estrangeira; antes a
integridade dependia só do código. A migração limpa órfãos antes (que o app nunca cria) e deixa
fatura de cartão apagado como está, porque apagá-la tiraria uma dívida da previsão.