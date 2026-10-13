namespace MinhasFinancas.Domain;

/// <summary>Regra R1: em que dia uma compra no cartao realmente sai da conta.</summary>
public static class RegrasCartao
{
    /// <summary>
    /// Compra feita no proprio dia do fechamento ja vai para a fatura seguinte.
    /// Vencimento menor ou igual ao fechamento cai no mes seguinte ao da fatura.
    /// </summary>
    public static DateOnly DataEfetiva(DateOnly dataCompra, int diaFechamento, int diaVencimento)
    {
        var deslocamentoFatura = dataCompra.Day >= diaFechamento ? 1 : 0;
        var deslocamentoVencimento = diaVencimento <= diaFechamento ? 1 : 0;
        return Calendario.AdicionarMeses(dataCompra, deslocamentoFatura + deslocamentoVencimento, diaVencimento);
    }

    public static DateOnly DataEfetiva(DateOnly dataCompra, Modelo.Cartao cartao) =>
        DataEfetiva(dataCompra, cartao.DiaFechamento, cartao.DiaVencimento);
}
