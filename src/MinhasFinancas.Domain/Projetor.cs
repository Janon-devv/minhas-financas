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
    public static ResultadoProjecao Projetar(EntradaProjecao entrada)
    {
        var cfg = entrada.Configuracao;
        var inicio = cfg.DataAncora;
        var fim = entrada.FimHorizonte ?? Horizonte.Calcular(entrada.Hoje, inicio, cfg.HorizonteMeses);

        if (fim < inicio)
        {
            fim = inicio;
        }

        var totalDias = fim.DayNumber - inicio.DayNumber + 1;
        var cartoes = entrada.Cartoes.ToDictionary(c => c.Id);
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

        foreach (var lancamento in entrada.Lancamentos)
        {
            if (lancamento.ValorCentavos <= 0)
            {
                continue;
            }

            if (lancamento.Tipo == TipoLancamento.Cartao &&
                (lancamento.CartaoId is null || !cartoes.ContainsKey(lancamento.CartaoId.Value)))
            {
                avisos.Add($"O lancamento \"{lancamento.Descricao}\" e do tipo Cartao mas nao tem cartao valido. " +
                           "Enquanto isso, ele esta sendo contado na data da compra.");
            }

            var fora = lancamento.EntradaIncerta && !cfg.ContarEntradasIncertas;
            var dataEfetiva = Ocorrencias.DataEfetiva(lancamento, cartoes);

            foreach (var data in Ocorrencias.Ate(dataEfetiva, lancamento.Vezes, fim))
            {
                if (data < inicio)
                {
                    continue;
                }

                var i = data.DayNumber - inicio.DayNumber;

                if (fora)
                {
                    // R3: entrada incerta fica fora do saldo, mas o mes mostra quanto deixou de contar.
                    incertasFora[i] += lancamento.ValorCentavos;
                    continue;
                }

                switch (lancamento.Tipo)
                {
                    case TipoLancamento.Entrada:
                        entradas[i] += lancamento.ValorCentavos;
                        break;
                    case TipoLancamento.Saida:
                        saidas[i] += lancamento.ValorCentavos;
                        break;
                    case TipoLancamento.Cartao:
                        cartao[i] += lancamento.ValorCentavos;
                        break;
                    case TipoLancamento.Economia:
                        economias[i] += lancamento.ValorCentavos;
                        break;
                }

                (movimentos[i] ??= []).Add(new MovimentoDoDia(
                    lancamento.Id,
                    lancamento.Descricao,
                    lancamento.Tipo,
                    lancamento.ValorCentavos,
                    lancamento.TagId,
                    lancamento.CartaoId,
                    lancamento.MetaId));

                if (lancamento.Tipo != TipoLancamento.Entrada)
                {
                    var chave = (data.Year, data.Month);
                    if (!tagsPorMes.TryGetValue(chave, out var porTag))
                    {
                        porTag = [];
                        tagsPorMes[chave] = porTag;
                    }

                    // A chave 0 guarda o que saiu sem tag nenhuma.
                    var chaveTag = lancamento.TagId ?? 0;
                    porTag.TryGetValue(chaveTag, out var acumulado);
                    porTag[chaveTag] = acumulado + lancamento.ValorCentavos;
                }
            }
        }

        // R5: percorre o calendario uma vez, acumulando o saldo.
        var dias = new List<DiaProjetado>(totalDias);
        var saldo = cfg.SaldoAncoraCentavos;

        for (var i = 0; i < totalDias; i++)
        {
            var data = inicio.AddDays(i);
            var temReal = entrada.DiariosReais.TryGetValue(data, out var diarioReal);
            var diario = temReal ? diarioReal : cfg.DiarioPrevistoCentavos;

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
        var meses = MontarMeses(dias, cfg, entrada.Hoje, inicio, fim, incertasFora, tagsPorMes, nomesTags);

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
        long? saldoHoje = null;
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
        Dictionary<int, string> nomesTags)
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
                tagsPorMes.TryGetValue((cursor.Year, cursor.Month), out var porTag);
                meses.Add(new ResumoMes
                {
                    Ano = cursor.Year,
                    Mes = cursor.Month,
                    EntradasCentavos = entradas,
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
