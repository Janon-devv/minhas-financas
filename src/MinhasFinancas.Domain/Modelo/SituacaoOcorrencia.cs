namespace MinhasFinancas.Domain.Modelo;

/// <summary>
/// O que aconteceu com UMA ocorrencia de um lancamento (R12). Um lancamento e a regra
/// ("aluguel todo dia 5"); pago e atrasado sao propriedades de uma ocorrencia dela, nao da regra.
///
/// So existe registro quando a ocorrencia foge do padrao. Sem registro vale o de sempre:
/// ocorrencia a partir da ancora conta na data prevista, e ocorrencia anterior a ancora ja esta
/// dentro do saldo de partida. E por isso que o app nao pode adivinhar atraso: o saldo da ancora
/// e o numero real do banco, e ele nao diz quais contas sairam dele.
/// </summary>
public class SituacaoOcorrencia
{
    public int Id { get; set; }

    public int LancamentoId { get; set; }

    /// <summary>A data prevista da ocorrencia. E ela que identifica de qual ocorrencia se trata.</summary>
    public DateOnly Data { get; set; }

    public bool Pago { get; set; }

    /// <summary>
    /// Nao aconteceu neste periodo e nao vai acontecer: academia trancada, viagem que nao teve.
    /// Diferente de pago, que e dinheiro que saiu, e diferente de em aberto, que e divida.
    /// Ocorrencia pulada some da projecao e da lista, sem mexer no lancamento dos outros meses.
    /// </summary>
    public bool Pulada { get; set; }

    /// <summary>O dia em que saiu de verdade. Pode ser diferente do previsto, que e o caso do atraso.</summary>
    public DateOnly? PagoEm { get; set; }

    /// <summary>
    /// Valor real, quando diferente do previsto. Serve para conta que varia de mes a mes, como luz
    /// e agua: o previsto continua valendo para os meses que ainda nao chegaram.
    /// </summary>
    public long? ValorCentavos { get; set; }

    public string? Obs { get; set; }
}
