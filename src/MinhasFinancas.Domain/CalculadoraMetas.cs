using MinhasFinancas.Domain.Modelo;

namespace MinhasFinancas.Domain;

public enum SituacaoMeta
{
    EmAndamento = 0,
    Concluida = 1,
    PrazoVencido = 2,
    SemPrazo = 3
}

public sealed record ResumoMeta
{
    public required int Id { get; init; }

    public required string Nome { get; init; }

    public long ObjetivoCentavos { get; init; }

    public DateOnly? Prazo { get; init; }

    /// <summary>Saldo inicial da meta mais os aportes que ja aconteceram, da ancora ate hoje.</summary>
    public long GuardadoCentavos { get; init; }

    /// <summary>Quanto ainda falta para o objetivo. Nunca negativo.</summary>
    public long FaltaCentavos { get; init; }

    /// <summary>Pode passar de 1 quando a meta foi superada. A barra limita em 100%, o numero nao.</summary>
    public decimal Progresso { get; init; }

    /// <summary>Contando o mes atual como uma oportunidade de aporte. Minimo 1.</summary>
    public int MesesRestantes { get; init; }

    /// <summary>Quanto reservar por mes para fechar o objetivo no prazo.</summary>
    public long AporteMensalSugeridoCentavos { get; init; }

    /// <summary>Onde a meta chega no prazo mantendo os aportes que ja estao no Plano de Contas.</summary>
    public long ProjetadoNoPrazoCentavos { get; init; }

    public SituacaoMeta Situacao { get; init; }

    /// <summary>true quando os aportes planejados nao alcancam o objetivo ate o prazo.</summary>
    public bool PlanoInsuficiente => Prazo is not null && Situacao != SituacaoMeta.Concluida &&
                                     ProjetadoNoPrazoCentavos < ObjetivoCentavos;
}

/// <summary>
/// Metas de economia. Extra absorvido do documento do GPT (secao 9), adaptado ao modelo deste app:
/// aqui nao existe Previsto contra Realizado, entao "guardado" e o que ja passou pela data de hoje
/// e o resto e plano, do mesmo jeito que o painel Agora trata o guardado total (R7).
/// </summary>
public static class CalculadoraMetas
{
    public static List<ResumoMeta> Calcular(
        IEnumerable<Meta> metas,
        IEnumerable<Lancamento> lancamentos,
        IReadOnlyList<Cartao> cartoes,
        DateOnly ancora,
        DateOnly hoje,
        DateOnly fimHorizonte)
    {
        var indiceCartoes = cartoes.ToDictionary(c => c.Id);
        var aportes = lancamentos
            .Where(l => l.Tipo == TipoLancamento.Economia && l.MetaId is not null && l.ValorCentavos > 0)
            .ToList();

        var resumos = new List<ResumoMeta>();

        foreach (var meta in metas)
        {
            long guardado = meta.SaldoInicialCentavos;
            long aPartirDeHoje = 0;
            var limite = meta.Prazo is DateOnly prazo ? Calendario.Menor(prazo, fimHorizonte) : fimHorizonte;

            foreach (var aporte in aportes.Where(a => a.MetaId == meta.Id))
            {
                var dataEfetiva = Ocorrencias.DataEfetiva(aporte, indiceCartoes);

                foreach (var data in Ocorrencias.Ate(dataEfetiva, aporte.Vezes, limite, aporte.Periodicidade))
                {
                    if (data < ancora)
                    {
                        continue;
                    }

                    if (data <= hoje)
                    {
                        guardado += aporte.ValorCentavos;
                    }
                    else
                    {
                        aPartirDeHoje += aporte.ValorCentavos;
                    }
                }
            }

            var falta = Math.Max(0, meta.ObjetivoCentavos - guardado);
            var mesesRestantes = meta.Prazo is DateOnly p
                ? Math.Max(1, Calendario.DistanciaEmMeses(hoje, p) + 1)
                : 1;

            var situacao = guardado >= meta.ObjetivoCentavos && meta.ObjetivoCentavos > 0
                ? SituacaoMeta.Concluida
                : meta.Prazo is null
                    ? SituacaoMeta.SemPrazo
                    : meta.Prazo < hoje
                        ? SituacaoMeta.PrazoVencido
                        : SituacaoMeta.EmAndamento;

            resumos.Add(new ResumoMeta
            {
                Id = meta.Id,
                Nome = meta.Nome,
                ObjetivoCentavos = meta.ObjetivoCentavos,
                Prazo = meta.Prazo,
                GuardadoCentavos = guardado,
                FaltaCentavos = falta,
                Progresso = meta.ObjetivoCentavos <= 0 ? 0m : (decimal)guardado / meta.ObjetivoCentavos,
                MesesRestantes = mesesRestantes,
                // Teto para cima em centavos: arredondar para baixo deixaria a meta sempre um pouco curta.
                AporteMensalSugeridoCentavos = falta == 0
                    ? 0
                    : (long)Math.Ceiling((decimal)falta / mesesRestantes),
                ProjetadoNoPrazoCentavos = guardado + aPartirDeHoje,
                Situacao = situacao
            });
        }

        return resumos;
    }
}
