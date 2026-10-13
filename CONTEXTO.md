# Radar do Saldo: contexto completo para iniciar o programa

> Documento de especificação para o Claude (ou qualquer dev) criar um aplicativo de finanças pessoais
> baseado em fluxo de caixa projetado dia a dia. Tudo aqui foi validado numa planilha funcional
> (`Radar_do_Saldo_Janon.xlsx`) e os números esperados estão na pasta `fixtures/`.

---

## 0. Como usar este pacote

Coloque assim no repositório novo:

```
docs/
  CONTEXTO.md                  <- este arquivo
  referencia/Radar_do_Saldo_Janon.xlsx
  fixtures/cenario_exemplo.json
  fixtures/esperado_calendario.csv
  fixtures/esperado_mensal.csv
CLAUDE.md                      <- aponte para docs/CONTEXTO.md (modelo na seção 14)
```

A seção 14 tem o prompt de inicialização pronto para colar.

---

## 1. O que é

Um app pessoal que responde, todo dia, uma pergunta: **"como vai estar meu saldo em cada dia daqui pra frente, e qual é o pior dia que vem aí?"**

A maioria dos apps de finanças olha para trás (categoriza o que já foi gasto). Este olha para frente: você informa o saldo de hoje, tudo que vai entrar e sair nas datas certas e quanto costuma gastar por dia. O app projeta o saldo dia a dia por meses e acende o farol quando vem aperto.

Usuário: uma pessoa (o dono), uso pessoal. Idioma pt-BR, moeda BRL, fuso America/Sao_Paulo.

## 2. Princípios do método

1. **Olhar pro futuro, não pro passado.** O dado central é o saldo projetado de cada dia, não o extrato.
2. **Quando o dinheiro entra/sai importa tanto quanto quanto.** Um salário no dia 5 e um aluguel no dia 3 não se compensam no mesmo mês: o dia 3 aperta.
3. **O número que importa é o MENOR saldo à frente**, não o saldo de hoje. Ele diz se você está tranquilo e se uma compra cabe.
4. **Previsão conservadora.** Entrada incerta fica fora; dia sem registro de gasto conta como o gasto previsto, nunca como zero.
5. **Rotina curta.** Check-in diário de segundos (quanto gastei hoje) e conferência semanal com o banco (recalibrar).

## 3. Origem e limites (importante)

- A ideia segue a lógica **publicamente documentada** de um método de mercado de fluxo de caixa projetado (planilha com blocos diários Data/Entrada/Saída/Diário/Saldo, plano de contas e indicadores mensais). Nenhum arquivo, aula, fórmula interna ou material pago foi usado; as fórmulas exatas do produto original não são públicas.
- O programa é **original**: nome próprio (Radar do Saldo), visual próprio, código próprio.
- **Não usar** nome, marca, logo, imagens, textos ou identidade visual de produtos de terceiros (por exemplo "Planilha do Breno", "App do Breno", "Escola do Breno"). Termos genéricos como Performance, Custo de vida, Economizado e Diário médio podem ser usados.

## 4. Glossário

| Termo | Significado |
|---|---|
| Âncora | Par (saldo, data) que o usuário informa: o saldo da conta corrente no **início** do dia da âncora, antes dos movimentos desse dia. A projeção começa aí. |
| Recalibrar | Trocar a âncora por um novo saldo/data (ex.: conferência semanal com o banco). |
| Diário | Gasto miúdo do cotidiano (lanche, mercado de passagem, transporte avulso). Não vai no Plano de Contas; é um valor por dia. |
| Diário previsto | Valor padrão por dia, configurável. Na dúvida, o usuário erra pra mais. |
| Diário real | Valor que o usuário registrou para uma data específica. Pode ser 0 ("não gastei nada"). |
| Plano de Contas | Lista de lançamentos programados: Entrada, Saída, Cartão, Economia, com data, valor e repetição. |
| Saldo de conforto | Limite pessoal. Abaixo dele o farol fica amarelo. |
| Farol | Situação de um saldo: `ok` (>= conforto), `atencao` (entre 0 e conforto), `negativo` (< 0). |
| Horizonte | Até onde a projeção vai. Padrão no app: 24 meses à frente de hoje (configurável). |

## 5. Modelo de dados

Dinheiro sempre em **centavos (inteiro)**. Datas sempre **datas locais** (sem hora), fuso America/Sao_Paulo.

### Configuracao (1 registro)
| Campo | Tipo | Regra |
|---|---|---|
| saldo_ancora_centavos | int | pode ser negativo |
| data_ancora | date | obrigatório |
| diario_previsto_centavos | int | >= 0 |
| saldo_conforto_centavos | int | >= 0 |
| contar_entradas_incertas | bool | padrão `false` |
| ja_guardado_centavos | int | só para acompanhamento de economias |
| horizonte_meses | int | padrão 24 |

Guarde também um **histórico de âncoras** (data, saldo, criado_em) para auditoria da conferência.

### Cartao
`id, nome (único), dia_fechamento (1..31), dia_vencimento (1..31)`

### Tag
`id, nome (único)`. Opcional por lançamento.

### Lancamento (Plano de Contas)
| Campo | Tipo | Regra |
|---|---|---|
| data | date | Para Cartão: data da **compra**. Para os demais: data em que cai na conta. |
| descricao | text | |
| tipo | enum | `Entrada`, `Saida`, `Cartao`, `Economia` |
| valor_centavos | int > 0 | Valor de **cada** ocorrência (parcela), não o total. |
| vezes | int ou null | 1 = uma vez; N = N ocorrências mensais; `null` = todo mês sem fim. (A planilha usava 999 para "sem fim".) |
| cartao_id | fk ou null | Obrigatório se tipo = Cartao. |
| certeza | enum ou null | Só para Entrada: `Certa` (padrão) ou `Incerta`. |
| tag_id | fk ou null | |
| obs | text | |

### DiarioReal
`data (PK), valor_centavos (>= 0)`. Ausência de registro = usa o previsto. Registro com 0 = "não gastei nada".
No app existe a ação explícita **"voltar para previsão"** (apaga o registro). Isso resolve uma limitação da planilha, onde apagar a célula virava 0.

### Simulacao (pode ser só estado de tela)
`descricao, valor_total_centavos, parcelas (>= 1), primeira_parcela (date)`

## 6. Regras do motor de projeção

O motor é uma **função pura**: `projetar(configuracao, cartoes, lancamentos, diarios_reais, hoje, simulacao?) -> resultado`.
Recalcula o horizonte inteiro a cada mudança (são ~730 dias × poucas centenas de lançamentos: trivial). Não persistir valores calculados.

### R1. Data efetiva de uma compra no cartão (vencimento da fatura)
```
fech = cartao.dia_fechamento ; venc = cartao.dia_vencimento ; c = data da compra
mes_fatura   = (dia(c) >= fech) ? mes(c) + 1 : mes(c)
mes_venc     = (venc <= fech)   ? mes_fatura + 1 : mes_fatura
data_efetiva = data(ano/mes_venc, min(venc, ultimo_dia(mes_venc)))
```
Exemplo: compra 14/10/2026, fecha 3, vence 10 → 14 >= 3 → fatura de novembro → 10 > 3 → vence em novembro → **10/11/2026**.
Compra no próprio dia do fechamento vai para a fatura seguinte.

Para os demais tipos: `data_efetiva = data`.

### R2. Ocorrências (repetição e parcelas)
```
para k = 0, 1, 2, ... enquanto (vezes == null ou k < vezes):
    mes_k = mes(data_efetiva) + k
    ocorrencia_k = data(ano/mes_k, min(dia(data_efetiva), ultimo_dia(mes_k)))
```
Dia 31 em mês curto cai no último dia (30/11, 28/02, 29/02 em ano bissexto). Validado na planilha.
Campos derivados mostrados na lista: **"Cai na conta em"** (ocorrência 0) e **"Última vez"** (ocorrência vezes-1, ou "sem fim").

### R3. O que entra na projeção
Uma ocorrência conta se:
- `data_ancora <= ocorrencia <= fim_do_horizonte`, e
- não é (`tipo == Entrada` e `certeza == Incerta` e `contar_entradas_incertas == false`).

Lançamentos cujas ocorrências estão todas antes da âncora aparecem **riscados** na lista (já estão dentro do saldo).

### R4. Movimentos de cada dia d (d >= data_ancora)
```
E(d)        = soma das ocorrências de Entrada que contam
Saida(d)    = soma das ocorrências de Saida
Cartao(d)   = soma das ocorrências de Cartao (pelo vencimento)
Economia(d) = soma das ocorrências de Economia
S(d)        = Saida(d) + Cartao(d) + Economia(d)
D(d)        = diario_real(d) se existir registro (inclusive 0), senão diario_previsto
```
Atenção: dia que **já passou** sem registro usa o previsto (conservador). Não é zero.

### R5. Saldo do dia
```
saldo(data_ancora) = saldo_ancora + E - S - D   (do próprio dia da âncora)
saldo(d)           = saldo(d-1) + E(d) - S(d) - D(d)
```
Dias antes da âncora não têm saldo (exibir vazio).

### R6. Farol
`negativo` se saldo < 0; `atencao` se saldo < conforto; senão `ok`.

### R7. Painel "Agora" (referência: hoje)
- **Saldo hoje** = saldo(hoje).
- **Menor saldo à frente** = mínimo de saldo(d) para d em [hoje, fim do horizonte] e a **data mais cedo** em que ele acontece.
- **Pode gastar hoje** = max(0, menor saldo à frente - conforto).
- **Primeiro dia abaixo do conforto** = menor d >= hoje com saldo(d) < conforto, ou "nunca".
- **Guardado até hoje** = ja_guardado + soma de Economia(d) para d em [âncora, hoje].
- **Conferência**: usuário digita o saldo real do banco; diferença = real - saldo(hoje). Se != 0, oferecer **Recalibrar** (nova âncora = saldo real, data = hoje).

### R8. Indicadores do mês M
Considere só dias d do mês com d >= âncora.
```
Entradas       = soma E(d)
Saidas         = soma Saida(d)
Cartao         = soma Cartao(d)
Diario         = soma D(d)               (real onde existe, previsto no resto)
Economias      = soma Economia(d)
Performance    = Entradas - Saidas - Diario - Economias - Cartao
Custo de vida  = Saidas + Diario + Cartao
Economizado    = Economias / Entradas    (0 se Entradas = 0)
Diario medio   = soma D(d) / qtd dias,  d em [max(âncora, 1º do mês), min(hoje, fim do mês)]   ("-" se nenhum dia)
Dias com diario real = qtd de d do mês, d >= âncora, com registro em DiarioReal
Entradas incertas fora = soma das ocorrências de Entrada Incerta no mês (>= âncora) quando o toggle está desligado
Saldo no fim do mês    = saldo(último dia do mês)
Menor saldo do mês     = mínimo saldo(d) do mês + data mais cedo
```
As fórmulas de Performance, Custo de vida, Economizado e Diário médio reproduzem a reconciliação aritmética pública do método (exemplo: entradas 4.500; saídas 2.876,60; diário 105; economias 500; cartão 0 → performance 1.018,40; custo de vida 2.981,60; economizado 11%; diário médio 105/24 = 4,38). Use esse exemplo como teste unitário extra.

### R9. Saídas por tag (mês M)
Soma das ocorrências de lançamentos não-Entrada com aquela tag, no mês, com data >= âncora. Mostrar valor e % do total das tags.

### R10. Horizonte de saldos (12 meses)
Começa no mês de max(hoje, âncora). Para cada mês: Entradas, Saídas e cartão (Saida + Cartao), Diário, Economias, Performance, Saldo no fim, Menor saldo, Guardado acumulado (= ja_guardado + Economias de âncora até o fim do mês) e Farol (aplicado ao menor saldo do mês).

### R11. Simulador de compra
```
parcela_k (k = 0..n-1) cai em: data(ano/mes(primeira)+k, min(dia(primeira), ultimo_dia))
valor_parcela = total / n   (centavos: arredondar e jogar a diferença na primeira parcela)
saldo_sim(d)  = saldo(d) - soma das parcelas com data <= d
```
Resultado, olhando de hoje até o fim do horizonte:
- menor saldo sem a compra (e data), menor saldo com a compra (e data)
- primeiro dia abaixo do conforto com a compra
- folga = menor com a compra - conforto
- veredito: `nao_cabe` se menor com compra < 0; `cabe_mas_aperta` se < conforto; senão `cabe_com_folga`

Se for no cartão, o usuário informa como primeira parcela a data de vencimento da fatura (melhoria futura: calcular via R1).

## 7. Regras de uso que o app deve ensinar
- Gasto miúdo pago no cartão: lançar como Cartão no Plano e **não** somar de novo no Diário.
- Não lançar a fatura inteira como Saída se as compras já estão como Cartão (dupla contagem).
- Saldo âncora é o saldo **antes** dos movimentos do dia da âncora. Se o banco já mostra um gasto de hoje, não registre esse gasto de novo no diário de hoje (ou recalibre com data de amanhã). Explicar isso no onboarding.
- Recalibrar não apaga nada: diários reais e lançamentos continuam guardados por data; o que fica antes da nova âncora só deixa de contar.

## 8. Telas e fluxos (mobile first)

1. **Onboarding (wizard)**: saldo de hoje → diário previsto → saldo de conforto → cartões → primeiras contas fixas (salário, aluguel, assinaturas) → pronto. Ao final mostrar o "menor saldo à frente".
2. **Hoje (tela inicial)**: cartões grandes com Saldo hoje, Pode gastar hoje, Menor saldo à frente (com data) e farol. Campo rápido **"Quanto gastei hoje?"** (check-in diário, aceita 0). Próximos 7 dias de movimentos. Alerta do próximo aperto.
3. **Calendário**: lista dia a dia agrupada por mês, rola até hoje. Colunas: Data, Dia da semana, Entrada, Saída, Diário (editável inline; previsto em cinza itálico, real em azul negrito), Saldo, Farol. Ação "voltar para previsão" no diário.
4. **Plano de Contas**: CRUD de lançamentos, filtros por tipo e tag, colunas derivadas "Cai na conta em" e "Última vez", riscado quando já está todo antes da âncora, aviso se Cartão sem cartão válido.
5. **Resumo do mês**: seletor de mês, indicadores do R8, saídas por tag com barra proporcional, horizonte de 12 meses (R10), gráfico de linha do saldo diário com linha tracejada do conforto.
6. **Simulador**: formulário + resultado + veredito colorido. Mostrar a curva com e sem a compra.
7. **Configurações**: âncora e conferência com banco (Recalibrar), diário previsto, conforto, toggle de entradas incertas, já guardado, cartões, tags, horizonte, exportar/importar dados.

### Identidade visual (herdada da planilha, minimalista)
- Faixa/topo: verde-petróleo `#134E4A`; acento `#0F766E`; texto `#1F2933`; secundário `#6B7280`; linhas `#E5E7EB`; fundo de cartão `#F8FAFC`.
- Tipos (pílulas): Entrada `#DCFCE7/#166534` · Saída `#FEE2E2/#991B1B` · Cartão `#EDE9FE/#5B21B6` · Economia `#DBEAFE/#1E40AF` · Diário `#FEF3C7/#92400E`.
- Farol: ok verde `#15803D`, atenção âmbar `#FEF3C7/#92400E`, negativo vermelho `#FEE2E2/#B91C1C`. Hoje destacado em `#D1FAE5`.
- Textos de interface em pt-BR informal e direto. **Não usar travessão (—)** em nenhum texto da interface.
- Formatação: `R$ 1.234,56`, datas `dd/mm/aaaa`, meses por extenso em português.

## 9. Arquitetura recomendada

Recomendação padrão (alinhada com C#/.NET, SQL e testes automatizados, que é o ecossistema do dono):

```
src/
  Radar.Domain/        <- motor puro (R1..R11), sem I/O, sem DateTime.Now (recebe "hoje")
  Radar.Infra/         <- EF Core + SQLite (arquivo local), migrations
  Radar.Web/           <- ASP.NET Core + Blazor Web App, responsivo e instalável (PWA)
tests/
  Radar.Domain.Tests/  <- xUnit: testes de regra + testes dourados contra docs/fixtures
  Radar.E2E/           <- Playwright .NET (fluxos: onboarding, check-in diário, simulador)
.github/workflows/ci.yml  <- build + testes em cada push
```

Pontos de projeto:
- `IClock` injetável para "hoje" (testes usam 2026-10-13).
- Dinheiro como `long` em centavos; formatação só na borda (UI).
- Motor recalcula tudo em memória; a UI chama `projetar(...)` e renderiza.
- Persistência local primeiro (um usuário). Login e nuvem ficam para depois.
- Alternativa equivalente se preferir TypeScript: Next.js + Prisma + SQLite + Vitest + Playwright. O motor continua sendo uma função pura com os mesmos testes dourados.

## 10. Cenário de referência e testes dourados

`fixtures/cenario_exemplo.json` traz a entrada completa (hoje = 2026-10-13):
- Âncora: R$ 2.500,00 em 13/10/2026 · diário previsto R$ 40,00 · conforto R$ 500,00 · incertas fora · já guardado R$ 1.000,00 · horizonte até 31/12/2028.
- Cartão "Principal": fecha 3, vence 10.
- Lançamentos: Tênis (Cartão, 4x R$ 89,90, compra 14/10) · Internet (Saída R$ 110, dia 15, sem fim) · Streaming (Saída R$ 40, dia 18, sem fim) · Freela (Entrada **Incerta** R$ 600, 20/10, uma vez) · Academia (Saída R$ 99, dia 25, sem fim) · Salário (Entrada Certa R$ 4.200, dia 5 a partir de 05/11, sem fim) · Reserva de emergência (Economia R$ 400, dia 6 a partir de 06/11, sem fim) · Aluguel (Saída R$ 1.300, dia 10 a partir de 10/11, sem fim).
- Diário real: R$ 50,00 em 13/10/2026.
- Simulação: Notebook R$ 1.900,00 em 1x, sai em 10/11/2026.

Os campos `cai_na_conta_em` e `ultima_vez` de cada lançamento no JSON são **valores esperados** (R1/R2), não entrada.

Resultados esperados (todos em centavos nos arquivos):
- `esperado_calendario.csv`: 811 dias (13/10/2026 a 31/12/2028) com entradas, saídas, cartão, economias, diário, se o diário é real, saldo e farol. Ex.: 13/10 saldo 2.450,00 · 15/10 2.260,00 · 31/10 1.481,00 · 04/11 1.321,00 · 05/11 5.481,00.
- `esperado_mensal.csv`: horizonte de 12 meses (R10). Ex.: nov/2026 entradas 4.200,00 · saídas e cartão 1.638,90 · diário 1.200,00 · economias 400,00 · performance 961,10 · fim 2.442,10 · menor 1.321,00 · guardado 1.400,00.
- No JSON, bloco `esperado`:
  - Agora: saldo hoje 2.450,00 · menor saldo à frente 1.321,00 em 04/11/2026 · pode gastar hoje 821,00 · primeiro dia abaixo do conforto "nunca" · guardado até hoje 1.000,00.
  - Outubro/2026: entradas 0 · saídas 249,00 · cartão 0 · diário 770,00 · economias 0 · performance -1.019,00 · custo de vida 1.019,00 · economizado 0% · diário médio 50,00 · 1 dia com diário real · incertas fora 600,00 · fim 1.481,00 · menor 1.481,00 em 31/10.
  - Simulação: menor sem 1.321,00 (04/11) · menor com 382,10 (04/12) · primeiro dia abaixo do conforto 02/12/2026 · folga -117,90 · veredito `cabe_mas_aperta`.

Esses números foram conferidos por duas implementações independentes (fórmulas da planilha e um script separado) com zero divergências. **O motor só está pronto quando bate 100% com os fixtures.**

Casos de teste unitários mínimos, além dos dourados:
1. R1: compra antes, no dia e depois do fechamento; vencimento menor ou igual ao fechamento (vira mês seguinte); vencimento 31 em fevereiro.
2. R2: dia 31 recorrente passando por 30/11, 28/02/2027 e 29/02/2028; vezes = 1, N e null.
3. R3: lançamento antes da âncora ignorado; Entrada Incerta com toggle ligado e desligado.
4. R4: diário real 0 diferente de "sem registro"; dia passado sem registro usa o previsto; "voltar para previsão".
5. R8: exemplo público (4.500 / 2.876,60 / 105 / 500 / 0 → 1.018,40 / 2.981,60 / 11% / 4,38 com 24 dias).
6. Recalibração: trocar âncora preserva diários reais por data e reinicia o saldo.
7. Empate de mínimo: retorna a data mais cedo.

## 11. Casos de borda e decisões já tomadas
- Âncora no futuro: permitido; dias até lá ficam sem saldo.
- Saldo âncora negativo: permitido; farol já começa vermelho.
- Lançamento com valor 0 ou sem data: inválido (não salvar).
- Cartão apagado com lançamentos vinculados: bloquear ou pedir para reatribuir.
- Horizonte termina: no app ele é sempre relativo a hoje (24 meses), sem a limitação de "estender linhas" da planilha.
- Arredondamento: tudo em centavos; só a divisão de parcelas do simulador arredonda (resto na primeira parcela).
- "Hoje" muda à meia-noite de America/Sao_Paulo.

## 12. Roadmap sugerido
- **MVP**: motor + testes dourados · onboarding · Plano de Contas · Calendário com diário editável · tela Hoje · Configurações com recalibração.
- **v1.1**: Resumo do mês completo, horizonte de 12 meses, gráfico com linha de conforto, simulador.
- **v1.2**: importar a planilha `Radar_do_Saldo_Janon.xlsx` (abas Início, Plano de Contas e Diário do Calendário) e exportar backup JSON/CSV.
- **v1.3**: lembrete diário de check-in (ex.: 21h), PWA offline, simulador no cartão calculando o vencimento automaticamente.
- **Depois**: importação de extrato (OFX/CSV) para sugerir diário real, várias contas, login e sync na nuvem.

## 13. Decisões em aberto (o Claude deve perguntar antes de começar)
1. Confirmar a stack (padrão: .NET 8 + Blazor + SQLite; alternativa: Next.js + SQLite).
2. Só local no início (recomendado) ou já com login e hospedagem?
3. Horizonte padrão de 24 meses está bom?
4. Nome do app: manter "Radar do Saldo"?

## 14. Prompt de inicialização (copie e cole no Claude)

```
Vou criar um app pessoal de fluxo de caixa projetado chamado Radar do Saldo.
Leia docs/CONTEXTO.md inteiro e os arquivos em docs/fixtures antes de qualquer código.
A planilha de referência está em docs/referencia/Radar_do_Saldo_Janon.xlsx.

Como quero que você trabalhe:
1. Me faça as perguntas da seção 13 numa mensagem só e espere minha resposta.
2. Depois proponha a estrutura de pastas e um plano curto por etapas (MVP da seção 12).
3. Comece pelo motor de domínio puro (regras R1 a R11), sem UI e sem banco.
   Escreva os testes unitários da seção 10 e os testes dourados que leem
   cenario_exemplo.json, esperado_calendario.csv e esperado_mensal.csv.
   Só avance quando todos os testes passarem com 100% de igualdade em centavos.
4. Só então faça persistência e telas, na ordem: Configurações/onboarding,
   Plano de Contas, Calendário com diário editável, tela Hoje.
5. Use dinheiro em centavos (inteiros), datas locais, fuso America/Sao_Paulo
   e um relógio injetável para "hoje".
6. Interface em pt-BR, informal e direta, visual minimalista com a paleta da seção 8.
   Não use travessão (—) em nenhum texto.
7. Não use nome, marca, imagens ou textos de produtos de terceiros (seção 3).
8. Ao fim de cada etapa: rode os testes, me diga o que mudou e o que vem a seguir.
```

### Modelo de CLAUDE.md (raiz do repositório)
```
# Radar do Saldo
App pessoal de fluxo de caixa projetado dia a dia. Especificação completa: docs/CONTEXTO.md.
- Motor de domínio é função pura; regras R1..R11 em docs/CONTEXTO.md seção 6.
- Testes dourados em docs/fixtures precisam passar sempre (igualdade exata em centavos).
- Dinheiro em centavos (long/int), datas locais, fuso America/Sao_Paulo, "hoje" via relógio injetável.
- UI em pt-BR, minimalista, sem travessão. Sem marcas ou materiais de terceiros.
```
