# Contexto e especificação inicial — programa pessoal de planejamento financeiro

Documento de passagem de contexto para o Claude · 13/10/2026

## 1. Instrução inicial para o Claude

Você vai iniciar um programa de finanças pessoais baseado na metodologia descrita neste documento. O objetivo é transformar a lógica de uma planilha pessoal em uma aplicação prática, com persistência de dados, lançamentos rápidos, projeção diária de saldo, metas e simulação de compras.

Leia o documento inteiro antes de implementar. Se houver um projeto existente, inspecione sua estrutura e preserve suas convenções. Se não houver, inicialize uma aplicação web para execução local, acessível pelo navegador. Essa plataforma é uma proposta de partida, não uma escolha já confirmada pelo usuário. Adote uma solução simples e registre as decisões técnicas, sem interromper o trabalho por escolhas reversíveis.

Comece construindo uma versão funcional: configuração → lançamento → cálculo → painel → persistência. Não entregue apenas um desenho de telas, dados estáticos ou um plano. Depois integre orçamento, metas e simulador conforme a ordem de implementação deste documento.

O usuário prefere respostas diretas, preservação do contexto e nenhuma invenção de dados pessoais. Ele pediu expressamente: **“Sem tantos testes, eu testo.”** Faça somente verificações curtas dos cálculos centrais e da inicialização. Não gaste a maior parte do trabalho em testes, infraestrutura ou refatorações antecipadas. Entregue uma versão executável, informe as limitações reais e deixe a validação funcional ampla para ele.

Não trate sugestões deste documento como decisões pessoais já aprovadas. Diferencie metodologia estabelecida, proposta técnica e evolução futura. Não acrescente integração bancária, login, assinatura, publicação na internet ou arquitetura para vários usuários nesta primeira etapa.

## 2. Origem e estado atual do trabalho

A sequência do trabalho foi:

1. Foi preparado um dossiê de referências públicas sobre uma planilha comercial de planejamento financeiro, conhecida como Planilha do Breno.
2. O usuário pediu uma versão própria e original para uso pessoal, baseada na ideia e na metodologia apresentadas no dossiê.
3. Foi criada a planilha `Minhas_Financas_Pessoais.xlsx`.
4. O usuário solicitou melhorias nos cálculos e no design.
5. Foi entregue uma revisão com projeção de 24 meses, orçamento mensal, alertas, metas e simulador com entrada e juros.
6. Agora o usuário quer contexto suficiente para iniciar um programa com essa metodologia.

### Fontes e precedência

- **Base conceitual:** `levantamento_planilha_breno_nogueira.md`, dossiê público de 13/10/2026, especialmente seções 3–5: entradas, saídas, gastos diários, saldo futuro e simulação.
- **Referência funcional própria:** `Minhas_Financas_Pessoais.xlsx`, revisão entregue em 13/10/2026, com sete abas descritas adiante.
- **Este documento:** explicita as regras, aponta limitações e propõe a tradução para software.

O dossiê não contém o código, as fórmulas internas ou uma cópia licenciada do produto comercial. Parte dos cálculos ali descritos é inferência a partir de demonstrações públicas. Portanto, não apresentar este programa como reprodução exata, oficial ou validada daquele produto. A implementação, o nome, a identidade visual e as regras detalhadas são próprios.

A planilha também não deve ser tomada como especificação infalível: a validação funcional completa ficou com o usuário. Sua exportação utiliza recálculo ao abrir no Excel; valores em cache ou alertas de uma prévia não são autoridade matemática. Não transportar dependências de Excel, fórmulas auxiliares ou erros de renderização para o programa.

Não há receitas, salários, despesas, contas bancárias ou metas reais do usuário cadastrados na referência. Datas de outubro de 2026 eram parâmetros de partida da planilha, não uma exigência para novos projetos. Começar sem dados pessoais inventados. Qualquer demonstração deve ser opcional, identificada como fictícia e separada dos dados de uso.

## 3. Qual problema o programa deve resolver

A pergunta principal é: **“Com o que tenho hoje e com o que está previsto, como ficará meu dinheiro nos próximos dias e meses?”**

As perguntas complementares são:

- Quanto tenho efetivamente disponível e quanto já está separado para metas?
- Em qual dia meu saldo pode ficar negativo ou abaixo do mínimo que defini?
- Qual é o menor saldo previsto, mesmo que eu termine o mês positivo?
- O orçamento diário está coerente com meus gastos?
- Quais compromissos estão vencidos ou próximos do vencimento?
- Uma compra à vista ou parcelada compromete meu saldo futuro?
- Quanto preciso reservar por mês para atingir uma meta?

O programa deve favorecer planejamento e decisões futuras. A categorização do passado serve de apoio, mas não pode tornar cada lançamento trabalhoso nem dominar a experiência.

### Princípios

- Calendário financeiro por **data de movimentação do dinheiro**.
- Separação explícita entre **previsto**, **realizado** e **cancelado**.
- O realizado substitui a previsão do mesmo movimento.
- Saldo disponível e dinheiro reservado em metas são separados.
- Projeção diária é a base dos resumos mensais.
- Toda informação derivada deve poder ser explicada por seus lançamentos.
- Ausência de informação não significa valor zero.
- Alertas indicam pendências e hipóteses; não garantem capacidade de compra.

## 4. Vocabulário e configurações

| Termo | Definição operacional |
| --- | --- |
| Mês inicial | Mês em que começa a projeção. Normalizado para seu primeiro dia. |
| Saldo disponível inicial | Dinheiro disponível no fechamento do dia anterior ao início. Exclui os valores iniciais das metas. Pode ser zero ou negativo. |
| Data de acompanhamento | Data até a qual o usuário conferiu seus registros. Não é necessariamente a data atual do computador. |
| Dia encerrado | Indica se o gasto diário da data de acompanhamento já foi encerrado. |
| Horizonte | 24 meses consecutivos a partir do mês inicial, incluindo esse mês. |
| Gasto diário padrão | Estimativa de gastos cotidianos pagos diretamente, em reais por dia. Zero desativa a estimativa. |
| Gasto diário específico | Substituição do padrão para determinado mês. Vazio herda o padrão; zero desativa naquele mês. |
| Teto de consumo | Limite mensal opcional para contas, cartão e gastos diários, descontados os estornos. |
| Piso de segurança | Saldo mínimo desejado. Abaixo dele existe atenção; abaixo de zero existe saldo negativo. |
| Mês em análise | Mês selecionado no painel; altera a visualização, não o saldo inicial nem o período armazenado. |

Fim da projeção = primeiro dia do mês inicial acrescido de 24 meses, menos um dia.

Usar datas de calendário, inclusive fins de semana e feriados. Não deslocar automaticamente vencimentos para dias úteis. A referência não implementa essa regra.

Separar `null` de `0`: saldo não informado impede calcular saldo; saldo zero é válido. Teto não informado significa sem limite; teto zero significa limite igual a zero.

Ao mudar o mês de abertura, é necessário revisar o saldo disponível e os saldos iniciais das metas. Não simplesmente trocar a data e reaproveitar uma abertura incompatível. No programa, propor uma revisão explícita desses valores, preservando os lançamentos históricos.

## 5. Tipos de movimento e efeitos

Os valores informados são sempre magnitudes positivas ou zero. O tipo define o sinal.

| Tipo | Saldo disponível | Consumo | Receita | Reserva da meta quando realizado |
| --- | --- | --- | --- | --- |
| Entrada | Aumenta | Não altera | Aumenta | Não altera |
| Conta | Diminui | Aumenta | Não altera | Não altera |
| Diário | Diminui | Aumenta | Não altera | Não altera |
| Cartão | Diminui na data do pagamento | Aumenta | Não altera | Não altera |
| Reserva | Diminui | Não altera | Não altera | Aumenta |
| Resgate | Aumenta | Não altera | Não altera | Diminui |
| Estorno | Aumenta | Diminui | Não altera | Não altera |

Uma reserva é transferência do dinheiro disponível para uma meta, não despesa de consumo. Um resgate faz o inverso e não é renda. Um estorno recupera um gasto e não é receita nova.

O programa começa com **um saldo disponível consolidado**. A planilha não controla contas bancárias individualmente nem transferências entre bancos. Não criar falsa precisão por conta ou um patrimônio total que inclua bens e dívidas inexistentes no modelo.

## 6. Lançamentos: campos, estados e validações

### Campos de entrada

| Campo | Regra |
| --- | --- |
| Identificador | ID estável gerado pelo programa. |
| Descrição | Obrigatória para movimento ativo. |
| Tipo | Um dos sete tipos descritos. |
| Situação | Previsto, Realizado ou Cancelado. |
| Data prevista | Obrigatória para Previsto. Mantida como histórico quando realizado. |
| Valor previsto | Obrigatório para Previsto, maior ou igual a zero. |
| Data realizada | Obrigatória para Realizado. |
| Valor realizado | Obrigatório para Realizado; zero é um valor válido. |
| Categoria | Opcional; classificação simples. |
| Identificação do cartão | Opcional no MVP; separar da categoria no modelo novo. |
| Meta vinculada | Obrigatória em Reserva e Resgate. Usar ID, não o nome como chave. |
| Observação | Proposta opcional para o programa. |

Para confirmar uma previsão, editar **o mesmo registro**, preencher valor/data realizados e mudar a situação. Não criar uma segunda linha equivalente nem somar previsão e realização.

### Normalização

```text
se situação = Cancelado:
    movimento não entra em nenhum total financeiro
se situação = Previsto:
    data usada = data prevista
    valor usado = valor previsto
se situação = Realizado:
    data usada = data realizada
    valor usado = valor realizado
```

A realização pode ter valor e data diferentes da previsão. O período de contabilização segue a data usada. A diferença individual, quando houver ambos os valores, é `realizado − previsto`.

Realização após a data de acompanhamento exige revisão. Para o programa, impedir uma confirmação inconsistente ou oferecer a atualização explícita da data de acompanhamento; não avançá-la silenciosamente. Em importações, guardar a linha como pendente e explicar o motivo.

Movimentos cancelados não são pendências de cálculo. Campos vazios da planilha não equivalem a registros reais do banco de dados.

### Pendências e duplicidades

Registros incompletos não entram nos cálculos e devem aparecer com motivo específico. Na criação normal pelo formulário, validar antes de salvar como movimento ativo. Se houver rascunhos/importações pendentes, mantê-los fora dos totais, com estado explícito.

A referência avisa possível duplicidade quando descrição, tipo, situação, categoria/cartão, data usada e valor usado coincidem. Para o programa, uma comparação normalizada e o ID da meta/cartão podem refinar o aviso. Não excluir, mesclar nem desconsiderar automaticamente: dois movimentos iguais podem ser legítimos.

Mostrar a diferença entre quantidade de grupos duplicados e quantidade de registros envolvidos. Não usar contadores ambíguos.

Movimentos fora do horizonte continuam armazenados, mas não participam dos totais daquele período. Exibi-los em um filtro próprio.

### Previsões vencidas

Um Previsto com data anterior à data de acompanhamento permanece na projeção na sua data original, até o usuário confirmar, reagendar ou cancelar. Ele não passa a Realizado por decurso de prazo e não muda automaticamente para hoje.

Isso significa que o saldo projetado na data de acompanhamento pode divergir do saldo confirmado, pois inclui previsões vencidas ainda abertas. Explicar essa diferença no painel e permitir abrir a lista responsável por ela. Não reiniciar a projeção no saldo confirmado e descartar vencidos sem uma decisão de produto.

## 7. Motor de cálculo: fluxo diário

Esta é a regra central a implementar em código, independentemente da interface.

### 7.1 Complemento de gasto diário

Para cada dia, somar os movimentos válidos do tipo Diário, previstos e realizados, conforme a data usada.

```text
orçamento diário do dia = específico do mês, se informado; senão, padrão
início da estimativa = data de acompanhamento + (dia encerrado ? 1 dia : 0 dias)

se dia >= início da estimativa:
    complemento diário = máximo(0, orçamento diário − diário lançado no dia)
senão:
    complemento diário = 0
```

A despesa diária projetada é `diário lançado + complemento diário`.

Exemplos fictícios, com orçamento de R$ 50,00:

- Sem lançamento Diário, dia ainda estimado: complemento R$ 50,00.
- Diário de R$ 20,00: complemento R$ 30,00; total diário R$ 50,00.
- Diário de R$ 80,00: complemento zero; total diário R$ 80,00.
- Dia de acompanhamento encerrado, Diário de R$ 20,00: complemento zero; total R$ 20,00.
- Dia anterior ao acompanhamento, sem Diário registrado: total zero; não preencher o passado com estimativas.

Conta, Cartão e Reserva não consomem esse orçamento diário. Estorno reduz o consumo total, mas não repõe o complemento diário na metodologia atual. Explicar que gastos cotidianos pagos por cartão devem seguir o regime de cartão, evitando também registrá-los como Diário.

O complemento é uma estimativa calculada, não um lançamento persistido. Não gerar centenas de lançamentos artificiais para representá-lo.

### 7.2 Resultado projetado do dia

```text
resultado projetado = entradas
                     − contas
                     − diário lançado
                     − complemento diário
                     − cartão
                     − reservas
                     + resgates
                     + estornos
```

Todos os componentes usam movimentos válidos, previstos ou realizados. Cada movimento aparece uma única vez.

```text
saldo projetado no primeiro dia = saldo inicial + resultado projetado do primeiro dia
saldo projetado nos dias seguintes = saldo projetado anterior + resultado do dia
```

### 7.3 Saldo realizado e saldo confirmado

```text
resultado realizado do dia = soma dos efeitos com situação Realizado naquele dia
saldo realizado do dia = saldo inicial + resultados realizados acumulados desde o início
saldo confirmado = saldo realizado na data de acompanhamento
```

Não incluir complemento diário nem previsões no saldo confirmado. Não apresentar saldo realizado de um dia futuro como se tivesse sido conferido.

### 7.4 Alertas e mínimos

- Negativo: saldo projetado < 0.
- Atenção: saldo projetado >= 0 e < piso de segurança.
- Dentro do piso: saldo projetado >= piso de segurança.
- Menor saldo futuro: mínimo dos saldos diários entre acompanhamento e fim da projeção, inclusive.
- Data do menor saldo: primeira ocorrência desse mínimo, em caso de empate.
- Primeiro dia negativo: primeira data nesse intervalo com saldo projetado < 0.
- Dias negativos: contagem de datas nesse intervalo com saldo projetado < 0.
- Saldo final: saldo projetado no último dia do horizonte.

O saldo é de fechamento do dia. Não há ordem intradiária de pagamentos; o sistema não garante disponibilidade em cada horário. Um mês pode terminar positivo e conter dias negativos.

### 7.5 Implementação sugerida

Validar e normalizar movimentos; agrupá-los uma vez por data e tipo; percorrer o calendário em ordem acumulando saldos; extrair indicadores e resumos da mesma sequência. Evitar varrer todos os lançamentos novamente para cada célula, gráfico ou dia.

Separar as funções de domínio dos componentes de interface e do banco. O mesmo cálculo deve servir ao painel, à tabela diária e ao simulador.

## 8. Resumos mensais e orçamento

| Indicador | Fórmula |
| --- | --- |
| Entradas projetadas | Soma das entradas do mês |
| Consumo projetado | Contas + cartão + Diário + complemento diário − estornos |
| Reservas líquidas projetadas | Reservas − resgates |
| Resultado projetado | Entradas − consumo − reservas líquidas |
| Saldo final do mês | Último saldo diário daquele mês |
| Menor saldo do mês | Menor saldo de fechamento entre todos os dias daquele mês |
| Percentual reservado | Reservas líquidas / entradas; indefinido quando entradas = 0 |
| Diário estimado | Soma apenas dos complementos diários |
| Folga no teto | Teto de consumo − consumo projetado, se houver teto |

A coluna Realizado usa somente movimentos realizados e nenhum complemento. A coluna Projetado inclui realizados, previsões e complemento diário. A coluna Diferença é `projetado − realizado`. No consumo, diferença positiva representa consumo ainda previsto; não significa economia.

Resultado mensal é fluxo líquido do mês; não é saldo bancário. O saldo final inclui o saldo de abertura e o efeito dos meses anteriores.

Reservas líquidas e percentual reservado podem ser negativos quando os resgates superarem os aportes. Não ocultar isso nem limitar artificialmente a zero. Consumo pode ficar negativo em um mês com estornos maiores que os gastos; mostrar com explicação.

### Agenda e janela de 30 dias

A referência mostra até dez movimentos previstos em aberto, inclusive vencidos, ordenados pela data mais antiga. O programa pode exibir dez no painel e oferecer “Ver todos”.

A planilha calcula pagamentos previstos entre acompanhamento e acompanhamento + 30 dias, inclusive, limitados ao horizonte. Isso cobre 31 datas de calendário quando ambas as pontas são contadas. Preservar a regra inicialmente e usar o rótulo explícito “Pagamentos de DD/MM a DD/MM”; se optar por exatamente 30 datas, registrar a mudança. Entram Conta, Diário, Cartão e Reserva; não entra complemento diário como obrigação cadastrada. Recebimentos não reduzem esse total de pagamentos.

## 9. Metas e dinheiro reservado

### Cadastro

- ID estável.
- Nome único para facilitar identificação.
- Valor objetivo > 0.
- Prazo em data de calendário.
- Saldo reservado inicial >= 0, inclusive zero informado explicitamente.

O saldo inicial da meta representa dinheiro já separado antes do início do planejamento. Não incluí-lo novamente no saldo disponível inicial.

### Cálculos

```text
saldo reservado atual = saldo inicial da meta
                      + reservas realizadas no período até o acompanhamento
                      − resgates realizados no mesmo intervalo

falta reservar = máximo(0, objetivo − saldo reservado atual)
progresso = saldo reservado atual / objetivo
meses restantes = máximo(1, diferença entre ano/mês do prazo e acompanhamento + 1)
aporte mensal sugerido = arredondar para cima em centavos(falta reservar / meses restantes)
```

O mês atual conta como uma oportunidade de aporte. Prazo vencido concentra o que falta em um mês. Não estimar rentabilidade, inflação ou rendimento de investimentos.

Situações: cadastro pendente, resgate excede reserva, concluída, prazo vencido, em andamento. Avaliar excesso de resgate antes de conclusão; concluída pode continuar concluída mesmo após o prazo.

Aportes previstos afetam o saldo disponível projetado, mas **não** aumentam o saldo reservado confirmado. Não misturar dinheiro já guardado com intenção de guardar. Uma projeção separada das metas é possível depois, claramente identificada.

Na referência, resgate excedente gera saldo reservado negativo e aviso. Para a primeira versão do programa, preservar o aviso de inconsistência e não corrigir por truncamento automático. Um bloqueio de resgate é uma decisão adicional a validar, pois exigiria considerar a disponibilidade da meta em cada data.

Ao renomear uma meta, preservar todos os vínculos por ID. Ao arquivar uma meta com movimentos, preservar o histórico. Metas vazias não existem no programa: não criar vinte metas nem vinte pendências apenas porque a planilha possuía vinte linhas preparadas.

## 10. Cartão de crédito: regime inicial e evolução

A metodologia usa regime de caixa. O dinheiro sai na data do pagamento da fatura/parcela, não necessariamente na data da compra.

Na primeira versão, registrar parcelas futuras como movimentos Cartão nas datas de pagamento. Identificação do cartão é um dado organizacional; ainda não implica gestão completa de fechamento e limite.

Há duas formas possíveis de representar a fatura:

1. Uma linha por parcela/item que compõe os pagamentos.
2. Uma linha pelo total da fatura.

Não somar as duas para a mesma obrigação. O formulário e a ajuda devem orientar a escolha. Para o MVP, priorizar parcelas individuais; o registro agregado permanece possível para uso manual consciente.

Não cadastrar a compra integral como Conta e também suas parcelas como Cartão. Não registrar como Diário um gasto que já será contabilizado na fatura.

Um módulo completo de cartões, com compras, fechamento, fatura, limite, pagamento parcial e crédito de estorno, é evolução futura. Se for implementado, modelar a conciliação antes de combiná-lo com o modo manual. Não criar uma gestão de faturas aparente que provoque dupla contagem.

Estorno recebido em dinheiro entra como Estorno na data do recebimento. Desconto dentro de uma fatura pode ser representado pelo ajuste de seu valor; não fazer simultaneamente os dois para a mesma recuperação.

## 11. Simulador de compras

Objetivo: comparar a projeção atual com uma compra hipotética, sem alterar lançamentos reais.

### Entradas e validações

- Preço à vista > 0.
- Entrada entre zero e o preço.
- Número inteiro de parcelas entre 1 e 36 na referência.
- Taxa efetiva mensal >= 0.
- Data da entrada, exigida quando entrada > 0.
- Data da primeira parcela, exigida quando houver valor financiado.
- Datas de pagamento iguais ou posteriores ao acompanhamento.
- Primeira parcela não anterior à entrada, quando houver entrada.

Não converter taxa anual em mensal sem uma opção e uma regra explícitas. A interface deve identificar claramente “juros ao mês”.

### Prestação e amortização

```text
principal P = preço − entrada
se P = 0: não existem parcelas financiadas
se taxa i = 0: prestação base = P / n
se i > 0: prestação base = P × i / (1 − (1+i)^(-n))
```

Arredondar prestação para centavos. Em cada parcela:

```text
juros = arredondar(dívida anterior × taxa mensal, 2 casas)
pagamento normal = mínimo(prestação base arredondada, dívida anterior + juros)
pagamento final = dívida anterior + juros restantes
amortização = pagamento − juros
dívida seguinte = dívida anterior − amortização
```

A última parcela ajusta os centavos para encerrar a dívida. Usar aritmética decimal apropriada; não aceitar resíduos binários como saldo de dívida. Total pago = entrada + parcelas. Juros totais = total pago − preço.

Para datas, somar meses à data original da primeira parcela, preservando seu dia quando existir e usando o último dia do mês quando não existir. Não derivar cada data da anterior de modo que 31/jan → 28/fev force 28/mar.

O modelo não inclui seguro, tarifas, IOF, taxas adicionais nem juros proporcionais por dias. Não apresentar o resultado como CET ou cotação de financiamento.

### Comparação

```text
pagamentos acumulados da compra até dia = entrada e parcelas até aquela data
saldo com compra no dia = saldo projetado original − pagamentos acumulados
```

Mostrar total pago, juros, cronograma, menor saldo com e sem compra, primeira data negativa e relação com o piso. Avaliar os mínimos futuros a partir da data de acompanhamento. Se já havia déficit antes da compra, dizer “O cenário já apresentava saldo negativo” e separar a piora causada pela simulação; não atribuir todo o problema à compra.

Se algum pagamento estiver fora do horizonte, avisar “Impacto parcial: há pagamentos após o fim da projeção” e informar o valor fora do intervalo. Não concluir que a compra cabe com base apenas na parcela visível do horizonte.

A referência exige transferência manual da simulação para os lançamentos. Para o programa, **proposta de melhoria**: botão “Adicionar ao planejamento”, com revisão das datas e valores antes de confirmar. Criar Entrada da compra como tipo Conta e parcelas como Cartão, todas Previsto. A operação deve ser atômica e identificada por um ID de grupo para não gerar duplicidade por clique repetido. Neste contexto, “entrada da compra” é pagamento inicial, não o tipo financeiro Entrada/receita.

Até a confirmação, o cenário deve permanecer separado do planejamento principal. Se a conversão ficar para a fase seguinte, dizer isso claramente.

## 12. Exemplos fictícios de referência matemática

Estes exemplos esclarecem as regras; não são dados do usuário e não precisam ser carregados na aplicação.

### A. Fluxo do dia

Saldo inicial R$ 1.000,00. No dia: receita R$ 500,00, conta R$ 200,00, Diário R$ 20,00, orçamento diário R$ 50,00, reserva R$ 100,00. Dia ainda estimado.

- Complemento diário: R$ 30,00.
- Resultado projetado: 500 − 200 − 20 − 30 − 100 = R$ 150,00.
- Saldo projetado: R$ 1.150,00.
- Se todos os lançamentos estão realizados, saldo confirmado: 1.000 + 500 − 200 − 20 − 100 = R$ 1.180,00.
- Encerrando o dia, o complemento desaparece e o saldo projetado passa a R$ 1.180,00.

### B. Realizado substitui previsão

Conta prevista de R$ 200,00 no dia 10, realizada por R$ 180,00 no dia 12. O movimento passa a afetar o dia 12 em R$ 180,00. Não permanecem R$ 200,00 no dia 10 e R$ 180,00 no dia 12.

### C. Reserva e resgate

Disponível inicial R$ 1.000,00; meta com R$ 300,00 iniciais. Reserva realizada de R$ 100,00 e resgate realizado de R$ 40,00. Disponível = R$ 940,00; meta = R$ 360,00. Soma dos dois = R$ 1.300,00. Não houve consumo nem renda nesses movimentos.

### D. Parcelamento sem juros

Preço R$ 100,00, entrada zero, três parcelas. Cronograma: R$ 33,33, R$ 33,33 e R$ 33,34. Total R$ 100,00 e dívida final zero.

### E. Mínimo diário importa

Saldo inicial R$ 100,00; conta de R$ 200,00 no dia 5; receita de R$ 300,00 no dia 10; sem demais movimentos ou estimativas. Saldo entre dias 5 e 9 = −R$ 100,00. Depois da receita = R$ 200,00. Fechar o mês positivo não elimina o déficit intermediário.

## 13. Telas e experiência de uso

### 13.1 Primeiro acesso

Apresentação curta: “Planeje seu saldo antes de gastar”. Solicitar mês inicial, saldo disponível inicial, data de acompanhamento, gasto diário padrão e piso de segurança. Permitir valores zero quando válidos e explicar o que exclui dinheiro das metas.

Após configurar, orientar cadastrar a próxima receita e os compromissos conhecidos. Sem lançamentos, exibir estado vazio útil. Não prometer saúde financeira apenas porque uma projeção incompleta está positiva.

### 13.2 Visão geral

- Quatro indicadores: disponível confirmado, menor saldo futuro com data, saldo ao final e total reservado confirmado.
- Faixa curta de pendências clicáveis, separada de alertas financeiros.
- Seletor de mês.
- Comparação mensal: realizado, projetado e diferença.
- Gráfico do saldo diário ou mensal com piso de segurança; permitir chegar ao detalhe diário.
- Próximos movimentos em aberto, incluindo vencidos.
- Ação principal evidente: “Novo lançamento”.

Não esconder saldo negativo por trás de uma pendência de cadastro. As duas informações podem coexistir, com indicação de que a projeção está incompleta.

### 13.3 Lançamentos

Tabela enxuta com data, descrição, tipo, situação, valor e ações. Campos adicionais podem aparecer no detalhe. Filtros por período, tipo, situação, categoria, meta, cartão e pendência; busca por descrição.

Ações principais: criar, editar, confirmar realização, cancelar e duplicar como novo previsto. Duplicar precisa de novo ID e revisão da data; não deve clonar silenciosamente o estado Realizado.

Formulário em modal ou painel lateral simples. Exibir campos de realização quando pertinentes e a meta quando o tipo exigir. Confirmação pode sugerir data e valor, mas o usuário deve poder ajustá-los.

### 13.4 Calendário / fluxo diário

Tabela navegável por mês, com data, entradas, consumo, reservas líquidas e saldo. Expansão por dia mostra componentes e lançamentos. Destacar acompanhamento, dias negativos e menores saldos. Manter a discriminação de complemento diário acessível sem transformar a tela inicial em uma planilha de dezesseis colunas.

### 13.5 Orçamento

Padrão diário e substituições mensais; teto opcional por mês. Exibir gasto diário registrado e estimativa adicional separadamente. Trocar o teto não muda os lançamentos nem limita artificialmente o gasto.

### 13.6 Metas

Nome, valor acumulado, objetivo, prazo, falta reservar e aporte sugerido. Ações de reservar/resgatar criam lançamentos vinculados; não alteram o saldo derivado da meta diretamente. Barra de progresso pode ser visualmente limitada a 100%, mas o valor e percentual reais precisam continuar acessíveis.

### 13.7 Simulador

Formulário compacto, resumo de custos, comparação do saldo e cronograma. Sem confirmação, nenhum dado financeiro do planejamento muda.

### 13.8 Configurações e dados

Parâmetros do planejamento, moeda BRL, datas em português do Brasil, exportação de backup e restauração. Ajuda curta sobre o método, especialmente saldo inicial, complemento diário e cartão.

## 14. Direção visual

A referência própria usa branco, cinza claro, azul sóbrio, verde e vermelho em funções específicas. É uma base possível, não obrigação de copiar a aparência de células.

| Uso | Cor de referência |
| --- | --- |
| Texto principal | #253349 |
| Títulos / navegação | #263F59 |
| Ação principal | #386AA5 |
| Reservas / situação favorável | #21765D |
| Déficit / erro | #B13B4B |
| Texto secundário | #68778A |
| Bordas | #DCE3EA |
| Fundo secundário | #F3F6F9 |

Interface natural, sóbria e legível. Preferir hierarquia de texto, alinhamento, espaços consistentes e tabelas bem organizadas. Evitar excesso de cartões, degradês decorativos, sombras fortes, emojis, ilustrações genéricas ou grandes áreas sem função.

Valores alinhados à direita, números com largura consistente, formatação `R$ 1.234,56`, datas `DD/MM/AAAA`. Status deve ter texto além da cor. Ações frequentes próximas dos dados; uso por teclado e foco visível. Componentes devem permanecer utilizáveis em tela estreita, mesmo que desktop seja a prioridade inicial proposta.

Não reproduzir marca, logotipo ou identidade do produto de referência. Nome provisório: **Minhas Finanças**. O nome definitivo ainda não foi escolhido.

## 15. Arquitetura proposta para iniciar

Esta seção é uma proposta de implementação, não uma exigência já definida pelo usuário.

### Ponto de partida

Aplicação web para rodar localmente, de usuário único, com interface em português. Se a pasta estiver vazia, uma opção simples é TypeScript, React e Vite para a interface, um serviço local em Node e SQLite para persistência. Se o ambiente já tiver outra base adequada, preferi-la e registrar a decisão. Não fixar versões com base neste documento; verificar as versões compatíveis no momento da inicialização.

Acesso local por padrão. Não publicar, abrir acesso externo ou adicionar infraestrutura em nuvem automaticamente. Se o programa for exposto externamente depois, autenticação e implantação passam a ser outro escopo.

Alternativa mais simples é persistência no navegador, mas precisa ser escolhida conscientemente: os dados ficam vinculados ao perfil/origem e podem ser removidos. Para histórico financeiro durável, a proposta principal usa banco local e backup explícito. Não usar apenas memória de componentes ou localStorage para todo o histórico sem deixar essa limitação clara.

### Separação mínima

- Domínio: dinheiro, datas, validações, projeção, orçamento, metas e simulação.
- Aplicação: casos de uso de cadastro, realização, cancelamento, backup e restauração.
- Persistência: repositórios e migrações de banco.
- Interface: telas, componentes e formatação.

Não é necessário criar microserviços, múltiplos pacotes ou uma arquitetura empresarial. As fronteiras acima podem existir dentro de um projeto pequeno.

### Modelo de dados sugerido

| Entidade | Campos essenciais |
| --- | --- |
| PlanningSettings | id, startMonth, openingAvailableCents, reviewDate, reviewDayClosed, defaultDailyBudgetCents, safetyFloorCents, horizonMonths, currency |
| MonthlyBudget | month único, dailyBudgetOverrideCents opcional, consumptionCapCents opcional |
| Transaction | id, description, type, status, plannedDate, plannedAmountCents, actualDate, actualAmountCents, categoryId opcional, cardId opcional, goalId opcional, notes, groupId opcional, createdAt, updatedAt |
| Goal | id, name, targetCents, deadline, openingReservedCents, archivedAt opcional |
| Category | id, name, archivedAt opcional |
| CardLabel | id, name, archivedAt opcional; apenas identificação no MVP |
| Simulation | Pode ser transitória; se salva: id, name, purchasePriceCents, downPaymentCents, installmentCount, monthlyRateDecimal, downPaymentDate, firstInstallmentDate |

Situação do negócio e validade são coisas diferentes. Não usar `Cancelado` para indicar erro de cadastro. Rascunhos e erros de importação podem existir separados dos movimentos ativos.

Saldos, indicadores e cronogramas são derivados. Não manter cópias independentes do saldo em várias tabelas. Se houver cache depois, definir invalidação e nunca usá-lo como origem do dinheiro.

### Precisão e calendário

- Valores monetários persistidos em centavos inteiros, com validação de faixa segura.
- Juros calculados com aritmética decimal e arredondamento explicitamente definido para centavos, preferencialmente meio para cima para valores não negativos.
- Taxa mensal armazenada com precisão própria, não como dinheiro.
- Datas financeiras armazenadas como `YYYY-MM-DD`, sem conversão de UTC que possa mudar o dia.
- Instantes de criação/edição separados das datas de vencimento e realização.
- Contexto de uso brasileiro; apresentação em `pt-BR`. Data sugerida de hoje deve considerar o fuso local adequado, sem substituir automaticamente a data de acompanhamento.
- Projeção de 24 meses gerada por calendário, não por um número fixo de 730 linhas. Considerar anos bissextos.

### Casos de uso essenciais

```text
salvar configuração
criar / editar / cancelar movimento
confirmar realização no mesmo movimento
listar movimentos com filtros
calcular projeção diária
obter painel e resumo do mês
salvar orçamento mensal
criar / editar / arquivar meta
calcular saldo e aporte da meta
simular compra sem alterar planejamento
exportar backup / restaurar backup
```

São operações conceituais; não é obrigatório transformar cada uma em um endpoint separado. A forma de acesso depende da arquitetura escolhida.

## 16. Persistência, backup e importação

Salvar os lançamentos de forma durável e refletir o resultado na interface. Falha ao salvar não pode aparecer como sucesso. Operações de várias parcelas devem ser atômicas.

Exportar backup JSON com `schemaVersion`, configurações, entidades e registros. Restauração valida estrutura, tipos, referências e versão antes de alterar os dados. Proposta inicial: restauração integral com resumo prévio, backup dos dados atuais e confirmação de substituição; não fazer mesclagem automática por nomes.

A importação da planilha é uma conveniência de fase posterior, não uma dependência para abrir o programa. Se implementada, mapear apenas entradas do usuário:

| Aba / área | Uso |
| --- | --- |
| Configurar B5:B9 | Mês inicial, saldo inicial, acompanhamento, diário padrão e piso |
| Configurar B11:B12 | Mês em análise e dia encerrado |
| Configurar A24:C47 | Mês, diário específico e teto mensal |
| Lançamentos A7:I1006 | Data prevista, descrição, tipo, categoria/cartão, situação, previsto, realizado, data realizada e meta |
| Metas A7:D26 | Nome, objetivo, prazo e saldo reservado inicial |
| Simulador B7:B12 | Preço, entrada, quantidade de parcelas, taxa mensal, data da entrada e primeira parcela |

Não importar fórmulas, caches, saldos derivados ou linhas vazias. A planilha une categoria/cartão em um campo; separar esses dados exige prévia de mapeamento, sem adivinhar qual texto é cartão. Vincular metas por correspondência inequívoca e sinalizar conflitos. Não executar macros ou código vindo de arquivos importados.

O programa não deve herdar os limites físicos de 1.000 lançamentos e 20 metas. O horizonte padrão de 24 meses e o limite inicial de 36 parcelas podem ser mantidos como escolhas de escopo, não como necessidade técnica.

## 17. Plano de implementação incremental

### Etapa 1 — Primeira versão executável

Inicializar projeto, persistência e documentação de execução. Implementar configuração, cadastro/edição/cancelamento/realização de movimentos, projeção diária e painel básico. Reabrir o programa deve manter os dados.

Critério de entrega: o usuário configura um saldo, cadastra receita e despesa, visualiza o impacto no dia correto, confirma com valor diferente e vê a previsão ser substituída.

### Etapa 2 — Completar o planejamento pessoal

Adicionar orçamento mensal, complemento diário, filtro de vencidos, pendências, possíveis duplicidades, resumo mensal e metas com reservas/resgates. Se o complemento já fizer parte do primeiro motor, apenas expor os controles nesta etapa.

Critério de entrega: painel, fluxo e metas concordam sobre o efeito dos mesmos movimentos; nenhum número é estático ou simulado sem identificação.

### Etapa 3 — Simular decisões

Adicionar simulador, juros, entrada, ajuste final de centavos, cronograma e comparação com o cenário atual. Opcionalmente, conversão explícita em lançamentos agrupados.

Critério de entrega: simular não modifica o planejamento, e custos fora do horizonte ficam visíveis.

### Etapa 4 — Uso continuado

Completar backup/restauração, ajuda e refinamentos de usabilidade. Backup deve entrar antes de o usuário depender do programa para uso real. Importação Excel, recorrências e módulo completo de cartões podem ser planejados depois.

Não interromper o trabalho a cada detalhe cosmético para pedir confirmação. Entregar porções funcionais e registrar o que ficou para depois.

## 18. Evoluções possíveis, fora do compromisso inicial

- Recorrências de salário, contas e assinaturas.
- Edição de uma ocorrência, próximas ocorrências ou série inteira.
- Contas bancárias separadas e transferências internas.
- Controle completo de compras, fechamento, faturas e limite de cartão.
- Pagamento parcial de obrigações com histórico de baixas.
- Projeção separada de saldo futuro das metas.
- Importação CSV/Excel assistida e conciliação.
- Múltiplos cenários salvos.
- Aplicação móvel, acesso por outros dispositivos e sincronização.

Recorrência não deve gerar o mesmo vencimento duas vezes ao abrir o sistema. Se adicionada, usar identificador de série/ocorrência e regra explícita para alterações e cancelamentos. Pagamento parcial exige um modelo próprio; o estado binário Previsto/Realizado da planilha não o resolve sozinho.

## 19. Decisões ainda abertas e padrão sugerido

| Questão | Padrão sugerido para não travar a inicialização |
| --- | --- |
| Plataforma final | Web local no navegador; desktop empacotado pode vir depois |
| Stack | Usar a existente; se não houver, a opção simples descrita na seção 15 |
| Nome definitivo | Minhas Finanças, provisório |
| Multiusuário / login | Fora da primeira versão local |
| Hospedagem / sincronização | Fora da primeira versão |
| Contas bancárias | Saldo consolidado |
| Cartão | Parcelas/pagamentos manuais por data |
| Recorrências | Cadastro manual ou duplicação assistida inicialmente |
| Pagamentos parciais | Fora do MVP; não fingir suporte |
| Resgate acima da reserva | Alertar conforme referência; bloqueio exige decisão adicional |
| Importação da planilha | Posterior à primeira versão funcional |
| Horizonte | 24 meses por padrão |

Somente perguntar ao usuário quando uma informação realmente bloquear a implementação ou mudar significativamente o produto. Registrar as escolhas reversíveis feitas por iniciativa técnica.

## 20. Cuidados para não perpetuar limitações da referência

- Não contar linhas vazias como metas pendentes ou lançamentos incompletos.
- Não replicar números de células ou intervalos fixos como estrutura de negócio.
- Não tratar cache de fórmula como verdade; recalcular a partir dos registros.
- Não converter ausência de saldo em zero silenciosamente.
- Não somar previsto e realizado do mesmo registro.
- Não somar orçamento diário inteiro por cima do Diário já lançado.
- Não somar compra, parcela e fatura como três saídas do mesmo dinheiro.
- Não classificar reserva como consumo nem resgate/estorno como renda.
- Não mostrar mínimo mensal como se fosse saldo de encerramento do mês.
- Não ocultar obrigações vencidas para deixar o cenário aparentemente positivo.
- Não ajustar resgate negativo ou dívida final com um máximo zero que esconda erro de cálculo.
- Não afirmar que uma compra é segura se despesas estiverem incompletas ou parcelas fora do horizonte.
- Não manter toda a lógica em componentes de tela, dificultando conferência e reutilização.

## 21. Conferência mínima e entrega ao usuário

O usuário fará a validação ampla. Para evitar entregar uma inicialização obviamente quebrada, basta uma passagem curta:

1. O projeto instala/inicia no ambiente escolhido e a tela principal abre.
2. Cadastro persiste e pode ser reaberto.
3. Conferir os exemplos A–D da seção 12 ou um pequeno conjunto equivalente das funções puras.
4. Verificar que cancelar zera o efeito e que simular não altera dados reais.

Não transformar isso em uma bateria extensiva. Se não for possível executar algo, declarar exatamente o que não foi executado. Não afirmar validação completa.

Entregar:

- Código funcional e organizado na pasta de trabalho.
- Instruções exatas para instalar, iniciar e localizar os dados.
- `README.md` com execução, escopo disponível e limitações.
- Um arquivo de continuidade, como `CLAUDE.md` ou `docs/CONTEXTO.md`, referenciando as regras e decisões efetivamente adotadas.
- Comandos de verificação executados e resultado, em poucas linhas.
- Pendências reais, sem apresentar funcionalidades futuras como prontas.

## 22. Resumo da missão para orientar a implementação

Construir um programa pessoal que projeta dinheiro disponível dia a dia, combinando saldo inicial, movimentos previstos e realizados, estimativa diária sem duplicação, reservas separadas e simulações de compra. O programa deve mostrar onde e quando o dinheiro pode faltar, explicar os números e permitir atualizar o planejamento rapidamente.

A prioridade é uma aplicação simples que funcione com dados persistidos e cálculos claros. O usuário quer começar a usar e testar; infraestrutura sofisticada e expansão de escopo ficam para depois.
