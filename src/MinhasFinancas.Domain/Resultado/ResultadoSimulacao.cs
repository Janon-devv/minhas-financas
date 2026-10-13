namespace MinhasFinancas.Domain.Resultado;

public sealed record ParcelaSimulada(
    int Numero,
    DateOnly Data,
    long PagamentoCentavos,
    long JurosCentavos,
    long AmortizacaoCentavos,
    long SaldoDevedorCentavos);

public sealed record ResultadoSimulacao
{
    public IReadOnlyList<ParcelaSimulada> Parcelas { get; init; } = [];

    public long EntradaCentavos { get; init; }

    public DateOnly? DataEntrada { get; init; }

    /// <summary>Entrada mais todas as parcelas.</summary>
    public long TotalPagoCentavos { get; init; }

    /// <summary>Total pago menos o preco a vista.</summary>
    public long JurosTotaisCentavos { get; init; }

    public long? MenorSemCentavos { get; init; }

    public DateOnly? DataMenorSem { get; init; }

    public long? MenorComCentavos { get; init; }

    public DateOnly? DataMenorCom { get; init; }

    public DateOnly? PrimeiroDiaAbaixoConfortoCom { get; init; }

    /// <summary>Menor saldo com a compra menos o conforto. Negativo significa furar o conforto.</summary>
    public long? FolgaCentavos { get; init; }

    public Veredito Veredito { get; init; }

    /// <summary>true quando o cenario ja ficava negativo mesmo sem a compra.</summary>
    public bool JaEstavaNegativoSemCompra { get; init; }

    /// <summary>Quanto o menor saldo piora por causa da compra.</summary>
    public long PioraCentavos { get; init; }

    /// <summary>
    /// Pagamentos que caem depois do fim do horizonte. Maior que zero significa que a conta
    /// mostrada e so o pedaco visivel: o resto do custo nao aparece na projecao.
    /// </summary>
    public long PagamentosForaDoHorizonteCentavos { get; init; }

    public bool ImpactoParcial => PagamentosForaDoHorizonteCentavos > 0;

    /// <summary>Saldo dia a dia com a compra, para desenhar as duas curvas.</summary>
    public IReadOnlyDictionary<DateOnly, long> SaldosComCompra { get; init; } = new Dictionary<DateOnly, long>();
}
