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

O dono pediu o mínimo: "eu testo". Ficaram 48 testes, concentrados onde um erro passa despercebido:

- 6 dourados, comparando o motor com `docs/fixtures` dia a dia e mês a mês, em centavos exatos.
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
