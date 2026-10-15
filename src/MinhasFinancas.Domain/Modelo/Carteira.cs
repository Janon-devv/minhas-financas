namespace MinhasFinancas.Domain.Modelo;

/// <summary>
/// Uma bolsa de dinheiro separada da conta corrente: vale-alimentacao, vale-transporte.
/// O saldo dela nao se mistura com o do banco, porque o dinheiro tambem nao se mistura:
/// almoco pago no VA nao deixa a conta corrente mais pobre, e o VA nao paga aluguel.
///
/// A conta corrente NAO tem linha aqui. Ela e a propria <see cref="Configuracao"/>, e e
/// sintetizada como carteira principal na hora de projetar (R13). Guardar o saldo do banco
/// em dois lugares criaria duas verdades para o mesmo numero, e a conferencia com o extrato
/// passaria a depender de qual delas foi gravada por ultimo.
/// </summary>
public class Carteira
{
    public int Id { get; set; }

    public string Nome { get; set; } = "";

    public TipoCarteira Tipo { get; set; } = TipoCarteira.Beneficio;

    /// <summary>Saldo no INICIO do dia da ancora desta carteira.</summary>
    public long SaldoAncoraCentavos { get; set; }

    /// <summary>Cada carteira confere no seu ritmo: o app do VA nao atualiza junto com o banco.</summary>
    public DateOnly DataAncora { get; set; }

    /// <summary>
    /// Gasto miudo por dia desta carteira. null quer dizer carteira sem diario: vale-transporte
    /// com valor fixo por dia faz sentido, vale-alimentacao as vezes nao, e obrigar um numero
    /// para todas inventaria gasto onde nao ha.
    /// </summary>
    public long? DiarioPrevistoCentavos { get; set; }

    public long SaldoConfortoCentavos { get; set; }

    /// <summary>Ordem de exibicao na tela Hoje.</summary>
    public int Ordem { get; set; }
}
