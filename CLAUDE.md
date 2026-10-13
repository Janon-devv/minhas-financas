# Minhas Finanças | Raul Janon

App pessoal de fluxo de caixa projetado dia a dia. Especificação completa: [docs/CONTEXTO.md](docs/CONTEXTO.md).
Decisões tomadas e divergências resolvidas: [docs/DECISOES.md](docs/DECISOES.md).

O documento de especificação chama o projeto de Radar do Saldo, que era o nome de trabalho.

## Regras do projeto

- O motor de domínio é **função pura**: regras R1 a R11 em `docs/CONTEXTO.md` seção 6, implementadas
  em `src/MinhasFinancas.Domain`. Sem I/O, sem `DateTime.Now`: o "hoje" entra por `IRelogio`.
- Os **testes dourados** em `tests/MinhasFinancas.Domain.Tests/MotorTests.cs` leem `docs/fixtures` e precisam
  continuar passando com igualdade exata em centavos. Se um deles quebrar, o motor está errado, não
  o fixture.
- Dinheiro é `long` em **centavos**. Formatação só na borda (`src/MinhasFinancas.Web/Formato.cs`).
- Datas são `DateOnly` locais, fuso America/Sao_Paulo. Nada de UTC que mude o dia.
- Interface em pt-BR, informal e direta, minimalista, com a paleta da seção 8 do CONTEXTO.
  **Não usar travessão (—) em nenhum texto de interface.**
- **Uma interface só, para celular e PC.** O celular define a estrutura; a partir de 900px o CSS
  transforma a barra de baixo em coluna lateral e abre os blocos em duas colunas. Para esconder algo
  no celular use `so-desktop`, que só existe como regra abaixo de 900px (assim no PC o elemento
  mantém o display natural dele); para esconder no PC use `so-celular`. Blocos que viram duas colunas
  usam `colunas`; grupos de quatro indicadores usam `painel-cartoes`.
- Sem nome, marca, imagem ou texto de produto de terceiros (seção 3 do CONTEXTO).

## Regras de interface que não podem regredir

Verificadas com medição, não no olho. Mudanças de UI precisam continuar respeitando:

- **Contraste.** Texto 4,5:1, contorno de campo 3:1. Por isso existem dois tokens de borda:
  `--linha` (#E5E7EB) é divisória decorativa; `--borda-campo` (#868E9B, 3,30:1) é contorno de
  campo de formulário, que a WCAG trata como componente de interface. Não unificar os dois.
  `--pilula-neutra-texto` existe porque #6B7280 sobre #E5E7EB dá 3,90:1 e reprova.
- **Alvo de toque 44px.** `--alvo-toque` em `.btn`, `.btn.pequeno`, `.campo` e `.toque-diario`.
  Um valor numérico pequeno dentro de tabela precisa virar `.toque-diario`, não um span clicável.
- **Nada clicável só com mouse.** Linha de tabela ou item de lista que responde a clique leva
  `clicavel` + `tabindex="0"` + `role="button"` + tratamento de Enter. Sem isso a função some
  para quem usa teclado.
- **Um `h1` por página.** A marca no topo é `span.marca-nome`, não `h1`. Páginas sem título
  visível usam `<h1 class="so-leitor">`, que `FocusOnNavigate` foca ao navegar.
- **Erro com `role="alert"`, confirmação com `aria-live="polite"`.** Borda vermelha sozinha não
  chega ao leitor de tela.
- **`prefers-reduced-motion`** está tratado globalmente no fim do `app.css`. Transições novas
  devem usar `--transicao` (180ms) para entrarem nessa regra.
- **Botão que grava desabilita enquanto grava.** No Plano, dois toques criariam dois lançamentos.

### Lacunas conhecidas

- Modais fecham por botão, por Cancelar e por toque fora, mas não pela tecla Esc, e não prendem
  o foco dentro deles.
- O gráfico não tem tooltip por ponto; os valores exatos ficam na tabela ao lado.
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

Antes de aceitar sugestão de componente, procure o que já existe: `Pilula`, `FarolTag`, `Icone`,
`CampoDinheiro`, `GraficoSaldo`, e as classes `bloco`, `cartao`, `painel-cartoes`, `colunas`,
`tabela`, `lista`. Reusar vence criar.

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
dotnet test                                              # 48 testes
dotnet run --project src/MinhasFinancas.Web --launch-profile local # http://localhost:5080
dotnet run --project src/MinhasFinancas.Web --launch-profile rede  # 0.0.0.0:5080, para o iPhone
dotnet dotnet-ef migrations add <Nome> --project src/MinhasFinancas.Infra --startup-project src/MinhasFinancas.Infra --output-dir Migracoes
```

## Contexto do dono

- Usa **iPhone**: a interface é mobile first e o app roda no PC servindo a rede local em HTTP.
  Cuidados de Safari já aplicados: `100dvh`, `safe-area-inset`, campos com 16px para não dar zoom ao
  focar, `inputmode="decimal"` nos campos de dinheiro, meta tags de web app e modal de reconexão
  traduzido (a conexão cai quando a tela do iPhone apaga).
- Pediu **poucos testes**: cobertura concentrada no motor e na leitura de valores digitados. Não
  encher o projeto de teste de interface.
