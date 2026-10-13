namespace MinhasFinancas.Domain.Modelo;

public class Cartao
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";

    /// <summary>Dia do mes em que a fatura fecha (1 a 31).</summary>
    public int DiaFechamento { get; set; }

    /// <summary>Dia do mes em que a fatura vence (1 a 31).</summary>
    public int DiaVencimento { get; set; }
}

public class Tag
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
}

/// <summary>
/// Meta de economia. Extra absorvido do documento do GPT (secao 9): da objetivo, prazo e aporte
/// sugerido ao tipo Economia, que no CONTEXTO.md era so um acumulado.
/// </summary>
public class Meta
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public long ObjetivoCentavos { get; set; }
    public DateOnly? Prazo { get; set; }

    /// <summary>Quanto desta meta ja estava guardado antes da ancora.</summary>
    public long SaldoInicialCentavos { get; set; }

    public DateTimeOffset? ArquivadaEm { get; set; }
}
