using System.Globalization;
using MinhasFinancas.Domain;
using MinhasFinancas.Domain.Modelo;
using Xunit;

namespace MinhasFinancas.Domain.Tests;

/// <summary>Testes dourados: o motor contra o cenario conferido em docs/fixtures.</summary>
public class MotorTests
{
    private static DateOnly Data(string iso) => DateOnly.ParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    [Fact]
    public void Calendario_bate_dia_a_dia_com_o_fixture()
    {
        var cenario = Cenario.Carregar();
        var resultado = Projetor.Projetar(cenario.Montar());
        var esperado = Cenario.LerCsv("esperado_calendario.csv");

        Assert.Equal(esperado.Count, resultado.Dias.Count);

        var divergencias = new List<string>();

        for (var i = 0; i < esperado.Count; i++)
        {
            var linha = esperado[i];
            var dia = resultado.Dias[i];
            var data = linha["data"];

            Conferir(divergencias, data, "data", data, dia.Data.ToString("yyyy-MM-dd"));
            Conferir(divergencias, data, "entradas", linha["entradas_centavos"], dia.EntradasCentavos);

            // No fixture diario a coluna saidas_centavos e a saida TOTAL do dia:
            // cartao e economias estao dentro dela, as colunas proprias sao detalhamento.
            Conferir(divergencias, data, "saidas", linha["saidas_centavos"], dia.SaidaTotalCentavos);
            Conferir(divergencias, data, "cartao", linha["cartao_centavos"], dia.CartaoCentavos);
            Conferir(divergencias, data, "economias", linha["economias_centavos"], dia.EconomiasCentavos);
            Conferir(divergencias, data, "diario", linha["diario_centavos"], dia.DiarioCentavos);
            Conferir(divergencias, data, "diario_e_real", linha["diario_e_real"], dia.DiarioEReal ? 1 : 0);
            Conferir(divergencias, data, "saldo", linha["saldo_centavos"], dia.SaldoCentavos);

            var farolEsperado = Cenario.FarolDe(linha["farol"]);
            if (farolEsperado != dia.Farol)
            {
                divergencias.Add($"{data} farol: esperado {farolEsperado}, obtido {dia.Farol}");
            }
        }

        Assert.True(divergencias.Count == 0,
            $"{divergencias.Count} divergencias em {esperado.Count} dias:\n" +
            string.Join("\n", divergencias.Take(20)));
    }

    [Fact]
    public void Horizonte_mensal_bate_com_o_fixture()
    {
        var cenario = Cenario.Carregar();
        var resultado = Projetor.Projetar(cenario.Montar());
        var esperado = Cenario.LerCsv("esperado_mensal.csv");
        var divergencias = new List<string>();

        for (var i = 0; i < esperado.Count; i++)
        {
            var linha = esperado[i];
            var mes = resultado.Meses[i];
            var rotulo = linha["mes"];

            Conferir(divergencias, rotulo, "mes", rotulo, $"{mes.Ano:0000}-{mes.Mes:00}");
            Conferir(divergencias, rotulo, "entradas", linha["entradas_centavos"], mes.EntradasCentavos);

            // No fixture mensal, diferente do diario, economias NAO entram neste bloco.
            Conferir(divergencias, rotulo, "saidas_e_cartao", linha["saidas_e_cartao_centavos"], mes.SaidasECartaoCentavos);
            Conferir(divergencias, rotulo, "diario", linha["diario_centavos"], mes.DiarioCentavos);
            Conferir(divergencias, rotulo, "economias", linha["economias_centavos"], mes.EconomiasCentavos);
            Conferir(divergencias, rotulo, "performance", linha["performance_centavos"], mes.PerformanceCentavos);
            Conferir(divergencias, rotulo, "saldo_fim", linha["saldo_fim_centavos"], mes.SaldoFimCentavos ?? long.MinValue);
            Conferir(divergencias, rotulo, "menor_saldo", linha["menor_saldo_centavos"], mes.MenorSaldoCentavos ?? long.MinValue);
            Conferir(divergencias, rotulo, "guardado", linha["guardado_acumulado_centavos"], mes.GuardadoAcumuladoCentavos);

            var farolEsperado = Cenario.FarolDe(linha["farol"]);
            if (farolEsperado != mes.Farol)
            {
                divergencias.Add($"{rotulo} farol: esperado {farolEsperado}, obtido {mes.Farol}");
            }
        }

        Assert.True(divergencias.Count == 0, string.Join("\n", divergencias.Take(20)));
    }

    [Fact]
    public void Painel_agora_bate_com_o_fixture()
    {
        var cenario = Cenario.Carregar();
        var resultado = Projetor.Projetar(cenario.Montar());
        var esperado = cenario.Esperado.CartoesResumo;
        var agora = resultado.Agora;

        Assert.Equal(esperado.SaldoHoje, agora.SaldoHojeCentavos);
        Assert.Equal(esperado.MenorSaldoAFrente, agora.MenorSaldoAFrenteCentavos);
        Assert.Equal(esperado.DataMenorSaldoAFrente, agora.DataMenorSaldoAFrente);
        Assert.Equal(esperado.PodeGastarHoje, agora.PodeGastarHojeCentavos);
        Assert.Equal(esperado.GuardadoAteHoje, agora.GuardadoAteHojeCentavos);
        Assert.Equal("nunca", esperado.PrimeiroDiaAbaixoConforto);
        Assert.Null(agora.PrimeiroDiaAbaixoConforto);
    }

    [Fact]
    public void Indicadores_de_outubro_batem_com_o_fixture()
    {
        var cenario = Cenario.Carregar();
        var resultado = Projetor.Projetar(cenario.Montar());
        var esperado = cenario.Esperado.Mes202610;
        var mes = resultado.Mes(2026, 10)!;

        Assert.Equal(esperado.Entradas, mes.EntradasCentavos);
        Assert.Equal(esperado.Saidas, mes.SaidasCentavos);
        Assert.Equal(esperado.Cartao, mes.CartaoCentavos);
        Assert.Equal(esperado.Diario, mes.DiarioCentavos);
        Assert.Equal(esperado.Economias, mes.EconomiasCentavos);
        Assert.Equal(esperado.Performance, mes.PerformanceCentavos);
        Assert.Equal(esperado.CustoDeVida, mes.CustoDeVidaCentavos);
        Assert.Equal(esperado.DiarioMedio, mes.DiarioMedioCentavos);
        Assert.Equal(esperado.DiasComDiarioReal, mes.DiasComDiarioReal);
        Assert.Equal(esperado.EntradasIncertasFora, mes.EntradasIncertasForaCentavos);
        Assert.Equal(esperado.SaldoFim, mes.SaldoFimCentavos);
        Assert.Equal(esperado.MenorSaldo, mes.MenorSaldoCentavos);
        Assert.Equal(esperado.DataMenorSaldo, mes.DataMenorSaldo);
        Assert.Equal(esperado.EconomizadoPct, Math.Round(mes.EconomizadoFracao * 100, 0, MidpointRounding.AwayFromZero));
    }

    [Fact]
    public void Simulacao_do_fixture_da_cabe_mas_aperta()
    {
        var cenario = Cenario.Carregar();
        var resultado = Projetor.Projetar(cenario.Montar());
        var esperado = cenario.Esperado.Simulacao;
        var simulacao = resultado.Simulacao!;

        Assert.Equal(esperado.MenorSem, simulacao.MenorSemCentavos);
        Assert.Equal(esperado.DataMenorSem, simulacao.DataMenorSem);
        Assert.Equal(esperado.MenorCom, simulacao.MenorComCentavos);
        Assert.Equal(esperado.DataMenorCom, simulacao.DataMenorCom);
        Assert.Equal(esperado.Folga, simulacao.FolgaCentavos);
        Assert.Equal(Data(esperado.PrimeiroDiaAbaixoConforto), simulacao.PrimeiroDiaAbaixoConfortoCom);
        Assert.Equal(Veredito.CabeMasAperta, simulacao.Veredito);
        Assert.Equal("cabe_mas_aperta", esperado.Veredito);
        Assert.False(simulacao.ImpactoParcial);
    }

    [Fact]
    public void Cai_na_conta_em_e_ultima_vez_batem_com_o_fixture()
    {
        var cenario = Cenario.Carregar();
        var entrada = cenario.Montar();
        var cartoes = entrada.Cartoes.ToDictionary(c => c.Id);

        for (var i = 0; i < cenario.Lancamentos.Count; i++)
        {
            var esperado = cenario.Lancamentos[i];
            var lancamento = entrada.Lancamentos[i];
            var efetiva = Ocorrencias.DataEfetiva(lancamento, cartoes);

            Assert.Equal(esperado.CaiNaContaEm, efetiva);

            var ultima = Ocorrencias.UltimaVez(efetiva, lancamento.Vezes);
            var textoUltima = ultima?.ToString("yyyy-MM-dd") ?? "sem fim";
            Assert.Equal(esperado.UltimaVez, textoUltima);
        }
    }

    private static void Conferir(List<string> divergencias, string rotulo, string campo, string esperado, long obtido)
    {
        var valor = long.Parse(esperado, CultureInfo.InvariantCulture);
        if (valor != obtido)
        {
            divergencias.Add($"{rotulo} {campo}: esperado {valor}, obtido {obtido}");
        }
    }

    private static void Conferir(List<string> divergencias, string rotulo, string campo, string esperado, string obtido)
    {
        if (esperado != obtido)
        {
            divergencias.Add($"{rotulo} {campo}: esperado {esperado}, obtido {obtido}");
        }
    }
}
