namespace MinhasFinancas.Domain.Modelo;

/// <summary>
/// A fatura de um cartao num ciclo (R14). Existe para o dono poder informar o TOTAL que o banco
/// mostra, sem ter que cadastrar compra por compra: registrar cada item de uma fatura e um
/// trabalho que ninguem mantem por muito tempo, e uma fatura pela metade mente mais do que
/// nenhuma.
///
/// Ciclo sem fatura cadastrada continua na R1: cada compra sai sozinha no vencimento dela.
/// </summary>
public class FaturaCartao
{
    public int Id { get; set; }

    public int CartaoId { get; set; }

    /// <summary>
    /// O vencimento identifica o ciclo. E a data em que o dinheiro sai da conta, que e a unica
    /// que o saldo projetado precisa saber.
    /// </summary>
    public DateOnly Vencimento { get; set; }

    /// <summary>Quando a fatura fechou. So informativo: ajuda a saber se a compra de hoje entra nela.</summary>
    public DateOnly Fechamento { get; set; }

    /// <summary>
    /// O total que o banco mostra. null significa "calcule pela soma dos itens". Informado,
    /// ele manda, e os itens marcados como detalhamento NAO sao somados de novo (R14).
    /// Continua editavel a mao mesmo quando foi calculado.
    /// </summary>
    public long? TotalInformadoCentavos { get; set; }

    /// <summary>Quanto ja foi pago desta fatura. Pagamento parcial acumula aqui.</summary>
    public long PagoCentavos { get; set; }

    public DateOnly? PagoEm { get; set; }

    public string? Obs { get; set; }
}
