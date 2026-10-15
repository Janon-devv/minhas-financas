namespace MinhasFinancas.Domain.Modelo;

public class Cartao
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";

    /// <summary>Dia do mes em que a fatura fecha (1 a 31).</summary>
    public int DiaFechamento { get; set; }

    /// <summary>Dia do mes em que a fatura vence (1 a 31).</summary>
    public int DiaVencimento { get; set; }

    /// <summary>
    /// Juros do rotativo ao mes, como fracao: 0,15 e 15% ao mes. Usado so quando a fatura passa
    /// do vencimento com saldo devedor (R15). Zero significa "nao sei", e ai o app nao inventa
    /// juros nenhum.
    /// </summary>
    public decimal JurosRotativoMensal { get; set; }
}

public class Tag
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";

    /// <summary>
    /// Nome da cor da paleta, nao um hexadecimal livre: cada nome tem par de fundo e texto ja
    /// medido em 4,5:1 no app.css. Cor escolhida a dedo quebraria o contraste sem avisar.
    /// Vazio cai na pilula neutra.
    /// </summary>
    public string Cor { get; set; } = "";
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
