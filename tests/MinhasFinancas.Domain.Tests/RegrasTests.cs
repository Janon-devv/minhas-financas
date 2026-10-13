using System.Globalization;
using MinhasFinancas.Domain;
using MinhasFinancas.Domain.Modelo;
using Xunit;

namespace MinhasFinancas.Domain.Tests;

/// <summary>Casos de borda das regras R1 a R11 que o cenario de referencia nao cobre sozinho.</summary>
public class RegrasTests
{
    private static DateOnly D(string iso) => DateOnly.ParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static Configuracao Cfg(
        string ancora,
        long saldo = 100_000,
        long diario = 0,
        long conforto = 0,
        bool contarIncertas = false) => new()
        {
            DataAncora = D(ancora),
            SaldoAncoraCentavos = saldo,
            DiarioPrevistoCentavos = diario,
            SaldoConfortoCentavos = conforto,
            ContarEntradasIncertas = contarIncertas,
            ConfiguracaoConcluida = true
        };

    private static Lancamento L(
        string data,
        TipoLancamento tipo,
        long valor,
        int? vezes = 1,
        Certeza? certeza = null,
        int id = 1) => new()
        {
            Id = id,
            Data = D(data),
            Descricao = "Teste",
            Tipo = tipo,
            ValorCentavos = valor,
            Vezes = vezes,
            Certeza = certeza
        };

    // ---------- R1: quando a compra no cartao sai da conta ----------

    [Theory]
    [InlineData("2026-10-02", 3, 10, "2026-10-10")]  // compra antes do fechamento: fatura do mes
    [InlineData("2026-10-03", 3, 10, "2026-11-10")]  // no proprio dia do fechamento ja vai pra seguinte
    [InlineData("2026-10-14", 3, 10, "2026-11-10")]  // depois do fechamento
    [InlineData("2026-10-10", 20, 5, "2026-11-05")]  // vencimento antes do fechamento: pula um mes
    [InlineData("2026-10-10", 10, 10, "2026-12-10")] // vencimento igual ao fechamento
    [InlineData("2027-01-25", 20, 31, "2027-02-28")] // vencimento 31 em fevereiro comum
    [InlineData("2028-01-25", 20, 31, "2028-02-29")] // fevereiro de ano bissexto
    public void R1_data_efetiva_do_cartao(string compra, int fechamento, int vencimento, string esperado)
    {
        Assert.Equal(D(esperado), RegrasCartao.DataEfetiva(D(compra), fechamento, vencimento));
    }

    // ---------- R2: ocorrencias e parcelas ----------

    [Fact]
    public void R2_dia_31_cai_no_ultimo_dia_e_nao_fica_preso()
    {
        var ocorrencias = Ocorrencias.Ate(D("2026-10-31"), null, D("2028-03-31")).ToList();

        Assert.Equal(D("2026-10-31"), ocorrencias[0]);
        Assert.Equal(D("2026-11-30"), ocorrencias[1]);

        // O mes seguinte volta para 31: cada ocorrencia sai da data original, nao da anterior.
        Assert.Equal(D("2026-12-31"), ocorrencias[2]);
        Assert.Contains(D("2027-02-28"), ocorrencias);
        Assert.Contains(D("2028-02-29"), ocorrencias);
    }

    [Fact]
    public void R2_vezes_controla_a_quantidade_de_ocorrencias()
    {
        Assert.Single(Ocorrencias.Ate(D("2026-10-14"), 1, D("2030-01-01")));
        Assert.Equal(4, Ocorrencias.Ate(D("2026-10-14"), 4, D("2030-01-01")).Count());

        // Sem fim para no horizonte, nunca em laco infinito.
        Assert.Equal(13, Ocorrencias.Ate(D("2026-10-14"), null, D("2027-10-14")).Count());
    }

    [Fact]
    public void R2_ultima_vez_ignora_o_horizonte()
    {
        Assert.Equal(D("2027-02-10"), Ocorrencias.UltimaVez(D("2026-11-10"), 4));
        Assert.Null(Ocorrencias.UltimaVez(D("2026-11-10"), null));
    }

    // ---------- R3: o que entra na projecao ----------

    [Fact]
    public void R3_lancamento_todo_antes_da_ancora_nao_conta()
    {
        var entrada = EntradaProjecao.De(
            Cfg("2026-10-13"),
            D("2026-10-13"),
            [L("2026-09-10", TipoLancamento.Saida, 50_000)],
            fimHorizonte: D("2026-10-31"));

        var resultado = Projetor.Projetar(entrada);

        Assert.Equal(100_000, resultado.Dias[0].SaldoCentavos);
        Assert.All(resultado.Dias, dia => Assert.Equal(0, dia.SaidaTotalCentavos));
    }

    [Fact]
    public void R3_entrada_incerta_depende_do_toggle()
    {
        var lancamentos = new[] { L("2026-10-20", TipoLancamento.Entrada, 60_000, certeza: Certeza.Incerta) };

        var fora = Projetor.Projetar(EntradaProjecao.De(
            Cfg("2026-10-13"), D("2026-10-13"), lancamentos, fimHorizonte: D("2026-10-31")));

        var dentro = Projetor.Projetar(EntradaProjecao.De(
            Cfg("2026-10-13", contarIncertas: true), D("2026-10-13"), lancamentos, fimHorizonte: D("2026-10-31")));

        Assert.Equal(100_000, fora.Dia(D("2026-10-20"))!.SaldoCentavos);
        Assert.Equal(60_000, fora.Mes(2026, 10)!.EntradasIncertasForaCentavos);

        Assert.Equal(160_000, dentro.Dia(D("2026-10-20"))!.SaldoCentavos);
        Assert.Equal(0, dentro.Mes(2026, 10)!.EntradasIncertasForaCentavos);
    }

    // ---------- R4: diario previsto contra diario real ----------

    [Fact]
    public void R4_diario_real_zero_e_diferente_de_sem_registro()
    {
        var cfg = Cfg("2026-10-13", diario: 4_000);

        var comZero = Projetor.Projetar(new EntradaProjecao
        {
            Configuracao = cfg,
            Hoje = D("2026-10-15"),
            DiariosReais = new Dictionary<DateOnly, long> { [D("2026-10-14")] = 0 },
            FimHorizonte = D("2026-10-15")
        });

        var semRegistro = Projetor.Projetar(new EntradaProjecao
        {
            Configuracao = cfg,
            Hoje = D("2026-10-15"),
            FimHorizonte = D("2026-10-15")
        });

        var diaComZero = comZero.Dia(D("2026-10-14"))!;
        Assert.Equal(0, diaComZero.DiarioCentavos);
        Assert.True(diaComZero.DiarioEReal);

        // Dia que ja passou sem registro usa o previsto, nunca zero: a previsao e conservadora.
        var diaSemRegistro = semRegistro.Dia(D("2026-10-14"))!;
        Assert.Equal(4_000, diaSemRegistro.DiarioCentavos);
        Assert.False(diaSemRegistro.DiarioEReal);
    }

    // ---------- R6 e R7 ----------

    [Fact]
    public void R7_empate_de_minimo_devolve_a_data_mais_cedo()
    {
        var entrada = EntradaProjecao.De(
            Cfg("2026-10-13", saldo: 100_000),
            D("2026-10-13"),
            [
                L("2026-10-14", TipoLancamento.Saida, 20_000, id: 1),
                L("2026-10-20", TipoLancamento.Entrada, 20_000, id: 2),
                L("2026-10-21", TipoLancamento.Saida, 20_000, id: 3)
            ],
            fimHorizonte: D("2026-10-31"));

        var resultado = Projetor.Projetar(entrada);

        Assert.Equal(80_000, resultado.Agora.MenorSaldoAFrenteCentavos);
        Assert.Equal(D("2026-10-14"), resultado.Agora.DataMenorSaldoAFrente);
    }

    [Fact]
    public void R6_farol_segue_o_conforto()
    {
        Assert.Equal(Farol.Negativo, Projetor.FarolDe(-1, 50_000));
        Assert.Equal(Farol.Atencao, Projetor.FarolDe(0, 50_000));
        Assert.Equal(Farol.Atencao, Projetor.FarolDe(49_999, 50_000));
        Assert.Equal(Farol.Ok, Projetor.FarolDe(50_000, 50_000));
    }

    [Fact]
    public void R7_ancora_no_futuro_deixa_hoje_sem_saldo()
    {
        var resultado = Projetor.Projetar(EntradaProjecao.De(
            Cfg("2026-11-01"), D("2026-10-13"), fimHorizonte: D("2026-11-30")));

        Assert.Null(resultado.Agora.SaldoHojeCentavos);
        Assert.True(resultado.Agora.AnteriorAAncora);
        Assert.Equal(D("2026-11-01"), resultado.Dias[0].Data);
    }

    // ---------- R8: a reconciliacao aritmetica publica do metodo ----------

    [Fact]
    public void R8_exemplo_publico_de_performance_e_custo_de_vida()
    {
        // Entradas 4.500 / saidas 2.876,60 / diario 105 / economias 500 / cartao 0
        // devem dar performance 1.018,40, custo de vida 2.981,60, economizado 11% e diario medio 4,38 em 24 dias.
        var entrada = new EntradaProjecao
        {
            Configuracao = Cfg("2026-10-08", saldo: 0, diario: 0),
            Hoje = D("2026-10-31"),
            Lancamentos =
            [
                L("2026-10-09", TipoLancamento.Entrada, 450_000, id: 1),
                L("2026-10-10", TipoLancamento.Saida, 287_660, id: 2),
                L("2026-10-11", TipoLancamento.Economia, 50_000, id: 3)
            ],
            DiariosReais = new Dictionary<DateOnly, long> { [D("2026-10-08")] = 10_500 },
            FimHorizonte = D("2026-10-31")
        };

        var mes = Projetor.Projetar(entrada).Mes(2026, 10)!;

        Assert.Equal(24, mes.DiasConsiderados);
        Assert.Equal(101_840, mes.PerformanceCentavos);
        Assert.Equal(298_160, mes.CustoDeVidaCentavos);
        Assert.Equal(11, Math.Round(mes.EconomizadoFracao * 100, 0, MidpointRounding.AwayFromZero));
        Assert.Equal(438, mes.DiarioMedioCentavos);
    }

    // ---------- R11: cronograma do simulador ----------

    [Fact]
    public void R11_sem_juros_divide_igual_e_joga_o_resto_na_primeira()
    {
        var parcelas = Simulador.MontarCronograma(10_000, 3, 0m, D("2026-11-10"));

        Assert.Equal([3_334L, 3_333L, 3_333L], parcelas.Select(p => p.PagamentoCentavos));
        Assert.Equal(10_000, parcelas.Sum(p => p.PagamentoCentavos));
        Assert.Equal(0, parcelas[^1].SaldoDevedorCentavos);
        Assert.All(parcelas, p => Assert.Equal(0, p.JurosCentavos));

        Assert.Equal([D("2026-11-10"), D("2026-12-10"), D("2027-01-10")], parcelas.Select(p => p.Data));
    }

    [Fact]
    public void R11_com_juros_fecha_a_divida_em_zero()
    {
        var parcelas = Simulador.MontarCronograma(100_000, 12, 0.02m, D("2026-11-10"));

        Assert.Equal(12, parcelas.Count);
        Assert.Equal(0, parcelas[^1].SaldoDevedorCentavos);
        Assert.Equal(100_000, parcelas.Sum(p => p.AmortizacaoCentavos));
        Assert.True(parcelas.Sum(p => p.JurosCentavos) > 0);

        // Prestacao fixa: so a ultima pode diferir, por causa do acerto de centavos.
        var distintas = parcelas.Take(11).Select(p => p.PagamentoCentavos).Distinct().Count();
        Assert.Equal(1, distintas);
    }

    [Fact]
    public void R11_parcela_que_cai_fora_do_horizonte_e_sinalizada()
    {
        var entrada = new EntradaProjecao
        {
            Configuracao = Cfg("2026-10-13", saldo: 500_000, conforto: 50_000),
            Hoje = D("2026-10-13"),
            FimHorizonte = D("2026-12-31"),
            Simulacao = new Simulacao
            {
                Descricao = "Geladeira",
                ValorTotalCentavos = 300_000,
                Parcelas = 6,
                PrimeiraParcela = D("2026-11-10")
            }
        };

        var simulacao = Projetor.Projetar(entrada).Simulacao!;

        Assert.True(simulacao.ImpactoParcial);
        // 6 parcelas de 50.000 a partir de 10/11; so duas caem dentro do horizonte, 4 ficam fora.
        Assert.Equal(200_000, simulacao.PagamentosForaDoHorizonteCentavos);
    }

    [Fact]
    public void R11_simular_nao_altera_a_projecao_de_base()
    {
        var baseEntrada = EntradaProjecao.De(
            Cfg("2026-10-13", saldo: 500_000),
            D("2026-10-13"),
            [L("2026-10-20", TipoLancamento.Saida, 30_000)],
            fimHorizonte: D("2026-12-31"));

        var semSimulacao = Projetor.Projetar(baseEntrada);
        var comSimulacao = Projetor.Projetar(baseEntrada with
        {
            Simulacao = new Simulacao
            {
                ValorTotalCentavos = 190_000,
                Parcelas = 1,
                PrimeiraParcela = D("2026-11-10")
            }
        });

        Assert.Equal(
            semSimulacao.Dias.Select(d => d.SaldoCentavos),
            comSimulacao.Dias.Select(d => d.SaldoCentavos));

        Assert.NotNull(comSimulacao.Simulacao);
        Assert.Null(semSimulacao.Simulacao);
    }

    // ---------- Recalibracao ----------

    [Fact]
    public void Recalibrar_troca_o_saldo_e_preserva_os_diarios_por_data()
    {
        var diarios = new Dictionary<DateOnly, long>
        {
            [D("2026-10-13")] = 5_000,
            [D("2026-10-20")] = 2_000
        };

        var antes = Projetor.Projetar(new EntradaProjecao
        {
            Configuracao = Cfg("2026-10-13", saldo: 250_000, diario: 4_000),
            Hoje = D("2026-10-20"),
            DiariosReais = diarios,
            FimHorizonte = D("2026-10-31")
        });

        // Nova ancora em 20/10 com o saldo conferido no banco.
        var depois = Projetor.Projetar(new EntradaProjecao
        {
            Configuracao = Cfg("2026-10-20", saldo: 200_000, diario: 4_000),
            Hoje = D("2026-10-20"),
            DiariosReais = diarios,
            FimHorizonte = D("2026-10-31")
        });

        Assert.Equal(D("2026-10-13"), antes.Inicio);
        Assert.Equal(D("2026-10-20"), depois.Inicio);

        // O diario registrado em 20/10 continua valendo depois da recalibracao.
        var dia = depois.Dia(D("2026-10-20"))!;
        Assert.Equal(2_000, dia.DiarioCentavos);
        Assert.True(dia.DiarioEReal);
        Assert.Equal(198_000, dia.SaldoCentavos);
    }

    // ---------- Metas ----------

    [Fact]
    public void Metas_separam_o_que_ja_foi_guardado_do_que_ainda_e_plano()
    {
        var meta = new Meta
        {
            Id = 1,
            Nome = "Reserva",
            ObjetivoCentavos = 500_000,
            Prazo = D("2027-01-31"),
            SaldoInicialCentavos = 100_000
        };

        var aporte = new Lancamento
        {
            Id = 1,
            Data = D("2026-10-06"),
            Descricao = "Reserva mensal",
            Tipo = TipoLancamento.Economia,
            ValorCentavos = 40_000,
            Vezes = null,
            MetaId = 1
        };

        var resumo = CalculadoraMetas.Calcular(
            [meta], [aporte], [], D("2026-10-01"), D("2026-11-15"), D("2028-12-31")).Single();

        // Aportes de 06/10 e 06/11 ja passaram; o resto e plano.
        Assert.Equal(180_000, resumo.GuardadoCentavos);
        Assert.Equal(320_000, resumo.FaltaCentavos);
        // De novembro a janeiro contam 3 meses de aporte, incluindo o mes atual.
        Assert.Equal(3, resumo.MesesRestantes);
        Assert.Equal(106_667, resumo.AporteMensalSugeridoCentavos);
        Assert.Equal(SituacaoMeta.EmAndamento, resumo.Situacao);

        // 180.000 mais os aportes de dez e jan nao chegam aos 500.000 do objetivo.
        Assert.True(resumo.PlanoInsuficiente);
    }
}
