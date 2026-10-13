namespace MinhasFinancas.Domain.Modelo;

/// <summary>
/// Entrada do simulador de compra (R11), com entrada e juros absorvidos do documento do GPT (secao 11).
/// Com TaxaMensal = 0 e EntradaCentavos = 0 o comportamento e o do CONTEXTO.md: parcelas iguais.
/// </summary>
public sealed record Simulacao
{
    public string Descricao { get; init; } = "";

    /// <summary>Preco a vista, em centavos. Precisa ser maior que zero.</summary>
    public long ValorTotalCentavos { get; init; }

    /// <summary>Pagamento inicial, entre zero e o preco a vista.</summary>
    public long EntradaCentavos { get; init; }

    /// <summary>Quantidade de parcelas do valor financiado (1 a 36).</summary>
    public int Parcelas { get; init; } = 1;

    /// <summary>Taxa efetiva ao mes, como fracao (0,0199 = 1,99% ao mes). Zero = sem juros.</summary>
    public decimal TaxaMensal { get; init; }

    /// <summary>Obrigatoria quando existe entrada.</summary>
    public DateOnly? DataEntrada { get; init; }

    public DateOnly PrimeiraParcela { get; init; }

    public long ValorFinanciadoCentavos => Math.Max(0, ValorTotalCentavos - EntradaCentavos);
}
