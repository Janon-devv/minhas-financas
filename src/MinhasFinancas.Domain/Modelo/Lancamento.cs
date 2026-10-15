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

    /// <summary>1 = uma vez; N = N ocorrencias; null = sem fim. O intervalo vem de Periodicidade.</summary>
    public int? Vezes { get; set; }

    /// <summary>
    /// Mensal (padrao) ou Semanal. Compra de supermercado acontece toda semana, nao todo dia 5:
    /// forcar tudo em mes espalha o gasto na data errada e o saldo do dia sai errado.
    /// </summary>
    public Periodicidade Periodicidade { get; set; } = Periodicidade.Mensal;

    public int? CartaoId { get; set; }

    /// <summary>
    /// So significa algo quando Tipo = Cartao e existe fatura cadastrada para o ciclo (R14).
    /// </summary>
    public PapelNaFatura PapelNaFatura { get; set; } = PapelNaFatura.NovoLancamento;

    /// <summary>
    /// De qual carteira o dinheiro sai ou entra. null e a conta corrente (R13), que e o que
    /// todo lancamento gravado antes das carteiras existirem quer dizer.
    /// </summary>
    public int? CarteiraId { get; set; }

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
    /// <summary>
    /// Chave propria em vez da data. A partir das carteiras, o mesmo dia tem um diario por
    /// carteira, e o SQLite nao aceita chave composta com coluna anulavel.
    /// </summary>
    public int Id { get; set; }

    public DateOnly Data { get; set; }

    /// <summary>null e a conta corrente (R13).</summary>
    public int? CarteiraId { get; set; }

    public long ValorCentavos { get; set; }
}
