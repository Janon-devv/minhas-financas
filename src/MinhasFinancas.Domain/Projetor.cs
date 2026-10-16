using MinhasFinancas.Domain.Modelo;
using MinhasFinancas.Domain.Resultado;

namespace MinhasFinancas.Domain;

/// <summary>
/// O motor. Funcao pura: as mesmas entradas dao sempre o mesmo resultado, sem I/O e sem ler
/// a data do sistema. Implementa as regras R1 a R11 de docs/CONTEXTO.md secao 6 e recalcula
/// o horizonte inteiro a cada chamada, porque sao poucas centenas de lancamentos em ~730 dias.
/// </summary>
public static class Projetor
{
    /// <summary>
    /// R13: projeta a conta corrente e, depois, cada carteira separada. Sao projecoes
    /// independentes de proposito: o dinheiro do vale-alimentacao nao cobre o aluguel, entao
    /// somar os saldos num numero so esconderia exatamente o aperto que o app existe para mostrar.
    /// </summary>
    public static ResultadoProjecao Projetar(EntradaProjecao entrada)
    {
        var carteiras = Carteiras.Resolver(entrada.Carteiras, entrada.Configuracao);
        var principal = Nucleo(entrada, carteiras[0]);

        if (carteiras.Count == 1)
        {
            return principal;
        }

        // A simulacao de compra pergunta se cabe na conta corrente, entao nao se repete nas outras.
        var semSimulacao = entrada with { Simulacao = null };

        var outras = carteiras
            .Skip(1)
            .Select(carteira =>
            {
                var projecao = Nucleo(semSimulacao, carteira);
                return new ProjecaoCarteira(carteira, projecao.Dias, projecao.Agora);
            })
            .ToList();

        return principal with { Carteiras = outras };
    }

    private static ResultadoProjecao Nucleo(EntradaProjecao entrada, Carteira carteira)
    {
        var cfg = Carteiras.ConfiguracaoDe(carteira, entrada.Configuracao);

        var lancamentos = entrada.Lancamentos
            .Where(l => Carteiras.Da(l.CarteiraId) == carteira.Id)
            .ToList();

        var diarios = DiariosDa(entrada, carteira);

        // R16: so os ciclos COM fatura cadastrada trocam as compras soltas pelo total dela.
        // Cartao sempre sai da conta corrente, entao as outras carteiras nem olham para isso.
        var ciclosComFatura = carteira.Id == Carteiras.IdPrincipal
            ? entrada.Faturas.Select(Faturas.Chave).ToHashSet()
            : [];

        var inicio = cfg.DataAncora;
        var fim = entrada.FimHorizonte ?? Horizonte.Calcular(entrada.Hoje, inicio, cfg.HorizonteMeses);

        if (fim < inicio)
        {
            fim = inicio;
        }

        var totalDias = fim.DayNumber - inicio.DayNumber + 1;
        var cartoes = entrada.Cartoes.ToDictionary(c => c.Id);
        var situacoes = entrada.Situacoes.ToDictionary(x => (x.LancamentoId, x.Data));
        var nomesTags = entrada.Tags.ToDictionary(t => t.Id, t => t.Nome);
        var avisos = new List<string>();

        var entradas = new long[totalDias];
        var saidas = new long[totalDias];
        var cartao = new long[totalDias];
        var economias = new long[totalDias];
        var incertasFora = new long[totalDias];
        var movimentos = new List<MovimentoDoDia>?[totalDias];

        // Tags por mes (R9), acumuladas junto com a varredura dos lancamentos.
        var tagsPorMes = new Dictionary<(int Ano, int Mes), Dictionary<int, long>>();

        foreach (var lancamento in lancamentos)
        {
            if (lancamento.ValorCentavos <= 0)
            {
                continue;
            }

            if (lancamento.Tipo == TipoLancamento.Cartao &&
                (lancamento.CartaoId is null || !cartoes.ContainsKey(lancamento.CartaoId.Value)))
            {
                avisos.Add($"\"{lancamento.Descricao}\" é compra no cartão, mas não tem cartão válido. " +
                           "Por enquanto, conta na data da compra.");
            }

            var fora = lancamento.EntradaIncerta && !cfg.ContarEntradasIncertas;
            var dataEfetiva = Ocorrencias.DataEfetiva(lancamento, cartoes);

            foreach (var data in Ocorrencias.Ate(dataEfetiva, lancamento.Vezes, fim, lancamento.Periodicidade))
            {
                // R12: o que foi registrado sobre esta ocorrencia manda sobre o padrao.
                situacoes.TryGetValue((lancamento.Id, data), out var situacao);

                // Pulada nao aconteceu: nao e divida nem dinheiro que saiu, some da conta.
                if (situacao?.Pulada == true)
                {
                    continue;
                }

                // R14: com fatura cadastrada para o ciclo, quem desconta e o total dela. Contar
                // a compra aqui tambem seria a dupla contagem que o CONTEXTO proibe desde a
                // primeira versao.
                if (lancamento.Tipo == TipoLancamento.Cartao &&
                    lancamento.CartaoId is int idDoCartao &&
                    ciclosComFatura.Contains((idDoCartao, data)))
                {
                    continue;
                }

                var valor = situacao?.ValorCentavos ?? lancamento.ValorCentavos;
                var conta = data;
                var atrasada = false;

                if (situacao?.Pago == true)
                {
                    // Saiu de verdade. Se saiu antes da ancora, ja esta dentro do saldo de partida
                    // e contar de novo tiraria o dinheiro duas vezes.
                    conta = situacao.PagoEm ?? data;

                    if (conta < inicio)
                    {
                        continue;
                    }
                }
                else if (data < inicio)
                {
                    // Venceu antes da ancora. Sem registro, o padrao e assumir que saiu: o saldo da
                    // ancora veio do extrato e ja reflete o que aconteceu. Com registro dizendo que
                    // nao saiu, o dinheiro ainda esta na conta e a saida ainda vai acontecer, entao
                    // ela e trazida para o primeiro dia projetado em vez de desaparecer.
                    if (situacao is null)
                    {
                        continue;
                    }

                    conta = inicio;
                    atrasada = true;
                }

                if (conta > fim)
                {
                    continue;
                }

                var i = conta.DayNumber - inicio.DayNumber;

                if (fora)
                {
                    // R3: entrada incerta fica fora do saldo, mas o mes mostra quanto deixou de contar.
                    incertasFora[i] += valor;
                    continue;
                }

                switch (lancamento.Tipo)
                {
                    case TipoLancamento.Entrada:
                        entradas[i] += valor;
                        break;
                    case TipoLancamento.Saida:
                        saidas[i] += valor;
                        break;
                    case TipoLancamento.Cartao:
                        cartao[i] += valor;
                        break;
                    case TipoLancamento.Economia:
                        economias[i] += valor;
                        break;
                }

                (movimentos[i] ??= []).Add(new MovimentoDoDia(
                    lancamento.Id,
                    lancamento.Descricao,
                    lancamento.Tipo,
                    valor,
                    lancamento.TagId,
                    lancamento.CartaoId,
                    lancamento.MetaId,
                    situacao?.Pago == true,
                    atrasada));

                if (lancamento.Tipo != TipoLancamento.Entrada)
                {
                    // A chave 0 guarda o que saiu sem tag nenhuma.
                    AcumularTag(tagsPorMes, conta, lancamento.TagId ?? 0, valor);
                }
            }
        }

        // R14 e R15: cada fatura entra como um gasto so, no dia em que o dinheiro sai.
        if (ciclosComFatura.Count > 0)
        {
            foreach (var resumo in Faturas.Resumir(entrada.Faturas, entrada.Lancamentos, entrada.Cartoes, entrada.Hoje))
            {
                // A parte ja paga so conta se saiu a partir da ancora: antes dela, o saldo de
                // partida ja esta sem esse dinheiro.
                if (resumo.PagoCentavos > 0 &&
                    resumo.Fatura.PagoEm is DateOnly pagoEm &&
                    pagoEm >= inicio && pagoEm <= fim)
                {
                    var iPago = pagoEm.DayNumber - inicio.DayNumber;
                    cartao[iPago] += resumo.PagoCentavos;
                    (movimentos[iPago] ??= []).Add(new MovimentoDoDia(
                        0, $"Fatura {resumo.NomeCartao}", TipoLancamento.Cartao,
                        resumo.PagoCentavos, null, resumo.Fatura.CartaoId, null, true, false));
                    AcumularTag(tagsPorMes, pagoEm, 0, resumo.PagoCentavos);
                }

                if (resumo.ASairCentavos <= 0)
                {
                    continue;
                }

                // R15: fatura vencida antes da ancora ainda vai sair, com os juros do atraso.
                // Deixar no vencimento passado a tiraria da projecao, e a divida sumiria da tela.
                var quando = Calendario.Maior(resumo.Fatura.Vencimento, inicio);

                if (quando > fim)
                {
                    continue;
                }

                var iFatura = quando.DayNumber - inicio.DayNumber;
                cartao[iFatura] += resumo.ASairCentavos;

                (movimentos[iFatura] ??= []).Add(new MovimentoDoDia(
                    0,
                    resumo.EmAtraso
                        ? $"Fatura {resumo.NomeCartao} (em atraso)"
                        : $"Fatura {resumo.NomeCartao}",
                    TipoLancamento.Cartao,
                    resumo.ASairCentavos,
                    null,
                    resumo.Fatura.CartaoId,
                    null,
                    false,
                    resumo.EmAtraso));

                AcumularTag(tagsPorMes, quando, 0, resumo.ASairCentavos);
            }
        }

        // R5: percorre o calendario uma vez, acumulando o saldo.
        var dias = new List<DiaProjetado>(totalDias);
        var saldo = cfg.SaldoAncoraCentavos;

        for (var i = 0; i < totalDias; i++)
        {
            var data = inicio.AddDays(i);
            var temReal = diarios.TryGetValue(data, out var diarioReal);
            // R4: registro do dia manda, inclusive zero. Sem registro, o previsto so entra
            // se a contagem estiver ligada; desligada, o dia vale zero e so desce o que for
            // registrado de fato.
            var previsto = cfg.DiarioPrevistoAtivo ? cfg.DiarioPrevistoCentavos : 0;
            var diario = temReal ? diarioReal : previsto;

            saldo += entradas[i] - saidas[i] - cartao[i] - economias[i] - diario;

            dias.Add(new DiaProjetado
            {
                Data = data,
                EntradasCentavos = entradas[i],
                SaidasCentavos = saidas[i],
                CartaoCentavos = cartao[i],
                EconomiasCentavos = economias[i],
                DiarioCentavos = diario,
                DiarioEReal = temReal,
                SaldoCentavos = saldo,
                Farol = FarolDe(saldo, cfg.SaldoConfortoCentavos),
                Movimentos = movimentos[i] ?? []
            });
        }

        var agora = MontarAgora(dias, cfg, entrada.Hoje, inicio, fim);
        // R17: ganho variavel e da conta corrente. Carteira de beneficio nao tem renda variavel.
        var ganhos = carteira.Id == Carteiras.IdPrincipal ? entrada.GanhosVariaveis : [];
        var meses = MontarMeses(dias, cfg, entrada.Hoje, inicio, fim, incertasFora, tagsPorMes, nomesTags, ganhos);

        var resultado = new ResultadoProjecao
        {
            Dias = dias,
            Agora = agora,
            Meses = meses,
            Inicio = inicio,
            Fim = fim,
            Avisos = avisos
        };

        return entrada.Simulacao is null
            ? resultado
            : resultado with { Simulacao = Simulador.Simular(entrada.Simulacao, dias, cfg, entrada.Hoje, inicio, fim) };
    }

    private static void AcumularTag(
        Dictionary<(int Ano, int Mes), Dictionary<int, long>> tagsPorMes,
        DateOnly data,
        int tagId,
        long valorCentavos)
    {
        var chave = (data.Year, data.Month);

        if (!tagsPorMes.TryGetValue(chave, out var porTag))
        {
            porTag = [];
            tagsPorMes[chave] = porTag;
        }

        porTag.TryGetValue(tagId, out var acumulado);
        porTag[tagId] = acumulado + valorCentavos;
    }

    /// <summary>
    /// O diario registrado desta carteira. O dicionario solto da entrada e o da conta corrente,
    /// que e como o cenario de referencia informa; a lista completa manda quando as duas falam
    /// do mesmo dia, por ser a forma que sabe de qual carteira esta falando.
    /// </summary>
    private static Dictionary<DateOnly, long> DiariosDa(EntradaProjecao entrada, Carteira carteira)
    {
        var diarios = new Dictionary<DateOnly, long>();

        if (carteira.Id == Carteiras.IdPrincipal)
        {
            foreach (var (data, valor) in entrada.DiariosReais)
            {
                diarios[data] = valor;
            }
        }

        foreach (var diario in entrada.Diarios.Where(d => Carteiras.Da(d.CarteiraId) == carteira.Id))
        {
            diarios[diario.Data] = diario.ValorCentavos;
        }

        return diarios;
    }

    /// <summary>R6.</summary>
    public static Farol FarolDe(long saldoCentavos, long confortoCentavos) => saldoCentavos switch
    {
        < 0 => Farol.Negativo,
        var s when s < confortoCentavos => Farol.Atencao,
        _ => Farol.Ok
    };

    /// <summary>R7.</summary>
    private static ResumoAgora MontarAgora(
        List<DiaProjetado> dias,
        Configuracao cfg,
        DateOnly hoje,
        DateOnly inicio,
        DateOnly fim)
    {
        var inicioJanela = Calendario.Maior(hoje, inicio);

        // Ancora amanha e o caso normal depois da conferencia com o banco (R7): o numero
        // conferido e o saldo do INICIO de amanha, que e exatamente o saldo do FIM de hoje.
        // Nao e aproximacao, e a mesma grandeza, entao o dia de hoje tem saldo sim. Sem isto a
        // tela Hoje fica sem o numero principal pelo resto do dia em que o dono conferiu.
        long? saldoHoje = hoje.AddDays(1) == inicio ? cfg.SaldoAncoraCentavos : null;
        long? menor = null;
        DateOnly? dataMenor = null;
        DateOnly? primeiroAbaixo = null;
        var guardado = cfg.JaGuardadoCentavos;

        foreach (var dia in dias)
        {
            if (dia.Data == hoje)
            {
                saldoHoje = dia.SaldoCentavos;
            }

            if (dia.Data <= hoje)
            {
                guardado += dia.EconomiasCentavos;
            }

            if (dia.Data < inicioJanela)
            {
                continue;
            }

            // Empate de minimo fica com a data mais cedo, por isso o comparador e estritamente menor.
            if (menor is null || dia.SaldoCentavos < menor)
            {
                menor = dia.SaldoCentavos;
                dataMenor = dia.Data;
            }

            if (primeiroAbaixo is null && dia.SaldoCentavos < cfg.SaldoConfortoCentavos)
            {
                primeiroAbaixo = dia.Data;
            }
        }

        return new ResumoAgora
        {
            Referencia = hoje,
            SaldoHojeCentavos = saldoHoje,
            MenorSaldoAFrenteCentavos = menor,
            DataMenorSaldoAFrente = dataMenor,
            PodeGastarHojeCentavos = menor is null ? 0 : Math.Max(0, menor.Value - cfg.SaldoConfortoCentavos),
            PrimeiroDiaAbaixoConforto = primeiroAbaixo,
            GuardadoAteHojeCentavos = guardado,
            Farol = menor is null ? Farol.Ok : FarolDe(menor.Value, cfg.SaldoConfortoCentavos),
            AnteriorAAncora = hoje < inicio
        };
    }

    /// <summary>R8, R9 e R10.</summary>
    private static List<ResumoMes> MontarMeses(
        List<DiaProjetado> dias,
        Configuracao cfg,
        DateOnly hoje,
        DateOnly inicio,
        DateOnly fim,
        long[] incertasFora,
        Dictionary<(int Ano, int Mes), Dictionary<int, long>> tagsPorMes,
        Dictionary<int, string> nomesTags,
        IReadOnlyList<GanhoVariavel> ganhosVariaveis)
    {
        var meses = new List<ResumoMes>();
        var guardadoAcumulado = cfg.JaGuardadoCentavos;
        var cursor = Calendario.PrimeiroDiaDoMes(inicio);
        var ultimoMes = Calendario.PrimeiroDiaDoMes(fim);

        while (cursor <= ultimoMes)
        {
            var primeiroDoMes = cursor;
            var ultimoDoMes = Calendario.UltimoDiaDoMes(cursor);

            long entradas = 0, saidas = 0, cartao = 0, diario = 0, economias = 0, incertas = 0;
            long? saldoFim = null, menor = null;
            DateOnly? dataMenor = null;
            var diasComReal = 0;
            var diasConsiderados = 0;

            // Media do diario so nos dias ja vividos: da ancora ou do primeiro do mes ate hoje.
            var inicioMedia = Calendario.Maior(inicio, primeiroDoMes);
            var fimMedia = Calendario.Menor(Calendario.Menor(hoje, ultimoDoMes), fim);
            long diarioDaMedia = 0;
            var diasDaMedia = 0;

            foreach (var dia in dias)
            {
                if (dia.Data < primeiroDoMes)
                {
                    continue;
                }

                if (dia.Data > ultimoDoMes)
                {
                    break;
                }

                diasConsiderados++;
                entradas += dia.EntradasCentavos;
                saidas += dia.SaidasCentavos;
                cartao += dia.CartaoCentavos;
                economias += dia.EconomiasCentavos;
                diario += dia.DiarioCentavos;
                incertas += incertasFora[dia.Data.DayNumber - inicio.DayNumber];

                if (dia.DiarioEReal)
                {
                    diasComReal++;
                }

                if (dia.Data >= inicioMedia && dia.Data <= fimMedia)
                {
                    diarioDaMedia += dia.DiarioCentavos;
                    diasDaMedia++;
                }

                saldoFim = dia.SaldoCentavos;

                if (menor is null || dia.SaldoCentavos < menor)
                {
                    menor = dia.SaldoCentavos;
                    dataMenor = dia.Data;
                }
            }

            guardadoAcumulado += economias;

            if (diasConsiderados > 0)
            {
                // R17: o que o dono registrou de ganho variavel neste mes entra nas entradas do
                // mes. Mes sem registro soma zero, e ai todo o resto fica igual ao de antes.
                var ganhoVariavel = GanhosVariaveis.DoMes(ganhosVariaveis, cursor.Year, cursor.Month);
                entradas += ganhoVariavel;

                tagsPorMes.TryGetValue((cursor.Year, cursor.Month), out var porTag);
                meses.Add(new ResumoMes
                {
                    Ano = cursor.Year,
                    Mes = cursor.Month,
                    EntradasCentavos = entradas,
                    GanhoVariavelCentavos = ganhoVariavel,
                    SaidasCentavos = saidas,
                    CartaoCentavos = cartao,
                    DiarioCentavos = diario,
                    EconomiasCentavos = economias,
                    PerformanceCentavos = entradas - saidas - diario - economias - cartao,
                    CustoDeVidaCentavos = saidas + diario + cartao,
                    EconomizadoFracao = entradas == 0 ? 0m : (decimal)economias / entradas,
                    DiarioMedioCentavos = diasDaMedia == 0
                        ? null
                        : (long)Math.Round((decimal)diarioDaMedia / diasDaMedia, MidpointRounding.AwayFromZero),
                    DiasComDiarioReal = diasComReal,
                    EntradasIncertasForaCentavos = incertas,
                    SaldoFimCentavos = saldoFim,
                    MenorSaldoCentavos = menor,
                    DataMenorSaldo = dataMenor,
                    GuardadoAcumuladoCentavos = guardadoAcumulado,
                    Farol = menor is null ? Farol.Ok : FarolDe(menor.Value, cfg.SaldoConfortoCentavos),
                    DiasConsiderados = diasConsiderados,
                    SaidasPorTag = MontarTags(porTag, nomesTags)
                });
            }

            cursor = cursor.AddMonths(1);
        }

        return meses;
    }

    private static List<SaidaPorTag> MontarTags(Dictionary<int, long>? porTag, Dictionary<int, string> nomes)
    {
        if (porTag is null || porTag.Count == 0)
        {
            return [];
        }

        var total = porTag.Values.Sum();

        return porTag
            .Select(par => new SaidaPorTag(
                par.Key == 0 ? null : par.Key,
                par.Key != 0 && nomes.TryGetValue(par.Key, out var nome) ? nome : "Sem tag",
                par.Value,
                total == 0 ? 0m : (decimal)par.Value / total))
            .OrderByDescending(t => t.ValorCentavos)
            .ToList();
    }
}
