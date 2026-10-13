namespace MinhasFinancas.Domain.Resultado;

/// <summary>Quanto saiu com cada tag num mes (R9).</summary>
public sealed record SaidaPorTag(int? TagId, string Nome, long ValorCentavos, decimal Fracao);

/// <summary>Indicadores de um mes (R8) e tambem uma linha do horizonte de 12 meses (R10).</summary>
public sealed record ResumoMes
{
    public required int Ano { get; init; }

    public required int Mes { get; init; }

    public long EntradasCentavos { get; init; }

    /// <summary>Apenas o tipo Saida.</summary>
    public long SaidasCentavos { get; init; }

    public long CartaoCentavos { get; init; }

    public long DiarioCentavos { get; init; }

    public long EconomiasCentavos { get; init; }

    /// <summary>Entradas - Saidas - Diario - Economias - Cartao.</summary>
    public long PerformanceCentavos { get; init; }

    /// <summary>Saidas + Diario + Cartao. Economia nao entra: e dinheiro que continua seu.</summary>
    public long CustoDeVidaCentavos { get; init; }

    /// <summary>Economias dividido por Entradas. Zero quando nao houve entrada no mes.</summary>
    public decimal EconomizadoFracao { get; init; }

    /// <summary>Media do diario nos dias ja vividos do mes. null quando o mes ainda nao comecou.</summary>
    public long? DiarioMedioCentavos { get; init; }

    public int DiasComDiarioReal { get; init; }

    /// <summary>Entradas Incertas que ficaram de fora da projecao neste mes.</summary>
    public long EntradasIncertasForaCentavos { get; init; }

    public long? SaldoFimCentavos { get; init; }

    public long? MenorSaldoCentavos { get; init; }

    public DateOnly? DataMenorSaldo { get; init; }

    /// <summary>Ja guardado mais todas as economias da ancora ate o fim deste mes.</summary>
    public long GuardadoAcumuladoCentavos { get; init; }

    /// <summary>Farol do MENOR saldo do mes, nao do saldo de fechamento.</summary>
    public Farol Farol { get; init; }

    /// <summary>Quantos dias do mes entraram na conta. O mes da ancora costuma entrar parcial.</summary>
    public int DiasConsiderados { get; init; }

    public IReadOnlyList<SaidaPorTag> SaidasPorTag { get; init; } = [];

    /// <summary>Saidas + Cartao, que e como o resumo mensal apresenta o bloco de contas.</summary>
    public long SaidasECartaoCentavos => SaidasCentavos + CartaoCentavos;

    public DateOnly PrimeiroDia => new(Ano, Mes, 1);

    public DateOnly UltimoDia => new(Ano, Mes, Calendario.UltimoDiaDoMes(Ano, Mes));
}
