using MinhasFinancas.Domain.Modelo;

namespace MinhasFinancas.Domain;

/// <summary>Tudo que o motor precisa para projetar. Nada aqui vem de banco ou de relogio do sistema.</summary>
public sealed record EntradaProjecao
{
    public required Configuracao Configuracao { get; init; }

    public IReadOnlyList<Cartao> Cartoes { get; init; } = [];

    public IReadOnlyList<Lancamento> Lancamentos { get; init; } = [];

    public IReadOnlyList<Tag> Tags { get; init; } = [];

    /// <summary>
    /// O que foi registrado sobre ocorrencias especificas (R12). Lista vazia significa tudo no
    /// padrao, que e exatamente o comportamento de antes desta regra existir.
    /// </summary>
    public IReadOnlyList<SituacaoOcorrencia> Situacoes { get; init; } = [];

    /// <summary>
    /// Faturas cadastradas (R14). Ciclo sem fatura aqui continua na R1: cada compra sai sozinha
    /// na data de vencimento dela, que e o comportamento de antes das faturas existirem.
    /// </summary>
    public IReadOnlyList<FaturaCartao> Faturas { get; init; } = [];

    /// <summary>Pagamentos das faturas, cada um com a data em que saiu da conta (R14).</summary>
    public IReadOnlyList<PagamentoFatura> PagamentosFatura { get; init; } = [];

    /// <summary>
    /// Ganho variavel registrado mes a mes (R17). Nao mexe no saldo projetado: e registro do que
    /// ja caiu, e o saldo conferido com o banco ja o contem. Entra so nos ganhos DO MES (R8).
    /// </summary>
    public IReadOnlyList<GanhoVariavel> GanhosVariaveis { get; init; } = [];

    /// <summary>
    /// Carteiras alem da conta corrente (R13). Lista vazia deixa so a conta corrente, que e o
    /// comportamento de antes de elas existirem.
    /// </summary>
    public IReadOnlyList<Carteira> Carteiras { get; init; } = [];

    /// <summary>
    /// Diario registrado por data, da CONTA CORRENTE. A chave presente com valor 0 significa
    /// "nao gastei nada". Continua existindo porque e assim que o cenario de referencia informa.
    /// </summary>
    public IReadOnlyDictionary<DateOnly, long> DiariosReais { get; init; } = new Dictionary<DateOnly, long>();

    /// <summary>
    /// Diario registrado de qualquer carteira. Soma-se ao dicionario acima; em conflito no mesmo
    /// dia da conta corrente, este manda, por ser o registro completo.
    /// </summary>
    public IReadOnlyList<DiarioReal> Diarios { get; init; } = [];

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
        Simulacao? simulacao = null,
        IEnumerable<SituacaoOcorrencia>? situacoes = null,
        IEnumerable<Carteira>? carteiras = null,
        IEnumerable<FaturaCartao>? faturas = null,
        IEnumerable<GanhoVariavel>? ganhosVariaveis = null,
        IEnumerable<PagamentoFatura>? pagamentosFatura = null) => new()
        {
            Configuracao = configuracao,
            Hoje = hoje,
            Lancamentos = lancamentos?.ToList() ?? [],
            Cartoes = cartoes?.ToList() ?? [],
            Tags = tags?.ToList() ?? [],
            DiariosReais = diarios?.Where(d => d.CarteiraId is null).ToDictionary(d => d.Data, d => d.ValorCentavos) ?? [],
            Diarios = diarios?.ToList() ?? [],
            Carteiras = carteiras?.ToList() ?? [],
            Faturas = faturas?.ToList() ?? [],
            PagamentosFatura = pagamentosFatura?.ToList() ?? [],
            GanhosVariaveis = ganhosVariaveis?.ToList() ?? [],
            FimHorizonte = fimHorizonte,
            Simulacao = simulacao,
            Situacoes = situacoes?.ToList() ?? []
        };
}
