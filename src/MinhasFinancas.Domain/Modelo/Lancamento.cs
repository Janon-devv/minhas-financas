namespace MinhasFinancas.Domain.Modelo;

/// <summary>Uma linha do Plano de Contas. O valor e de CADA ocorrencia (parcela), nunca o total.</summary>
public class Lancamento
{
    public int Id { get; set; }

    /// <summary>Para Cartao: data da COMPRA. Para os demais: data em que cai na conta.</summary>
    public DateOnly Data { get; set; }

    public string Descricao { get; set; } = "";
    public TipoLancamento Tipo { get; set; }

    /// <summary>Valor de cada ocorrencia, em centavos. Precisa ser maior que zero.</summary>
    public long ValorCentavos { get; set; }

    /// <summary>1 = uma vez; N = N ocorrencias mensais; null = todo mes, sem fim.</summary>
    public int? Vezes { get; set; }

    public int? CartaoId { get; set; }

    /// <summary>So para Entrada. null equivale a Certa.</summary>
    public Certeza? Certeza { get; set; }

    public int? TagId { get; set; }

    /// <summary>So para Economia: vincula o aporte a uma meta.</summary>
    public int? MetaId { get; set; }

    public string? Obs { get; set; }

    public bool EntradaIncerta => Tipo == TipoLancamento.Entrada && Certeza == Domain.Certeza.Incerta;
}

/// <summary>
/// Gasto miudo registrado para uma data. Ausencia de registro significa "usa o previsto";
/// um registro com valor 0 significa "nao gastei nada". Sao coisas diferentes.
/// </summary>
public class DiarioReal
{
    public DateOnly Data { get; set; }
    public long ValorCentavos { get; set; }
}
