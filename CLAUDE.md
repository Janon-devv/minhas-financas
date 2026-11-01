# Minhas Finanças | Raul Janon

App pessoal de fluxo de caixa projetado dia a dia. Especificação completa: [docs/CONTEXTO.md](docs/CONTEXTO.md).
Decisões tomadas e divergências resolvidas: [docs/DECISOES.md](docs/DECISOES.md).

O documento de especificação chama o projeto de Radar do Saldo, que era o nome de trabalho.

## Regras do projeto

- O motor de domínio é **função pura**: regras R1 a R17 em `docs/CONTEXTO.md` seção 6, implementadas
  em `src/MinhasFinancas.Domain`. Sem I/O, sem `DateTime.Now`: o "hoje" entra por `IRelogio`.
- Os **testes dourados** em `tests/MinhasFinancas.Domain.Tests/MotorTests.cs` leem `docs/fixtures` e precisam
  continuar passando com igualdade exata em centavos. Se um deles quebrar, o motor está errado, não
  o fixture.
- Dinheiro é `long` em **centavos**. Formatação só na borda (`src/MinhasFinancas.Web/Formato.cs`).
- Datas são `DateOnly` locais, fuso America/Sao_Paulo. Nada de UTC que mude o dia.
- Interface em pt-BR, informal e direta, minimalista, com a paleta da seção 8 do CONTEXTO.
  **Não usar travessão (—) em nenhum texto de interface.**
- **Uma interface só, para celular e PC.** O celular define a estrutura (desenhada em 390px); a
  partir de 900px o CSS transforma a barra de baixo em coluna lateral e abre os blocos em duas
  colunas. No PC a coluna é a identidade do app: petróleo fundo (`--lateral-fundo`), anéis do
  radar do ícone ao fundo, seções "Dia a dia" e "Planejar", Ajustes embaixo, e a página aberta
  marcada como o ponto no radar (ícone e ponto em `--radar`). O último item do rodapé recolhe a
  coluna a só ícones; a escolha fica no navegador (`lateral-recolhida`, em `app.js` e no script
  do `<head>` do `App.razor`). O conteúdo ocupa a largura toda.
  Para esconder algo no celular use `so-desktop`, que só existe como regra abaixo de 900px
  (assim no PC o elemento mantém o display natural dele); para esconder no PC use `so-celular`.
  Blocos que viram duas colunas usam `colunas`.
- **Toda página começa por `<Cabecalho>`**, em petróleo. A barra de status do iPhone é
  `black-translucent` e fica sobre ele; a `.faixa-status` cobre essa área quando o conteúdo rola.
  Página sem cabeçalho deixaria o relógio branco sobre fundo claro. Destaque grande (`destaque`)
  só em Hoje e Calendário; o resto usa o cabeçalho compacto. No PC, dentro do layout com coluna
  (`.com-lateral`), o mesmo `<Cabecalho>` fica claro: título grande sobre o fundo e ações à
  direita; o petróleo vai só para o cartão do número principal (`.hero`), com os indicadores num
  cartão branco ao lado. Botão `sobre-escuro`, seletor de mês e abas ganham versão clara ali.
- **Texto curto na tela, explicação no (i).** Regra que muda dinheiro (valor por ocorrência, item já
  no saldo inicial, renda variável que não mexe no saldo, conta vencida fora da previsão) continua
  visível em uma linha; o porquê vai em `Rotulo Ajuda=` ou `BotaoAjuda`. Um (i) por bloco, não um
  por número.
- Sem nome, marca, imagem ou texto de produto de terceiros (seção 3 do CONTEXTO).

## Regras de interface que não podem regredir

Verificadas com medição, não no olho. Mudanças de UI precisam continuar respeitando:

- **Contraste.** Texto 4,5:1, contorno de campo 3:1. Por isso existem dois tokens de borda:
  `--linha` (#E5E7EB) é divisória decorativa; `--borda-campo` (#868E9B, 3,30:1) é contorno de
  campo de formulário, que a WCAG trata como componente de interface. Não unificar os dois.
  `--pilula-neutra-texto` existe porque #6B7280 sobre #E5E7EB dá 3,90:1 e reprova.
- **Alvo de toque 44px.** `--alvo-toque` em `.btn`, `.btn.pequeno`, `.btn-icone`, `.filtro`,
  `.botao-ajuda` e nas abas; `.campo` tem 48px. O (i) usa margem negativa para ter 44px de toque
  sem esticar a linha do rótulo.
- **Nada clicável só com mouse.** Linha de lista que responde a toque é um `<button class="item">`
  ou `<a class="item">` de verdade (no calendário, `button.dia`), nunca `li` com `@onclick`. Assim
  teclado e leitor de tela funcionam sem `tabindex` nem tratamento de Enter.
- **Um `h1` por página.** É o `Titulo` do `<Cabecalho>`, que `FocusOnNavigate` foca ao navegar.
  A marca "Minhas Finanças" é `span.marca`, não `h1`.
- **Modal é `<Modal>`.** Fecha por X, Esc e toque fora, prende o foco, devolve o foco ao fechar e
  põe as ações no `Rodape`, que fica fixo acima do teclado (`app.js` mede a área visível).
- **Excluir pede um segundo toque** ("Excluir" vira "Excluir de vez").
- **Erro com `role="alert"`, confirmação com `aria-live="polite"`.** Borda vermelha sozinha não
  chega ao leitor de tela.
- **`prefers-reduced-motion`** está tratado globalmente no fim do `app.css`. Transições novas
  devem usar `--transicao` (180ms) para entrarem nessa regra.
- **Botão que grava desabilita enquanto grava, e o handler confere a trava na primeira linha**
  (`if (... || _salvando) return;`). Só o `disabled` não basta: ele chega ao iPhone depois de uma
  ida e volta pela rede, e um toque duplo passa. Vale para salvar, excluir, pagar e restaurar.
- **Modal fecha por Esc com o foco onde estiver.** O `Modal.razor.js` ouve o documento, porque o
  elemento focado pode sumir (excluir uma linha da lista dentro do modal) e o foco cair no body.
- **Erro inesperado fica na tela.** O `MainLayout` tem um `ErrorBoundary` com "Tentar de novo";
  ele não substitui tratar o erro esperado (nome repetido, validação) com mensagem.

## Regras de dados e segurança

- **Validação no domínio, com faixa.** `Validacao` recusa data fora de 2000 a 2100, valor acima de
  R$ 9.999.999.999,99, juros acima de 100% ao mês e mais de 360 repetições. Data ou valor absurdo
  não é só número errado: já travou o app inteiro (juros de uma fatura de 2006 estourando o long).
- **Nome único volta como mensagem.** `Salvar{Cartao,Tag,Meta,Carteira}Async` devolve `string?`
  com o motivo; a tela mostra no `Aviso`. Comparação sem diferenciar maiúsculas.
- **Ocorrências de um lançamento saem de `Ocorrencias.Do`.** Ele aplica a data efetiva e o dia de
  referência (vencimento do cartão). Chamar `Ocorrencias.Ate` direto com a data efetiva de uma
  compra no cartão erra o dia quando o vencimento é 29 a 31.
- **Fatura e compra se casam por `Faturas.Chave(fatura, cartoes)`**, o ciclo calculado, nunca pela
  data digitada na fatura. Cada pagamento de fatura é um `PagamentoFatura` com a própria data.
- **Migração gerada pelo EF precisa ser lida antes de rodar.** Na de pagamentos ele apagava as
  colunas antes de copiar os dados. Confira com `dotnet dotnet-ef migrations script <anterior> <nova>`
  e teste numa cópia de um banco no formato antigo. O app copia o banco antes de migrar.
- **Backup automático** (`CopiaDeSeguranca`, `CopiaDiaria`): cópia diária com `VACUUM INTO`, 30
  dias, na pasta de backups do banco em uso. Campo novo de backup sobe `BackupService.VersaoSchema`
  e arquivo antigo tem que continuar abrindo.
- **Rede.** `Seguranca/ProtecaoDeRede` roda antes de tudo: cabeçalhos, filtro de Host (só
  localhost, IP e nome do PC; extras em `MinhasFinancas:HostsPermitidos`) e PIN para quem não é
  este computador. Endpoint ou arquivo novo fica atrás do PIN automaticamente; só entra em
  `LiberadosSemPin` o que a tela de entrada precisa e não tem dado. O perfil `rede` roda em
  Production.

### Lacunas conhecidas

- O gráfico mostra o valor do ponto só ao passar o mouse (`<title>` do SVG); no toque não há
  tooltip, e os valores que importam estão escritos ao lado.
- Só tema claro. O `app.css` já é todo baseado em tokens, então um tema escuro seria um bloco
  `prefers-color-scheme` redefinindo as variáveis.

## Skills de design: quem manda quando o conselho conflita

Instaladas em `~/.claude/skills`: `frontend-design`, `ui-ux-pro-max`, `web-design-guidelines`,
`webapp-testing`, `playwright-cli`. São conselhos genéricos de web; aqui já existem decisões
tomadas. Quando bater conflito, a ordem de precedência é:

1. `docs/CONTEXTO.md` (paleta da seção 8, nomes, regras de produto)
2. A seção "Regras de interface que não podem regredir" deste arquivo
3. O que já está em `wwwroot/app.css` e nos componentes de `Components/Compartilhado`
4. A skill

Conflitos já decididos, não reabrir sem o dono pedir:

- **Fonte.** Skill costuma sugerir Google Fonts. Não. O app roda em HTTP na rede local, às vezes
  sem internet, e a pilha de fontes do sistema já dá SF Pro no iPhone.
- **Paleta e tema escuro.** A paleta é a da seção 8 do CONTEXTO. Tema escuro, se vier, é um bloco
  `prefers-color-scheme` redefinindo os tokens, não paleta nova.
- **Tailwind, shadcn/ui, React, qualquer `npm`.** Não existe `package.json` e não vai existir.
  CSS é um arquivo só com tokens em `:root`; componente novo é `.razor` em `Components/Compartilhado`.
- **Padrão de landing page** (hero, prova social, CTA enorme). Não se aplica: é app de uso diário,
  abre direto no dado.
- **Animação.** Só com `--transicao`, senão escapa da regra de `prefers-reduced-motion`.

Antes de aceitar sugestão de componente, procure o que já existe em `Components/Compartilhado`:
`Cabecalho`, `Grupo`, `Valor` (dinheiro com centavos menores no destaque), `Rotulo` e `BotaoAjuda`
(ajuda contextual), `Aviso`, `Vazio`, `Carregando`, `IconeTipo`, `SeletorMes`, `Modal`, `FarolTag`,
`Pilula`, `Icone` (uma família só: traço 1,8, grade 24), `CampoDinheiro`, `CampoData`,
`GraficoSaldo`. Classes: `lista`/`item`, `linhas`/`linha` (conta aberta), `indicadores`,
`hero-indicadores`, `segmentado` (radios), `interruptor`, `detalhes` (mais opções), `filtros`,
`abas`, `dias`, `proporcao`, `colunas`. Reusar vence criar.

### Como verificar antes de dar por pronto

```bash
dotnet build src/MinhasFinancas.Web          # sempre antes de subir
dotnet run --project src/MinhasFinancas.Web --launch-profile local
playwright-cli resize 390 844 && playwright-cli goto http://localhost:5080/calendario
playwright-cli screenshot                     # e de novo em 1440 900
```

Subir com `--no-build` sem ter compilado serve estático quebrado e dá 500 falso em
`blazor.web.js` e no CSS: a tela parece outra e a conclusão sai errada. Para checar cor ou
tamanho real, `playwright-cli eval` lendo `getComputedStyle`, não o olho na imagem.
`webapp-testing` e `playwright-cli` servem para olhar, não para encher o repositório de teste de
interface (ver "Contexto do dono"). A pasta `.playwright-cli/` é lixo de verificação e está no
`.gitignore`.

## Duas armadilhas dos fixtures

1. Em `esperado_calendario.csv` a coluna `saidas_centavos` é a saída **total** do dia: cartão e
   economias já estão dentro dela. As colunas `cartao_centavos` e `economias_centavos` são
   detalhamento, não parcelas a somar.
2. Em `esperado_mensal.csv` é diferente: `saidas_e_cartao_centavos` **exclui** economias.

No código, `DiaProjetado.SaidaTotalCentavos` corresponde à primeira e
`ResumoMes.SaidasECartaoCentavos` à segunda.

## Comandos

```bash
dotnet test                                              # 81 testes
dotnet run --project src/MinhasFinancas.Web --launch-profile local # http://localhost:5080
dotnet run --project src/MinhasFinancas.Web --launch-profile rede  # 0.0.0.0:5080, para o iPhone (pede PIN)
dotnet dotnet-ef migrations add <Nome> --project src/MinhasFinancas.Infra --startup-project src/MinhasFinancas.Infra --output-dir Migracoes
```

## Contexto do dono

- Usa **iPhone**: a interface é mobile first e o app roda no PC servindo a rede local em HTTP.
  Cuidados de Safari já aplicados: `100dvh`, `safe-area-inset`, campos com 16px para não dar zoom ao
  focar, `inputmode="decimal"` nos campos de dinheiro, meta tags de web app e modal de reconexão
  traduzido (a conexão cai quando a tela do iPhone apaga).
- Pediu **poucos testes**: cobertura concentrada no motor e na leitura de valores digitados. Não
  encher o projeto de teste de interface.
