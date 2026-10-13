namespace MinhasFinancas.Domain.Resultado;

/// <summary>Painel Agora (R7). Os campos sao anulaveis porque a ancora pode estar no futuro.</summary>
public sealed record ResumoAgora
{
    public required DateOnly Referencia { get; init; }

    public long? SaldoHojeCentavos { get; init; }

    public long? MenorSaldoAFrenteCentavos { get; init; }

    public DateOnly? DataMenorSaldoAFrente { get; init; }

    /// <summary>max(0, menor saldo a frente - saldo de conforto).</summary>
    public long PodeGastarHojeCentavos { get; init; }

    /// <summary>null significa que nunca fica abaixo do conforto dentro do horizonte.</summary>
    public DateOnly? PrimeiroDiaAbaixoConforto { get; init; }

    public long GuardadoAteHojeCentavos { get; init; }

    /// <summary>Farol aplicado ao menor saldo a frente, que e o numero que decide se vem aperto.</summary>
    public Farol Farol { get; init; }

    /// <summary>true quando hoje e anterior a ancora: ainda nao existe saldo projetado para hoje.</summary>
    public bool AnteriorAAncora { get; init; }
}
