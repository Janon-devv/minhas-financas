using MinhasFinancas.Domain.Modelo;
using MinhasFinancas.Domain.Resultado;

namespace MinhasFinancas.Domain;

/// <summary>
/// Regra R11: compara a projecao atual com uma compra hipotetica, sem tocar em nenhum lancamento.
/// Entrada e juros vieram do documento do GPT (secao 11). Com taxa zero o resultado e o do
/// CONTEXTO.md: parcelas iguais e o resto de centavos na primeira parcela.
/// </summary>
public static class Simulador
{
    public const int MaximoParcelas = 36;

    public static ResultadoSimulacao Simular(
        Simulacao simulacao,
        IReadOnlyList<DiaProjetado> dias,
        Configuracao cfg,
        DateOnly hoje,
        DateOnly inicio,
        DateOnly fim)
    {
        var entradaPaga = Math.Clamp(simulacao.EntradaCentavos, 0, Math.Max(0, simulacao.ValorTotalCentavos));
        var financiado = Math.Max(0, simulacao.ValorTotalCentavos - entradaPaga);
        var quantidade = Math.Clamp(simulacao.Parcelas, 1, MaximoParcelas);
        var taxa = simulacao.TaxaMensal < 0 ? 0m : simulacao.TaxaMensal;

        List<ParcelaSimulada> parcelas = financiado == 0
            ? []
            : MontarCronograma(financiado, quantidade, taxa, simulacao.PrimeiraParcela);

        var totalPago = entradaPaga + parcelas.Sum(p => p.PagamentoCentavos);
        var dataEntrada = entradaPaga > 0 ? simulacao.DataEntrada ?? hoje : (DateOnly?)null;

        // Quanto a compra tira da conta ate cada dia.
        var pagamentosPorData = new SortedDictionary<DateOnly, long>();

        if (entradaPaga > 0 && dataEntrada is DateOnly dataDaEntrada)
        {
            pagamentosPorData[dataDaEntrada] = entradaPaga;
        }

        foreach (var parcela in parcelas)
        {
            pagamentosPorData.TryGetValue(parcela.Data, out var acumulado);
            pagamentosPorData[parcela.Data] = acumulado + parcela.PagamentoCentavos;
        }

        var foraDoHorizonte = pagamentosPorData
            .Where(par => par.Key > fim)
            .Sum(par => par.Value);

        var inicioJanela = Calendario.Maior(hoje, inicio);
        var saldosComCompra = new Dictionary<DateOnly, long>(dias.Count);

        long? menorSem = null, menorCom = null;
        DateOnly? dataMenorSem = null, dataMenorCom = null, primeiroAbaixoCom = null;
        long pago = 0;

        foreach (var dia in dias)
        {
            if (pagamentosPorData.TryGetValue(dia.Data, out var doDia))
            {
                pago += doDia;
            }

            var saldoCom = dia.SaldoCentavos - pago;
            saldosComCompra[dia.Data] = saldoCom;

            if (dia.Data < inicioJanela)
            {
                continue;
            }

            if (menorSem is null || dia.SaldoCentavos < menorSem)
            {
                menorSem = dia.SaldoCentavos;
                dataMenorSem = dia.Data;
            }

            if (menorCom is null || saldoCom < menorCom)
            {
                menorCom = saldoCom;
                dataMenorCom = dia.Data;
            }

            if (primeiroAbaixoCom is null && saldoCom < cfg.SaldoConfortoCentavos)
            {
                primeiroAbaixoCom = dia.Data;
            }
        }

        var veredito = menorCom switch
        {
            null => Veredito.CabeComFolga,
            < 0 => Veredito.NaoCabe,
            var m when m < cfg.SaldoConfortoCentavos => Veredito.CabeMasAperta,
            _ => Veredito.CabeComFolga
        };

        return new ResultadoSimulacao
        {
            Parcelas = parcelas,
            EntradaCentavos = entradaPaga,
            DataEntrada = dataEntrada,
            TotalPagoCentavos = totalPago,
            JurosTotaisCentavos = totalPago - simulacao.ValorTotalCentavos,
            MenorSemCentavos = menorSem,
            DataMenorSem = dataMenorSem,
            MenorComCentavos = menorCom,
            DataMenorCom = dataMenorCom,
            PrimeiroDiaAbaixoConfortoCom = primeiroAbaixoCom,
            FolgaCentavos = menorCom is null ? null : menorCom.Value - cfg.SaldoConfortoCentavos,
            Veredito = veredito,
            JaEstavaNegativoSemCompra = menorSem is < 0,
            PioraCentavos = menorSem is null || menorCom is null ? 0 : menorSem.Value - menorCom.Value,
            PagamentosForaDoHorizonteCentavos = foraDoHorizonte,
            SaldosComCompra = saldosComCompra
        };
    }

    /// <summary>
    /// Cronograma de parcelas. Sem juros, divide igual e joga o resto de centavos na primeira parcela,
    /// que e a hipotese conservadora (o dinheiro sai mais cedo). Com juros, usa prestacao fixa
    /// (sistema frances) e a ultima parcela acerta os centavos para zerar a divida.
    /// </summary>
    public static List<ParcelaSimulada> MontarCronograma(
        long financiadoCentavos,
        int quantidade,
        decimal taxaMensal,
        DateOnly primeiraParcela)
    {
        var parcelas = new List<ParcelaSimulada>(quantidade);

        if (financiadoCentavos <= 0 || quantidade <= 0)
        {
            return parcelas;
        }

        if (taxaMensal == 0m)
        {
            var baseParcela = financiadoCentavos / quantidade;
            var resto = financiadoCentavos - (baseParcela * quantidade);
            var devedor = financiadoCentavos;

            for (var k = 0; k < quantidade; k++)
            {
                var pagamento = k == 0 ? baseParcela + resto : baseParcela;
                devedor -= pagamento;
                parcelas.Add(new ParcelaSimulada(
                    k + 1,
                    Calendario.AdicionarMeses(primeiraParcela, k, primeiraParcela.Day),
                    pagamento,
                    0,
                    pagamento,
                    devedor));
            }

            return parcelas;
        }

        var fator = Potencia(1m + taxaMensal, quantidade);
        var prestacao = Arredondar(financiadoCentavos * taxaMensal * fator / (fator - 1m));
        var divida = financiadoCentavos;

        for (var k = 0; k < quantidade; k++)
        {
            var juros = Arredondar(divida * taxaMensal);
            long pagamento;

            if (k == quantidade - 1)
            {
                // A ultima parcela liquida o que sobrou, inclusive os centavos de arredondamento.
                pagamento = divida + juros;
            }
            else
            {
                pagamento = Math.Min(prestacao, divida + juros);

                // Rede de seguranca: uma prestacao que nao cobre nem os juros nunca amortizaria.
                if (pagamento <= juros)
                {
                    pagamento = Math.Min(divida + juros, juros + 1);
                }
            }

            var amortizacao = pagamento - juros;
            divida -= amortizacao;

            parcelas.Add(new ParcelaSimulada(
                k + 1,
                Calendario.AdicionarMeses(primeiraParcela, k, primeiraParcela.Day),
                pagamento,
                juros,
                amortizacao,
                divida));

            if (divida <= 0)
            {
                break;
            }
        }

        return parcelas;
    }

    private static decimal Potencia(decimal valor, int expoente)
    {
        var resultado = 1m;
        for (var i = 0; i < expoente; i++)
        {
            resultado *= valor;
        }

        return resultado;
    }

    private static long Arredondar(decimal valor) =>
        (long)Math.Round(valor, 0, MidpointRounding.AwayFromZero);
}
