# Minhas Finanças | Raul Janon

App pessoal de fluxo de caixa projetado. Responde uma pergunta todo dia: **como vai estar meu saldo
em cada dia daqui pra frente, e qual é o pior dia que vem aí?**

Roda local, um usuário, sem login e sem nuvem. Os dados ficam num arquivo SQLite no seu computador.

## Como rodar

Precisa do **.NET SDK 10** (`dotnet --version` deve mostrar 10.x).

### No computador

```bash
dotnet run --project src/MinhasFinancas.Web --launch-profile local
```

Abre em <http://localhost:5080>.

### No iPhone, pela rede de casa

```bash
dotnet run --project src/MinhasFinancas.Web --launch-profile rede
```

No iPhone, conectado ao mesmo Wi-Fi, abra `http://192.168.1.31:5080`
(troque pelo IP do seu PC, que você descobre com `ipconfig` ou com o comando abaixo).

```powershell
Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.IPAddress -notlike '127.*' }
```

Na primeira vez o Windows provavelmente vai bloquear a porta. Libere uma vez só, num PowerShell
como administrador:

```powershell
New-NetFirewallRule -DisplayName "Minhas Finanças" -Direction Inbound -LocalPort 5080 -Protocol TCP -Action Allow -Profile Private
```

**Para ficar com cara de app no iPhone:** abra o endereço no Safari, toque em compartilhar e depois
em "Adicionar à Tela de Início". Ele passa a abrir em tela cheia, com ícone próprio e sem a barra
do navegador.

Duas coisas para saber sobre o uso no celular:

- É HTTP, não HTTPS. De propósito: um certificado autoassinado faria o Safari reclamar em toda
  abertura. Como só trafega na sua rede local, tudo bem. O custo é que não dá para ter modo offline
  (service worker exige HTTPS).
- Quando a tela do iPhone apaga, a conexão com o servidor cai. Ao voltar, aparece "Reconectando..."
  por um instante e a tela volta de onde estava. Se o PC tiver sido desligado nesse meio tempo, é só
  recarregar.

## Onde ficam seus dados

```
%LOCALAPPDATA%\MinhasFinancas\financas.db
```

Fica fora do repositório de propósito: recompilar, trocar de branch ou limpar a pasta nunca
encosta no seu histórico financeiro. O caminho exato aparece na tela de Ajustes.

Para usar outro arquivo (um banco de testes, por exemplo):

```bash
# PowerShell
$env:MinhasFinancas__CaminhoBanco = "C:\caminho\teste.db"; dotnet run --project src/MinhasFinancas.Web
```

## Backup

Em **Ajustes > Backup** você baixa um JSON com tudo: configuração, lançamentos, diários, cartões,
tags e metas. Faça isso antes de passar a depender do app de verdade.

A restauração é integral e substitui tudo. Ela primeiro mostra o que tem no arquivo e só mexe no
banco depois que você confirma. Antes de substituir, ela salva sozinha o estado atual em
`%LOCALAPPDATA%\MinhasFinancas\backups\`.

## Layout

A mesma aplicação se adapta ao tamanho da tela, sem versão separada:

- **No celular:** navegação em barra fixa embaixo (Hoje, Calendário, Lançar, Resumo, Ajustes),
  cabeçalho verde-petróleo em toda tela, blocos empilhados, respeitando as áreas seguras do
  aparelho. Metas e Simular compra ficam nas ações rápidas da tela Hoje.
- **No PC:** a navegação vira uma coluna lateral à esquerda (com Metas e Simular compra) e os
  blocos se organizam em duas colunas: na tela Hoje o gasto do dia e as contas ficam ao lado dos
  outros saldos e dos próximos dias, no Resumo a composição fica ao lado do saldo, nos Ajustes a
  conferência fica ao lado dos cadastros. No calendário, entrada, saída e diário viram colunas.

A troca acontece em 900px de largura.

## O que já funciona

| Tela | O que faz |
|---|---|
| **Hoje** | Saldo previsto hoje, quanto pode gastar, menor saldo com data, saldo após as contas do mês, gasto de hoje, contas do mês para confirmar, outros saldos, próximos 7 dias e o aviso mais grave |
| **Calendário** | Resumo do mês e um dia por linha (entrada, saída, diário, saldo), com detalhe e gasto do dia numa folha |
| **Lançar** | Receitas e despesas (Entrada, Saída, Cartão e Economia, com repetição mensal ou semanal, parcelas, tags, certeza da entrada e meta), renda variável do mês e faturas de cartão |
| **Resumo** | Resultado do mês, entradas contra saídas, para onde foi o dinheiro, custo de vida, economizado, tags, saldo do mês com gráfico e os próximos 12 meses |
| **Simular compra** | Compra à vista ou parcelada, com entrada e juros ao mês, comparando as duas curvas de saldo |
| **Metas** | Objetivo, prazo, progresso, quanto guardar por mês e aviso quando as economias lançadas não alcançam o objetivo |
| **Ajustes** | Conferir saldo com o banco, saldo e previsão, carteiras, cartões, tags, backup e restauração |

O motor de cálculo implementa as regras R1 a R11 de [docs/CONTEXTO.md](docs/CONTEXTO.md) seção 6 e
bate 100% com o cenário conferido em `docs/fixtures`.

## O que ainda não existe

- Importar a planilha `Radar_do_Saldo_Janon.xlsx` ou extrato OFX/CSV.
- Lembrete diário de check-in e modo offline.
- Simulador calculando sozinho o vencimento da fatura quando a compra é no cartão (hoje você informa
  a data em que a parcela cai).
- Converter uma simulação em lançamentos de verdade com um botão.
- Várias contas bancárias, login, sincronização.

## Testes

```bash
dotnet test
```

São 48 testes: os dourados comparam o motor dia a dia com `docs/fixtures` (811 dias e 12 meses, em
centavos exatos), mais os casos de borda das regras e a leitura de valores digitados.

## Cenário de exemplo

Para ver o app cheio de dados sem digitar nada, use um banco separado e carregue o cenário fictício
dos fixtures:

```bash
dotnet run tools/semear-cenario.cs -- "C:\caminho\exemplo.db"
$env:MinhasFinancas__CaminhoBanco = "C:\caminho\exemplo.db"; dotnet run --project src/MinhasFinancas.Web
```

Esses dados são inventados, só para conferência de tela. Nunca aponte o semeador para o banco onde
estão seus dados de verdade: ele apaga o que encontra antes de carregar.

## Estrutura

```
src/MinhasFinancas.Domain/   motor puro: regras R1 a R11, sem banco, sem tela, sem ler a data do sistema
src/MinhasFinancas.Infra/    EF Core + SQLite, migrações, serviços de planejamento e backup
src/MinhasFinancas.Web/      Blazor Server, interface em português, mobile first
tests/              xUnit: testes dourados contra os fixtures e casos de borda
tools/              utilitários: gerador de ícones e semeador do cenário de exemplo
docs/               especificação, fixtures, planilha de referência e decisões
```

## Origem

O documento de especificação em `docs/CONTEXTO.md` chama o projeto de Radar do Saldo, que era o nome
de trabalho. O nome definitivo é **Minhas Finanças | Raul Janon**.

A metodologia segue a lógica publicamente documentada de fluxo de caixa projetado descrita em
[docs/CONTEXTO.md](docs/CONTEXTO.md). Nome, código, regras detalhadas e identidade visual são
próprios. Nenhum material, marca ou arquivo de produto de terceiros foi usado.
