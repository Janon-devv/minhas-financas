using MinhasFinancas.Domain.Modelo;

namespace MinhasFinancas.Domain;

/// <summary>
/// R13: resolve a lista de carteiras que o motor projeta. Funcao pura.
/// </summary>
public static class Carteiras
{
    /// <summary>
    /// Id reservado da conta corrente. Ela nao e uma linha de banco: e a propria Configuracao,
    /// sintetizada aqui. Um lancamento com CarteiraId null pertence a ela.
    /// </summary>
    public const int IdPrincipal = 0;

    /// <summary>
    /// A conta corrente na frente, as demais na ordem definida. Sem nenhuma carteira cadastrada
    /// sobra so a conta corrente, com exatamente os numeros da Configuracao: e isso que mantem
    /// um cenario sem carteira produzindo os mesmos centavos de sempre.
    /// </summary>
    public static List<Carteira> Resolver(IEnumerable<Carteira> cadastradas, Configuracao cfg)
    {
        var principal = new Carteira
        {
            Id = IdPrincipal,
            Nome = "Conta corrente",
            Tipo = TipoCarteira.Banco,
            SaldoAncoraCentavos = cfg.SaldoAncoraCentavos,
            DataAncora = cfg.DataAncora,
            DiarioPrevistoCentavos = cfg.DiarioPrevistoAtivo ? cfg.DiarioPrevistoCentavos : null,
            SaldoConfortoCentavos = cfg.SaldoConfortoCentavos,
            Ordem = -1
        };

        return
        [
            principal,
            .. cadastradas.Where(c => c.Id != IdPrincipal).OrderBy(c => c.Ordem).ThenBy(c => c.Id)
        ];
    }

    /// <summary>A carteira de um lancamento. null e a conta corrente.</summary>
    public static int Da(int? carteiraId) => carteiraId ?? IdPrincipal;

    /// <summary>
    /// A configuracao equivalente de uma carteira, para o motor projetar qualquer uma com o
    /// mesmo codigo. Carteira sem diario previsto projeta com a contagem desligada, que e o
    /// mesmo que dizer que dia sem registro nao desconta nada (R4).
    /// </summary>
    public static Configuracao ConfiguracaoDe(Carteira carteira, Configuracao cfg) =>
        carteira.Id == IdPrincipal
            ? cfg
            : new Configuracao
            {
                Id = cfg.Id,
                SaldoAncoraCentavos = carteira.SaldoAncoraCentavos,
                DataAncora = carteira.DataAncora,
                DiarioPrevistoCentavos = carteira.DiarioPrevistoCentavos ?? 0,
                DiarioPrevistoAtivo = carteira.DiarioPrevistoCentavos is not null,
                SaldoConfortoCentavos = carteira.SaldoConfortoCentavos,
                ContarEntradasIncertas = cfg.ContarEntradasIncertas,

                // Economia e meta sao da conta corrente: o VA nao guarda dinheiro para o futuro.
                JaGuardadoCentavos = 0,

                HorizonteMeses = cfg.HorizonteMeses,
                ConfiguracaoConcluida = cfg.ConfiguracaoConcluida
            };
}
