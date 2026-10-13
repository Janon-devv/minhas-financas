using MinhasFinancas.Domain.Modelo;

namespace MinhasFinancas.Domain;

/// <summary>Tudo que o motor precisa para projetar. Nada aqui vem de banco ou de relogio do sistema.</summary>
public sealed record EntradaProjecao
{
    public required Configuracao Configuracao { get; init; }

    public IReadOnlyList<Cartao> Cartoes { get; init; } = [];

    public IReadOnlyList<Lancamento> Lancamentos { get; init; } = [];

    public IReadOnlyList<Tag> Tags { get; init; } = [];

    /// <summary>Diario registrado por data. A chave presente com valor 0 significa "nao gastei nada".</summary>
    public IReadOnlyDictionary<DateOnly, long> DiariosReais { get; init; } = new Dictionary<DateOnly, long>();

    public required DateOnly Hoje { get; init; }

    /// <summary>
    /// Fim do horizonte. Quando null, calcula a partir de HorizonteMeses da configuracao.
    /// O cenario de referencia em docs/fixtures informa a data explicita.
    /// </summary>
    public DateOnly? FimHorizonte { get; init; }

    public Simulacao? Simulacao { get; init; }

    public static EntradaProjecao De(
        Configuracao configuracao,
        DateOnly hoje,
        IEnumerable<Lancamento>? lancamentos = null,
        IEnumerable<Cartao>? cartoes = null,
        IEnumerable<Tag>? tags = null,
        IEnumerable<DiarioReal>? diarios = null,
        DateOnly? fimHorizonte = null,
        Simulacao? simulacao = null) => new()
        {
            Configuracao = configuracao,
            Hoje = hoje,
            Lancamentos = lancamentos?.ToList() ?? [],
            Cartoes = cartoes?.ToList() ?? [],
            Tags = tags?.ToList() ?? [],
            DiariosReais = diarios?.ToDictionary(d => d.Data, d => d.ValorCentavos) ?? [],
            FimHorizonte = fimHorizonte,
            Simulacao = simulacao
        };
}
