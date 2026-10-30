using System.Globalization;
using MinhasFinancas.Domain;
using MinhasFinancas.Domain.Modelo;
using MinhasFinancas.Domain.Resultado;
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
        bool contarIncertas = false,
        bool diarioAtivo = true) => new()
        {
            DataAncora = D(ancora),
            SaldoAncoraCentavos = saldo,
            DiarioPrevistoCentavos = diario,
            DiarioPrevistoAtivo = diarioAtivo,
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

    [Fact]
    public void R4_contagem_desligada_zera_o_dia_sem_registro_mas_nao_o_registrado()
    {
        var cfg = Cfg("2026-10-13", diario: 4_000, diarioAtivo: false);

        var resultado = Projetor.Projetar(new EntradaProjecao
        {
            Configuracao = cfg,
            Hoje = D("2026-10-15"),
            DiariosReais = new Dictionary<DateOnly, long> { [D("2026-10-14")] = 7_000 },
            FimHorizonte = D("2026-10-15")
        });

        // Dia sem registro nao desconta nada: o previsto fica guardado so como referencia.
        var semRegistro = resultado.Dia(D("2026-10-13"))!;
        Assert.Equal(0, semRegistro.DiarioCentavos);
        Assert.False(semRegistro.DiarioEReal);

        // Gasto registrado continua descendo igual: desligar o previsto nao apaga gasto real.
        var registrado = resultado.Dia(D("2026-10-14"))!;
        Assert.Equal(7_000, registrado.DiarioCentavos);
        Assert.True(registrado.DiarioEReal);

        // 100.000 - 0 no dia 13 - 7.000 no dia 14 - 0 no dia 15.
        Assert.Equal(93_000, resultado.Dia(D("2026-10-15"))!.SaldoCentavos);
    }

    [Fact]
    public void R2_semanal_cai_de_sete_em_sete_dias_e_atravessa_o_mes()
    {
        var ocorrencias = Ocorrencias
            .Ate(D("2026-10-28"), null, D("2026-11-18"), Periodicidade.Semanal)
            .ToList();

        // Vira o mes sem puxar para o mesmo dia do mes seguinte: semana e semana.
        Assert.Equal(
            [D("2026-10-28"), D("2026-11-04"), D("2026-11-11"), D("2026-11-18")],
            ocorrencias);

        Assert.Equal(D("2026-11-18"), Ocorrencias.UltimaVez(D("2026-10-28"), 4, Periodicidade.Semanal));
    }

    [Fact]
    public void R2_semanal_num_mes_cheio_da_quatro_ou_cinco_ocorrencias()
    {
        // O mesmo lancamento rende semanas diferentes conforme o mes: e por isso que compra de
        // mercado nao cabe num valor mensal fixo.
        var outubro = Ocorrencias.Ate(D("2026-10-01"), null, D("2026-10-31"), Periodicidade.Semanal).Count();
        var novembro = Ocorrencias.Ate(D("2026-11-02"), null, D("2026-11-30"), Periodicidade.Semanal).Count();

        Assert.Equal(5, outubro);
        Assert.Equal(5, novembro);
    }

    // ---------- R12: situacao de cada ocorrencia ----------

    private static SituacaoOcorrencia Sit(
        string data,
        bool pago = false,
        string? pagoEm = null,
        long? valor = null,
        int lancamentoId = 1) => new()
        {
            LancamentoId = lancamentoId,
            Data = D(data),
            Pago = pago,
            PagoEm = pagoEm is null ? null : D(pagoEm),
            ValorCentavos = valor
        };

    private static ResultadoProjecao Projetar(
        Configuracao cfg,
        string hoje,
        string fim,
        IEnumerable<Lancamento> lancamentos,
        IEnumerable<SituacaoOcorrencia>? situacoes = null) =>
        Projetor.Projetar(new EntradaProjecao
        {
            Configuracao = cfg,
            Hoje = D(hoje),
            FimHorizonte = D(fim),
            Lancamentos = lancamentos.ToList(),
            Situacoes = situacoes?.ToList() ?? []
        });

    [Fact]
    public void R12_conta_vencida_antes_da_ancora_so_volta_para_a_projecao_se_alguem_disser_que_nao_pagou()
    {
        var cfg = Cfg("2026-10-14", saldo: 100_000);
        var aluguel = L("2026-10-05", TipoLancamento.Saida, 150_000, vezes: null);

        // Sem registro vale o padrao de sempre: o saldo da ancora veio do extrato e ja contem
        // o que aconteceu, entao a ocorrencia anterior a ela fica de fora.
        var padrao = Projetar(cfg, "2026-10-14", "2026-10-20", [aluguel]);
        Assert.Equal(100_000, padrao.Dia(D("2026-10-14"))!.SaldoCentavos);

        // Dito que nao saiu, o dinheiro ainda esta na conta: a saida e trazida para o primeiro
        // dia projetado, em vez de desaparecer da conta de chegar.
        var emAberto = Projetar(cfg, "2026-10-14", "2026-10-20", [aluguel], [Sit("2026-10-05")]);
        var dia = emAberto.Dia(D("2026-10-14"))!;

        Assert.Equal(150_000, dia.SaidasCentavos);
        Assert.Equal(-50_000, dia.SaldoCentavos);
        Assert.True(dia.Movimentos[0].Atrasada);
    }

    [Fact]
    public void R12_ocorrencia_paga_antes_da_ancora_nao_desconta_de_novo()
    {
        var cfg = Cfg("2026-10-14", saldo: 100_000);
        var aluguel = L("2026-10-05", TipoLancamento.Saida, 150_000, vezes: null);

        var paga = Projetar(cfg, "2026-10-14", "2026-10-20", [aluguel],
            [Sit("2026-10-05", pago: true, pagoEm: "2026-10-05")]);

        Assert.Equal(100_000, paga.Dia(D("2026-10-14"))!.SaldoCentavos);
    }

    [Fact]
    public void R12_pagamento_depois_da_ancora_conta_no_dia_em_que_saiu()
    {
        var cfg = Cfg("2026-10-14", saldo: 100_000);
        var conta = L("2026-10-16", TipoLancamento.Saida, 30_000, vezes: 1);

        // Previsto para o dia 16, pago no dia 18: o dinheiro sai no 18.
        var resultado = Projetar(cfg, "2026-10-14", "2026-10-20", [conta],
            [Sit("2026-10-16", pago: true, pagoEm: "2026-10-18")]);

        Assert.Equal(0, resultado.Dia(D("2026-10-16"))!.SaidasCentavos);
        Assert.Equal(30_000, resultado.Dia(D("2026-10-18"))!.SaidasCentavos);
        Assert.Equal(70_000, resultado.Dia(D("2026-10-18"))!.SaldoCentavos);
    }

    [Fact]
    public void R12_ocorrencia_pulada_nao_conta_nem_como_divida()
    {
        var cfg = Cfg("2026-10-01", saldo: 100_000);
        var academia = L("2026-10-10", TipoLancamento.Saida, 12_000, vezes: null);

        var resultado = Projetar(cfg, "2026-10-01", "2026-11-30", [academia],
            [new SituacaoOcorrencia { LancamentoId = 1, Data = D("2026-10-10"), Pulada = true }]);

        // Outubro nao teve. Novembro continua valendo: pular e uma ocorrencia, nao o lancamento.
        Assert.Equal(0, resultado.Dia(D("2026-10-10"))!.SaidasCentavos);
        Assert.Equal(12_000, resultado.Dia(D("2026-11-10"))!.SaidasCentavos);
        Assert.Equal(88_000, resultado.Dia(D("2026-11-30"))!.SaldoCentavos);
    }

    [Fact]
    public void R12_valor_registrado_vale_so_naquela_ocorrencia()
    {
        var cfg = Cfg("2026-10-01", saldo: 100_000);
        var luz = L("2026-10-10", TipoLancamento.Saida, 10_000, vezes: null);

        // Conta que varia: outubro veio 180, novembro continua valendo o previsto.
        var resultado = Projetar(cfg, "2026-10-01", "2026-11-30", [luz],
            [Sit("2026-10-10", valor: 18_000)]);

        Assert.Equal(18_000, resultado.Dia(D("2026-10-10"))!.SaidasCentavos);
        Assert.Equal(10_000, resultado.Dia(D("2026-11-10"))!.SaidasCentavos);
    }

    [Fact]
    public void R12_fora_da_projecao_marca_so_o_que_venceu_antes_da_ancora()
    {
        var aluguel = L("2026-10-05", TipoLancamento.Saida, 150_000, vezes: null, id: 1);
        var internet = L("2026-10-20", TipoLancamento.Saida, 10_000, vezes: null, id: 2);

        var lista = Pendencias.Listar(
            [aluguel, internet],
            new Dictionary<int, Cartao>(),
            [],
            desde: D("2026-10-01"),
            ate: D("2026-10-25"),
            hoje: D("2026-10-25"),
            ancora: D("2026-10-14"));

        var venceuAntes = lista.Single(p => p.LancamentoId == 1);
        var venceuDepois = lista.Single(p => p.LancamentoId == 2);

        // A de antes da ancora some do saldo projetado; a de depois ja esta la dentro.
        Assert.True(venceuAntes.ForaDaProjecao);
        Assert.False(venceuDepois.ForaDaProjecao);

        // Nenhuma das duas tem resposta ainda, e isso e diferente de alguem ter dito que nao pagou.
        Assert.All(lista, p => Assert.False(p.Registrada));
        Assert.All(lista, p => Assert.Equal(EstadoOcorrencia.EmAberto, p.Estado));
    }

    [Fact]
    public void R12_marcar_pago_vale_so_para_a_ocorrencia_daquele_mes()
    {
        var aluguel = L("2026-10-05", TipoLancamento.Saida, 150_000, vezes: null);

        // Pago o de outubro. Novembro e dezembro nascem sem resposta, como qualquer mes novo.
        var lista = Pendencias.Listar(
            [aluguel],
            new Dictionary<int, Cartao>(),
            [Sit("2026-10-05", pago: true, pagoEm: "2026-10-05")],
            desde: D("2026-10-01"),
            ate: D("2026-12-31"),
            hoje: D("2026-11-10"),
            ancora: D("2026-10-01"));

        Assert.Equal(EstadoOcorrencia.Paga, lista.Single(p => p.DataPrevista == D("2026-10-05")).Estado);
        Assert.Equal(EstadoOcorrencia.EmAberto, lista.Single(p => p.DataPrevista == D("2026-11-05")).Estado);
        Assert.Equal(EstadoOcorrencia.Prevista, lista.Single(p => p.DataPrevista == D("2026-12-05")).Estado);
    }

    // ---------- R13: carteiras ----------

    [Fact]
    public void R13_carteira_tem_saldo_proprio_e_nao_mistura_com_a_conta_corrente()
    {
        var cfg = Cfg("2026-10-01", saldo: 100_000);

        var va = new Carteira
        {
            Id = 7,
            Nome = "Vale-alimentacao",
            Tipo = TipoCarteira.Beneficio,
            SaldoAncoraCentavos = 60_000,
            DataAncora = D("2026-10-01")
        };

        var almoco = L("2026-10-05", TipoLancamento.Saida, 25_000, vezes: 1, id: 1);
        almoco.CarteiraId = 7;

        var aluguel = L("2026-10-05", TipoLancamento.Saida, 150_000, vezes: 1, id: 2);

        var resultado = Projetor.Projetar(new EntradaProjecao
        {
            Configuracao = cfg,
            Hoje = D("2026-10-01"),
            FimHorizonte = D("2026-10-10"),
            Lancamentos = [almoco, aluguel],
            Carteiras = [va]
        });

        // A conta corrente só vê o aluguel: o almoço saiu do benefício.
        Assert.Equal(-50_000, resultado.Dia(D("2026-10-05"))!.SaldoCentavos);

        // E o benefício só vê o almoço: ele não paga aluguel, nem fica negativo por causa dele.
        var carteira = Assert.Single(resultado.Carteiras);
        Assert.Equal(35_000, carteira.Dia(D("2026-10-05"))!.SaldoCentavos);
        Assert.Equal(Farol.Ok, carteira.Agora.Farol);
    }

    [Fact]
    public void R13_sem_carteira_cadastrada_nada_muda()
    {
        var cfg = Cfg("2026-10-01", saldo: 100_000, diario: 1_000);
        var conta = L("2026-10-05", TipoLancamento.Saida, 30_000, vezes: 1);

        var entrada = new EntradaProjecao
        {
            Configuracao = cfg,
            Hoje = D("2026-10-01"),
            FimHorizonte = D("2026-10-10"),
            Lancamentos = [conta]
        };

        var resultado = Projetor.Projetar(entrada);

        // Elemento neutro: lista vazia de carteiras deixa a projecao identica a de antes delas
        // existirem. E o que mantem os testes dourados passando sem tocar nos fixtures.
        Assert.Empty(resultado.Carteiras);
        Assert.Equal(100_000 - 30_000 - (1_000 * 5), resultado.Dia(D("2026-10-05"))!.SaldoCentavos);
    }

    [Fact]
    public void R13_carteira_sem_diario_nao_desconta_nada_por_dia()
    {
        var cfg = Cfg("2026-10-01", saldo: 100_000, diario: 1_000);

        var semDiario = new Carteira
        {
            Id = 1,
            Nome = "Vale-alimentacao",
            SaldoAncoraCentavos = 50_000,
            DataAncora = D("2026-10-01"),
            DiarioPrevistoCentavos = null
        };

        var comDiario = new Carteira
        {
            Id = 2,
            Nome = "Vale-transporte",
            SaldoAncoraCentavos = 50_000,
            DataAncora = D("2026-10-01"),
            DiarioPrevistoCentavos = 1_500,
            Ordem = 1
        };

        var resultado = Projetor.Projetar(new EntradaProjecao
        {
            Configuracao = cfg,
            Hoje = D("2026-10-01"),
            FimHorizonte = D("2026-10-05"),
            Carteiras = [semDiario, comDiario]
        });

        var va = resultado.Carteiras.Single(c => c.Carteira.Id == 1);
        var vt = resultado.Carteiras.Single(c => c.Carteira.Id == 2);

        // Sem diario o saldo fica parado; com diario desce todo dia, inclusive o da ancora.
        Assert.Equal(50_000, va.Dia(D("2026-10-05"))!.SaldoCentavos);
        Assert.Equal(50_000 - (1_500 * 5), vt.Dia(D("2026-10-05"))!.SaldoCentavos);
    }

    // ---------- R14 a R16: fatura de cartao ----------

    private static Cartao CartaoDeTeste(decimal juros = 0m) => new()
    {
        Id = 1,
        Nome = "Principal",
        DiaFechamento = 3,
        DiaVencimento = 10,
        JurosRotativoMensal = juros
    };

    private static Lancamento CompraNoCartao(string data, long valor, PapelNaFatura papel, int id = 1)
    {
        var compra = L(data, TipoLancamento.Cartao, valor, vezes: 1, id: id);
        compra.CartaoId = 1;
        compra.PapelNaFatura = papel;
        return compra;
    }

    [Fact]
    public void R14_total_informado_manda_e_detalhamento_nao_soma_de_novo()
    {
        var cartao = CartaoDeTeste();

        // Dois itens do mesmo ciclo: um so descreve o que ja esta no total, o outro e novo.
        var descrito = CompraNoCartao("2026-10-01", 89_000, PapelNaFatura.Detalhamento, id: 1);
        var novo = CompraNoCartao("2026-10-01", 34_000, PapelNaFatura.NovoLancamento, id: 2);

        var fatura = new FaturaCartao
        {
            Id = 1,
            CartaoId = 1,
            Fechamento = D("2026-10-03"),
            Vencimento = D("2026-10-10"),
            TotalInformadoCentavos = 120_000
        };

        var resumo = Faturas.Resumir([fatura], [descrito, novo], [cartao], D("2026-10-05")).Single();

        Assert.True(resumo.TotalFoiInformado);
        Assert.Equal(120_000, resumo.InformadoCentavos);
        Assert.Equal(89_000, resumo.DetalhadoCentavos);
        Assert.Equal(34_000, resumo.AcrescentadoCentavos);

        // 120.000 informado + 34.000 do novo lancamento. O detalhado ja estava dentro dos 120.000.
        Assert.Equal(154_000, resumo.TotalCentavos);
    }

    [Fact]
    public void R14_sem_total_informado_a_fatura_e_a_soma_dos_itens()
    {
        var cartao = CartaoDeTeste();
        var um = CompraNoCartao("2026-10-01", 89_000, PapelNaFatura.Detalhamento, id: 1);
        var outro = CompraNoCartao("2026-10-01", 34_000, PapelNaFatura.NovoLancamento, id: 2);

        var fatura = new FaturaCartao
        {
            Id = 1,
            CartaoId = 1,
            Fechamento = D("2026-10-03"),
            Vencimento = D("2026-10-10"),
            TotalInformadoCentavos = null
        };

        var resumo = Faturas.Resumir([fatura], [um, outro], [cartao], D("2026-10-05")).Single();

        // Sem numero informado os itens sao a unica fonte, e ai todos contam, inclusive o
        // detalhamento: nao ha total para ele estar dentro.
        Assert.False(resumo.TotalFoiInformado);
        Assert.Equal(123_000, resumo.TotalCentavos);
    }

    [Fact]
    public void R14_fatura_substitui_as_compras_do_ciclo_na_projecao()
    {
        var cartao = CartaoDeTeste();
        var compra = CompraNoCartao("2026-10-01", 89_000, PapelNaFatura.Detalhamento);

        var fatura = new FaturaCartao
        {
            Id = 1,
            CartaoId = 1,
            Fechamento = D("2026-10-03"),
            Vencimento = D("2026-10-10"),
            TotalInformadoCentavos = 120_000
        };

        var resultado = Projetor.Projetar(new EntradaProjecao
        {
            Configuracao = Cfg("2026-10-01", saldo: 500_000),
            Hoje = D("2026-10-01"),
            FimHorizonte = D("2026-10-31"),
            Lancamentos = [compra],
            Cartoes = [cartao],
            Faturas = [fatura]
        });

        // So a fatura desconta. Contar a compra tambem seria a dupla contagem que o CONTEXTO
        // proibe desde a primeira versao.
        Assert.Equal(120_000, resultado.Dia(D("2026-10-10"))!.CartaoCentavos);
        Assert.Equal(380_000, resultado.Dia(D("2026-10-10"))!.SaldoCentavos);
    }

    [Fact]
    public void R16_ciclo_sem_fatura_continua_na_regra_antiga()
    {
        var cartao = CartaoDeTeste();
        var compra = CompraNoCartao("2026-10-01", 89_000, PapelNaFatura.NovoLancamento);

        var resultado = Projetor.Projetar(new EntradaProjecao
        {
            Configuracao = Cfg("2026-10-01", saldo: 500_000),
            Hoje = D("2026-10-01"),
            FimHorizonte = D("2026-10-31"),
            Lancamentos = [compra],
            Cartoes = [cartao]
        });

        // Sem fatura cadastrada, a compra sai sozinha no vencimento dela, como sempre foi.
        Assert.Equal(89_000, resultado.Dia(D("2026-10-10"))!.CartaoCentavos);
    }

    [Fact]
    public void R15_fatura_vencida_entra_na_projecao_com_os_juros_do_atraso()
    {
        var cartao = CartaoDeTeste(juros: 0.10m);

        var fatura = new FaturaCartao
        {
            Id = 1,
            CartaoId = 1,
            Fechamento = D("2026-09-03"),
            Vencimento = D("2026-09-10"),
            TotalInformadoCentavos = 100_000
        };

        var resultado = Projetor.Projetar(new EntradaProjecao
        {
            Configuracao = Cfg("2026-10-14", saldo: 500_000),
            Hoje = D("2026-10-14"),
            FimHorizonte = D("2026-10-31"),
            Cartoes = [cartao],
            Faturas = [fatura]
        });

        // Venceu em 10/09 e nao foi paga. Deixar no passado a tiraria da projecao e a divida
        // sumiria da tela, entao ela vem para o primeiro dia projetado, com 1 mes de juros.
        Assert.Equal(110_000, resultado.Dia(D("2026-10-14"))!.CartaoCentavos);
    }

    [Fact]
    public void R15_juros_do_rotativo_param_em_100_por_cento_da_divida()
    {
        // Fatura de 2006 (ano digitado errado) com 15% ao mes: 240 meses de juros compostos
        // estouravam o long e derrubavam todas as telas. A lei limita juros a 100% da divida.
        var cartao = CartaoDeTeste(juros: 0.15m);
        var fatura = new FaturaCartao { Id = 1, CartaoId = 1, Vencimento = D("2006-10-10"), TotalInformadoCentavos = 100_000 };

        var resumo = Faturas.Resumir([fatura], [], [cartao], D("2026-10-23")).Single();

        Assert.Equal(100_000, resumo.JurosCentavos);
        Assert.Equal(200_000, resumo.ASairCentavos);
        Assert.Equal(115_000, Faturas.ComJuros(100_000, 0.15m, 1));
    }

    [Fact]
    public void Valores_absurdos_sao_recusados_e_nao_derrubam_o_motor()
    {
        // Taxa de 9999% ao mes estourava o decimal no cronograma.
        var parcelas = Simulador.MontarCronograma(100_000, 36, 99.99m, D("2026-11-10"));
        Assert.NotEmpty(parcelas);

        var simulacao = new Simulacao { ValorTotalCentavos = 100_000, Parcelas = 2, TaxaMensal = 99.99m, PrimeiraParcela = D("2026-11-10") };
        Assert.Contains(Validacao.Simulacao(simulacao, D("2026-10-23")), e => e.Contains("100%"));

        var lancamento = L("2006-10-10", TipoLancamento.Saida, 10_000);
        lancamento.Data = new DateOnly(1926, 10, 10);
        Assert.Contains(Validacao.Lancamento(lancamento, []), e => e.Contains("2000"));

        var cartao = CartaoDeTeste(juros: 1.5m);
        Assert.NotEmpty(Validacao.Cartao(cartao));
    }

    [Fact]
    public void R14_pagamento_parcial_deixa_so_o_que_falta()
    {
        var cartao = CartaoDeTeste();

        var fatura = new FaturaCartao
        {
            Id = 1,
            CartaoId = 1,
            Fechamento = D("2026-10-03"),
            Vencimento = D("2026-10-10"),
            TotalInformadoCentavos = 100_000
        };

        var resultado = Projetor.Projetar(new EntradaProjecao
        {
            Configuracao = Cfg("2026-10-01", saldo: 500_000),
            Hoje = D("2026-10-01"),
            FimHorizonte = D("2026-10-31"),
            Cartoes = [cartao],
            Faturas = [fatura],
            PagamentosFatura = [new PagamentoFatura { Id = 1, FaturaId = 1, Data = D("2026-10-05"), ValorCentavos = 30_000 }]
        });

        // O que ja saiu sai no dia em que saiu; o que falta continua marcado no vencimento.
        Assert.Equal(30_000, resultado.Dia(D("2026-10-05"))!.CartaoCentavos);
        Assert.Equal(70_000, resultado.Dia(D("2026-10-10"))!.CartaoCentavos);
        Assert.Equal(400_000, resultado.Dia(D("2026-10-10"))!.SaldoCentavos);
    }

    [Fact]
    public void R14_pagamento_feito_antes_da_ancora_nao_sai_de_novo()
    {
        var cartao = CartaoDeTeste();
        var fatura = new FaturaCartao
        {
            Id = 1,
            CartaoId = 1,
            Fechamento = D("2026-10-03"),
            Vencimento = D("2026-10-10"),
            TotalInformadoCentavos = 100_000
        };

        // 60.000 pagos em 02/10, antes da conferencia com o banco (ancora 04/10): o saldo
        // conferido ja esta sem eles. Os outros 40.000 saem em 06/10, depois da ancora.
        var resultado = Projetor.Projetar(new EntradaProjecao
        {
            Configuracao = Cfg("2026-10-04", saldo: 500_000),
            Hoje = D("2026-10-04"),
            FimHorizonte = D("2026-10-31"),
            Cartoes = [cartao],
            Faturas = [fatura],
            PagamentosFatura =
            [
                new PagamentoFatura { Id = 1, FaturaId = 1, Data = D("2026-10-02"), ValorCentavos = 60_000 },
                new PagamentoFatura { Id = 2, FaturaId = 1, Data = D("2026-10-06"), ValorCentavos = 40_000 }
            ]
        });

        // Somados com a data do ultimo, os 100.000 sairiam em 06/10 e os 60.000 contariam duas vezes.
        Assert.Equal(40_000, resultado.Dias.Sum(d => d.CartaoCentavos));
        Assert.Equal(460_000, resultado.Dia(D("2026-10-31"))!.SaldoCentavos);
    }

    [Fact]
    public void R14_fatura_com_vencimento_empurrado_pelo_banco_continua_no_mesmo_ciclo()
    {
        var cartao = CartaoDeTeste();
        var compra = CompraNoCartao("2026-10-01", 89_000, PapelNaFatura.Detalhamento);

        // O vencimento calculado e 10/10 (sabado); o banco cobra em 12/10 e o dono digita isso.
        var fatura = new FaturaCartao
        {
            Id = 1,
            CartaoId = 1,
            Fechamento = D("2026-10-03"),
            Vencimento = D("2026-10-12"),
            TotalInformadoCentavos = 89_000
        };

        var resultado = Projetor.Projetar(new EntradaProjecao
        {
            Configuracao = Cfg("2026-10-01", saldo: 500_000),
            Hoje = D("2026-10-01"),
            FimHorizonte = D("2026-10-31"),
            Lancamentos = [compra],
            Cartoes = [cartao],
            Faturas = [fatura]
        });

        // Sai uma vez so, no dia que o banco cobra. Casando pela data exata, a compra sairia
        // tambem em 10/10 e o gasto contaria duas vezes.
        Assert.Equal(0, resultado.Dia(D("2026-10-10"))!.CartaoCentavos);
        Assert.Equal(89_000, resultado.Dia(D("2026-10-12"))!.CartaoCentavos);
        Assert.Equal(89_000, resultado.Dias.Sum(d => d.CartaoCentavos));
    }

    [Fact]
    public void R14_segunda_fatura_do_mesmo_ciclo_e_recusada()
    {
        var cartao = CartaoDeTeste();
        var existente = new FaturaCartao { Id = 1, CartaoId = 1, Fechamento = D("2026-10-03"), Vencimento = D("2026-10-10") };
        var nova = new FaturaCartao { Id = 0, CartaoId = 1, Fechamento = D("2026-10-03"), Vencimento = D("2026-10-12") };

        Assert.NotEmpty(Validacao.Fatura(nova, [cartao], [existente]));
        Assert.Empty(Validacao.Fatura(existente, [cartao], [existente]));
    }

    [Fact]
    public void R1_cartao_que_vence_dia_30_mantem_o_dia_nas_parcelas()
    {
        var cartao = new Cartao { Id = 1, Nome = "C", DiaFechamento = 20, DiaVencimento = 30 };
        var compra = L("2026-01-25", TipoLancamento.Cartao, 10_000, vezes: 3);
        compra.CartaoId = 1;

        var datas = Ocorrencias.Do(compra, new Dictionary<int, Cartao> { [1] = cartao }, D("2026-12-31")).ToList();

        // A primeira cai em 28/02 porque fevereiro nao tem dia 30; as outras voltam para o dia 30.
        Assert.Equal([D("2026-02-28"), D("2026-03-30"), D("2026-04-30")], datas);
        Assert.Equal(D("2026-04-30"), Ocorrencias.UltimaVezDo(compra, new Dictionary<int, Cartao> { [1] = cartao }));
    }

    [Fact]
    public void R14_parcelamento_poe_uma_parcela_em_cada_fatura()
    {
        var cartao = CartaoDeTeste();

        // Compra em 3 vezes: uma parcela por ciclo, nunca o total de uma vez.
        var parcelada = L("2026-10-01", TipoLancamento.Cartao, 30_000, vezes: 3);
        parcelada.CartaoId = 1;

        var faturas = new List<FaturaCartao>
        {
            new() { Id = 1, CartaoId = 1, Fechamento = D("2026-10-03"), Vencimento = D("2026-10-10") },
            new() { Id = 2, CartaoId = 1, Fechamento = D("2026-11-03"), Vencimento = D("2026-11-10") },
            new() { Id = 3, CartaoId = 1, Fechamento = D("2026-12-03"), Vencimento = D("2026-12-10") }
        };

        var resumos = Faturas.Resumir(faturas, [parcelada], [cartao], D("2026-10-05"));

        Assert.All(resumos, r => Assert.Equal(30_000, r.TotalCentavos));
        Assert.Equal(3, resumos.Count);
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

    [Fact]
    public void R7_ancora_amanha_e_o_saldo_do_fim_de_hoje()
    {
        // Caso normal depois de conferir com o banco: a ancora e amanha, e o numero conferido
        // e o saldo do inicio de amanha, que e o mesmo do fim de hoje. Sem isto a tela Hoje
        // ficaria sem o numero principal pelo resto do dia em que o dono conferiu.
        var resultado = Projetor.Projetar(EntradaProjecao.De(
            Cfg("2026-10-15", saldo: 382_700, diario: 2_000), D("2026-10-14"),
            fimHorizonte: D("2026-10-31")));

        Assert.Equal(382_700, resultado.Agora.SaldoHojeCentavos);

        // A projecao continua comecando amanha: o diario de hoje nao desconta de novo, porque o
        // numero do banco ja o inclui.
        Assert.Equal(D("2026-10-15"), resultado.Dias[0].Data);
        Assert.Equal(380_700, resultado.Dias[0].SaldoCentavos);
    }

    // ---------- R17: ganho variavel ----------

    [Fact]
    public void R17_ganho_do_mes_conta_nas_entradas_e_na_performance()
    {
        var ganho = new GanhoVariavel { Ano = 2026, Mes = 10, ValorCentavos = 124_700 };

        var resultado = Projetor.Projetar(EntradaProjecao.De(
            Cfg("2026-10-01", saldo: 500_000), D("2026-10-01"),
            lancamentos: [L("2026-10-05", TipoLancamento.Entrada, 420_000)],
            fimHorizonte: D("2026-10-31"),
            ganhosVariaveis: [ganho]));

        var mes = resultado.Mes(2026, 10)!;

        Assert.Equal(124_700, mes.GanhoVariavelCentavos);
        Assert.Equal(544_700, mes.EntradasCentavos);
        Assert.Equal(544_700, mes.PerformanceCentavos);
    }

    [Fact]
    public void R17_ganho_do_mes_nao_mexe_no_saldo_projetado()
    {
        // A decisao do dono: e registro do que JA CAIU. O saldo conferido com o banco ja contem
        // esse dinheiro, entao soma-lo na projecao mostraria dinheiro que nao existe.
        var entrada = EntradaProjecao.De(
            Cfg("2026-10-01", saldo: 500_000), D("2026-10-01"), fimHorizonte: D("2026-10-31"));

        var sem = Projetor.Projetar(entrada);
        var com = Projetor.Projetar(entrada with
        {
            GanhosVariaveis = [new GanhoVariavel { Ano = 2026, Mes = 10, ValorCentavos = 124_700 }]
        });

        Assert.Equal(sem.Dias.Select(d => d.SaldoCentavos), com.Dias.Select(d => d.SaldoCentavos));
        Assert.Equal(sem.Agora.MenorSaldoAFrenteCentavos, com.Agora.MenorSaldoAFrenteCentavos);
    }

    [Fact]
    public void R17_mes_em_branco_nao_chuta_media()
    {
        // Outubro preenchido nao faz novembro render nada: mes em branco vale zero ate alguem
        // responder. E a linha conservadora do app.
        var resultado = Projetor.Projetar(EntradaProjecao.De(
            Cfg("2026-10-01", saldo: 500_000), D("2026-10-01"),
            fimHorizonte: D("2026-11-30"),
            ganhosVariaveis: [new GanhoVariavel { Ano = 2026, Mes = 10, ValorCentavos = 124_700 }]));

        Assert.Equal(124_700, resultado.Mes(2026, 10)!.GanhoVariavelCentavos);
        Assert.Equal(0, resultado.Mes(2026, 11)!.GanhoVariavelCentavos);
        Assert.Equal(0, resultado.Mes(2026, 11)!.EntradasCentavos);
    }

    [Fact]
    public void R17_media_ignora_mes_sem_registro()
    {
        // A media e so referencia de tela, e mes nao preenchido nao vira zero na conta dela:
        // "ainda nao respondi" nao e "ganhei nada".
        List<GanhoVariavel> registros =
        [
            new() { Ano = 2026, Mes = 8, ValorCentavos = 100_000 },
            new() { Ano = 2026, Mes = 9, ValorCentavos = 140_000 }
        ];

        Assert.Equal(120_000, GanhosVariaveis.Media(registros));
        Assert.Null(GanhosVariaveis.Media([]));
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
