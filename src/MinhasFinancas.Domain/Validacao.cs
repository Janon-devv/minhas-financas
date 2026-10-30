using MinhasFinancas.Domain.Modelo;

namespace MinhasFinancas.Domain;

/// <summary>
/// Regras de "pode salvar?" (CONTEXTO.md secao 11). Ficam no dominio para a tela e qualquer
/// importacao futura usarem o mesmo criterio.
/// </summary>
public static class Validacao
{
    /// <summary>
    /// Faixa de datas aceitas. Fora dela e erro de digitacao (2006 em vez de 2026), e uma data
    /// assim nao e so um numero errado: decadas de juros ou de ocorrencias mensais quebram a conta.
    /// </summary>
    public static readonly DateOnly DataMinima = new(2000, 1, 1);

    public static readonly DateOnly DataMaxima = new(2100, 12, 31);

    /// <summary>
    /// Maior valor aceito, em centavos: R$ 9.999.999.999,99, o mesmo teto do campo de dinheiro.
    /// Acima disso e zero a mais, e somas de milhares de dias com valores assim estourariam.
    /// </summary>
    public const long ValorMaximoCentavos = 9_999_999_999_99;

    /// <summary>Teto de juros ao mes, como fracao: 1 e 100% ao mes.</summary>
    public const decimal JurosMaximoMensal = 1m;

    /// <summary>Teto de repeticoes de um lancamento, o mesmo do campo "quantas vezes".</summary>
    public const int VezesMaximo = 360;

    public static bool DataPlausivel(DateOnly data) => data >= DataMinima && data <= DataMaxima;

    public static bool ValorPlausivel(long centavos) => Math.Abs(centavos) <= ValorMaximoCentavos;

    private static string ForaDaFaixa(string oQue) =>
        $"{oQue} precisa ficar entre {DataMinima.Year} e {DataMaxima.Year}.";

    private const string ValorAltoDemais = "Valor alto demais: confira os zeros.";

    public static List<string> Lancamento(
        Lancamento lancamento,
        IReadOnlyList<Cartao> cartoes,
        IReadOnlyList<Carteira>? carteiras = null)
    {
        var erros = new List<string>();

        if (lancamento.CarteiraId is int carteiraId && carteiras is not null &&
            carteiras.All(c => c.Id != carteiraId))
        {
            erros.Add("A carteira escolhida não existe mais.");
        }

        if (string.IsNullOrWhiteSpace(lancamento.Descricao))
        {
            erros.Add("Escreva uma descrição.");
        }

        if (lancamento.ValorCentavos <= 0)
        {
            erros.Add("O valor precisa ser maior que zero.");
        }
        else if (!ValorPlausivel(lancamento.ValorCentavos))
        {
            erros.Add(ValorAltoDemais);
        }

        if (lancamento.Data == default)
        {
            erros.Add("Informe a data.");
        }
        else if (!DataPlausivel(lancamento.Data))
        {
            erros.Add(ForaDaFaixa("A data"));
        }

        if (lancamento.Vezes is int vezes && vezes < 1)
        {
            erros.Add("Quantas vezes precisa ser pelo menos 1.");
        }
        else if (lancamento.Vezes > VezesMaximo)
        {
            erros.Add($"Quantas vezes vai até {VezesMaximo}. Para mais que isso, use sem fim.");
        }

        // A data efetiva de um lancamento de Cartao e o vencimento da fatura, calculado uma vez
        // a partir da compra. Repetir isso de 7 em 7 dias geraria vencimentos que nao existem:
        // compra semanal no cartao e varias compras caindo na MESMA fatura, nao uma fatura por
        // semana. Enquanto a R1 nao for aplicada por ocorrencia, semanal so vale fora do cartao.
        if (lancamento.Tipo == TipoLancamento.Cartao && lancamento.Periodicidade == Periodicidade.Semanal)
        {
            erros.Add("Cartão não aceita repetição semanal: as compras da semana caem na mesma fatura.");
        }

        if (lancamento.Tipo == TipoLancamento.Cartao)
        {
            if (lancamento.CartaoId is null)
            {
                erros.Add("Escolha o cartão.");
            }
            else if (cartoes.All(c => c.Id != lancamento.CartaoId))
            {
                erros.Add("O cartão escolhido não existe mais.");
            }
        }

        if (lancamento.Tipo != TipoLancamento.Entrada && lancamento.Certeza is not null)
        {
            erros.Add("Certeza só vale para entrada.");
        }

        if (lancamento.Tipo != TipoLancamento.Economia && lancamento.MetaId is not null)
        {
            erros.Add("Meta só vale para economia.");
        }

        return erros;
    }

    public static List<string> Configuracao(Configuracao cfg)
    {
        var erros = new List<string>();

        if (cfg.DataAncora == default)
        {
            erros.Add("Informe a data do saldo.");
        }
        else if (!DataPlausivel(cfg.DataAncora))
        {
            erros.Add(ForaDaFaixa("A data do saldo"));
        }

        if (!ValorPlausivel(cfg.SaldoAncoraCentavos) || !ValorPlausivel(cfg.DiarioPrevistoCentavos) ||
            !ValorPlausivel(cfg.SaldoConfortoCentavos) || !ValorPlausivel(cfg.JaGuardadoCentavos))
        {
            erros.Add(ValorAltoDemais);
        }

        if (cfg.DiarioPrevistoCentavos < 0)
        {
            erros.Add("O diário previsto não pode ser negativo.");
        }

        if (cfg.SaldoConfortoCentavos < 0)
        {
            erros.Add("O saldo de conforto não pode ser negativo.");
        }

        if (cfg.HorizonteMeses is < 1 or > 120)
        {
            erros.Add("O horizonte precisa ficar entre 1 e 120 meses.");
        }

        return erros;
    }

    public static List<string> Carteira(Carteira carteira)
    {
        var erros = new List<string>();

        if (string.IsNullOrWhiteSpace(carteira.Nome))
        {
            erros.Add("Dê um nome à carteira.");
        }

        if (carteira.DataAncora == default)
        {
            erros.Add("Informe a data do saldo da carteira.");
        }
        else if (!DataPlausivel(carteira.DataAncora))
        {
            erros.Add(ForaDaFaixa("A data do saldo da carteira"));
        }

        if (!ValorPlausivel(carteira.SaldoAncoraCentavos) || !ValorPlausivel(carteira.DiarioPrevistoCentavos ?? 0) ||
            !ValorPlausivel(carteira.SaldoConfortoCentavos))
        {
            erros.Add(ValorAltoDemais);
        }

        // Beneficio nao entra no cheque especial: o cartao do VA simplesmente recusa a compra.
        if (carteira.Tipo == TipoCarteira.Beneficio && carteira.SaldoAncoraCentavos < 0)
        {
            erros.Add("Saldo de benefício não pode ser negativo.");
        }

        if (carteira.DiarioPrevistoCentavos is long diario && diario < 0)
        {
            erros.Add("O diário previsto da carteira não pode ser negativo.");
        }

        if (carteira.SaldoConfortoCentavos < 0)
        {
            erros.Add("O saldo de conforto da carteira não pode ser negativo.");
        }

        return erros;
    }

    public static List<string> Cartao(Cartao cartao)
    {
        var erros = new List<string>();

        if (string.IsNullOrWhiteSpace(cartao.Nome))
        {
            erros.Add("Dê um nome ao cartão.");
        }

        if (cartao.DiaFechamento is < 1 or > 31)
        {
            erros.Add("O dia de fechamento precisa ficar entre 1 e 31.");
        }

        if (cartao.DiaVencimento is < 1 or > 31)
        {
            erros.Add("O dia de vencimento precisa ficar entre 1 e 31.");
        }

        if (cartao.JurosRotativoMensal < 0 || cartao.JurosRotativoMensal > JurosMaximoMensal)
        {
            erros.Add("Os juros do rotativo precisam ficar entre 0% e 100% ao mês.");
        }

        return erros;
    }

    /// <summary>
    /// Uma fatura por ciclo. O ciclo e o de <see cref="Faturas.Chave"/>, e nao a data digitada:
    /// duas faturas do mesmo ciclo com vencimentos diferentes (um deles empurrado pelo banco)
    /// receberiam as mesmas compras, e o ciclo seria descontado duas vezes.
    /// </summary>
    public static List<string> Fatura(
        FaturaCartao fatura,
        IReadOnlyList<Cartao> cartoes,
        IEnumerable<FaturaCartao> existentes)
    {
        var erros = new List<string>();
        var cartao = cartoes.FirstOrDefault(c => c.Id == fatura.CartaoId);

        if (cartao is null)
        {
            erros.Add("Escolha o cartão.");
        }

        if (fatura.Vencimento == default)
        {
            erros.Add("Informe o vencimento.");
        }
        else if (!DataPlausivel(fatura.Vencimento) || (fatura.Fechamento != default && !DataPlausivel(fatura.Fechamento)))
        {
            erros.Add(ForaDaFaixa("As datas da fatura"));
        }
        else if (fatura.Fechamento != default && fatura.Fechamento > fatura.Vencimento)
        {
            erros.Add("O fechamento vem antes do vencimento.");
        }

        if (fatura.TotalInformadoCentavos is long total && total < 0)
        {
            erros.Add("O total da fatura não pode ser negativo.");
        }
        else if (!ValorPlausivel(fatura.TotalInformadoCentavos ?? 0))
        {
            erros.Add(ValorAltoDemais);
        }

        if (cartao is not null && fatura.Vencimento != default && DataPlausivel(fatura.Vencimento))
        {
            var indice = cartoes.ToDictionary(c => c.Id);
            var ciclo = Faturas.Chave(fatura, indice);
            var outra = existentes.FirstOrDefault(f => f.Id != fatura.Id && Faturas.Chave(f, indice) == ciclo);

            if (outra is not null)
            {
                erros.Add($"Esse cartão já tem a fatura que vence em {outra.Vencimento:dd/MM/yyyy}, " +
                          "do mesmo ciclo. Edite aquela em vez de criar outra.");
            }
        }

        return erros;
    }

    public static List<string> PagamentoFatura(long valorCentavos, DateOnly data)
    {
        var erros = new List<string>();

        if (valorCentavos <= 0)
        {
            erros.Add("Informe quanto foi pago.");
        }
        else if (!ValorPlausivel(valorCentavos))
        {
            erros.Add(ValorAltoDemais);
        }

        if (data == default)
        {
            erros.Add("Informe a data do pagamento.");
        }
        else if (!DataPlausivel(data))
        {
            erros.Add(ForaDaFaixa("A data do pagamento"));
        }

        return erros;
    }

    /// <summary>Zero vale ("este mes nao rendeu nada"); negativo nao existe em renda que ja caiu.</summary>
    public static List<string> GanhoVariavel(long valorCentavos)
    {
        var erros = new List<string>();

        if (valorCentavos < 0)
        {
            erros.Add("O valor recebido não pode ser negativo.");
        }
        else if (!ValorPlausivel(valorCentavos))
        {
            erros.Add(ValorAltoDemais);
        }

        return erros;
    }

    public static List<string> Meta(Meta meta)
    {
        var erros = new List<string>();

        if (string.IsNullOrWhiteSpace(meta.Nome))
        {
            erros.Add("Dê um nome à meta.");
        }

        if (meta.ObjetivoCentavos <= 0)
        {
            erros.Add("O objetivo precisa ser maior que zero.");
        }

        if (meta.SaldoInicialCentavos < 0)
        {
            erros.Add("O valor já guardado não pode ser negativo.");
        }

        if (!ValorPlausivel(meta.ObjetivoCentavos) || !ValorPlausivel(meta.SaldoInicialCentavos))
        {
            erros.Add(ValorAltoDemais);
        }

        if (meta.Prazo is DateOnly prazo && !DataPlausivel(prazo))
        {
            erros.Add(ForaDaFaixa("O prazo"));
        }

        return erros;
    }

    public static List<string> Simulacao(Simulacao simulacao, DateOnly hoje)
    {
        var erros = new List<string>();

        if (simulacao.ValorTotalCentavos <= 0)
        {
            erros.Add("O preço precisa ser maior que zero.");
        }
        else if (!ValorPlausivel(simulacao.ValorTotalCentavos))
        {
            erros.Add(ValorAltoDemais);
        }

        if (simulacao.EntradaCentavos < 0 || simulacao.EntradaCentavos > simulacao.ValorTotalCentavos)
        {
            erros.Add("A entrada precisa ficar entre zero e o preço.");
        }

        if (simulacao.Parcelas is < 1 or > Simulador.MaximoParcelas)
        {
            erros.Add($"A quantidade de parcelas precisa ficar entre 1 e {Simulador.MaximoParcelas}.");
        }

        if (simulacao.TaxaMensal < 0)
        {
            erros.Add("Os juros não podem ser negativos.");
        }
        else if (simulacao.TaxaMensal > JurosMaximoMensal)
        {
            erros.Add("Os juros precisam ficar entre 0% e 100% ao mês.");
        }

        if ((simulacao.DataEntrada is DateOnly entradaInformada && !DataPlausivel(entradaInformada)) ||
            (simulacao.PrimeiraParcela != default && !DataPlausivel(simulacao.PrimeiraParcela)))
        {
            erros.Add(ForaDaFaixa("As datas"));
        }

        if (simulacao.EntradaCentavos > 0 && simulacao.DataEntrada is null)
        {
            erros.Add("Informe a data da entrada.");
        }

        if (simulacao.DataEntrada is DateOnly dataEntrada && dataEntrada < hoje)
        {
            erros.Add("A data da entrada não pode estar no passado.");
        }

        if (simulacao.ValorFinanciadoCentavos > 0)
        {
            if (simulacao.PrimeiraParcela == default)
            {
                erros.Add("Informe a data da primeira parcela.");
            }
            else if (simulacao.PrimeiraParcela < hoje)
            {
                erros.Add("A primeira parcela não pode estar no passado.");
            }
            else if (simulacao.DataEntrada is DateOnly entrada && simulacao.PrimeiraParcela < entrada)
            {
                erros.Add("A primeira parcela não pode vir antes da entrada.");
            }
        }

        return erros;
    }
}
