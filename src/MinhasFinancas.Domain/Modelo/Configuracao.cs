namespace MinhasFinancas.Domain.Modelo;

/// <summary>Registro unico de configuracao (CONTEXTO.md secao 5). Dinheiro sempre em centavos.</summary>
public class Configuracao
{
    public int Id { get; set; } = 1;

    /// <summary>Saldo da conta no INICIO do dia da ancora, antes dos movimentos desse dia. Pode ser negativo.</summary>
    public long SaldoAncoraCentavos { get; set; }

    public DateOnly DataAncora { get; set; }

    /// <summary>Gasto miudo esperado por dia. Na duvida, errar pra mais.</summary>
    public long DiarioPrevistoCentavos { get; set; }

    /// <summary>Limite pessoal: abaixo dele o farol fica amarelo.</summary>
    public long SaldoConfortoCentavos { get; set; }

    public bool ContarEntradasIncertas { get; set; }

    /// <summary>Quanto ja estava guardado antes da ancora. So para acompanhamento de economias.</summary>
    public long JaGuardadoCentavos { get; set; }

    public int HorizonteMeses { get; set; } = 24;

    /// <summary>Marca se o usuario ja concluiu o onboarding.</summary>
    public bool ConfiguracaoConcluida { get; set; }
}

/// <summary>Historico de ancoras, para auditoria da conferencia semanal com o banco.</summary>
public class AncoraHistorico
{
    public int Id { get; set; }
    public DateOnly Data { get; set; }
    public long SaldoCentavos { get; set; }
    public DateTimeOffset CriadoEm { get; set; }
    public string? Observacao { get; set; }
}
