namespace MinhasFinancas.Domain.Resultado;

/// <summary>Um movimento individual que caiu num dia, para a UI abrir o detalhe sem recalcular.</summary>
public sealed record MovimentoDoDia(
    int LancamentoId,
    string Descricao,
    TipoLancamento Tipo,
    long ValorCentavos,
    int? TagId,
    int? CartaoId,
    int? MetaId);

/// <summary>Uma linha do calendario (R4, R5, R6).</summary>
public sealed record DiaProjetado
{
    public required DateOnly Data { get; init; }

    public long EntradasCentavos { get; init; }

    /// <summary>Apenas o tipo Saida. Para a saida total do dia use SaidaTotalCentavos.</summary>
    public long SaidasCentavos { get; init; }

    public long CartaoCentavos { get; init; }

    public long EconomiasCentavos { get; init; }

    public long DiarioCentavos { get; init; }

    /// <summary>true quando existe registro de DiarioReal para esta data, inclusive com valor 0.</summary>
    public bool DiarioEReal { get; init; }

    public long SaldoCentavos { get; init; }

    public Farol Farol { get; init; }

    public IReadOnlyList<MovimentoDoDia> Movimentos { get; init; } = [];

    /// <summary>Tudo que sai da conta no dia, fora o diario: Saida + Cartao + Economia.</summary>
    public long SaidaTotalCentavos => SaidasCentavos + CartaoCentavos + EconomiasCentavos;

    public long ResultadoCentavos => EntradasCentavos - SaidaTotalCentavos - DiarioCentavos;

    public bool TemMovimento => EntradasCentavos != 0 || SaidaTotalCentavos != 0;
}
