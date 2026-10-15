using MinhasFinancas.Domain.Modelo;

namespace MinhasFinancas.Domain;

/// <summary>
/// A conta aberta de uma fatura (R14). Guarda as parcelas separadas de proposito: um total
/// errado sem a decomposicao visivel vira desconfianca no app inteiro.
/// </summary>
/// <param name="InformadoCentavos">O total que o dono digitou olhando o banco. 0 quando nao informou.</param>
/// <param name="AcrescentadoCentavos">Itens marcados como novo lancamento: entram ALEM do informado.</param>
/// <param name="DetalhadoCentavos">Itens que so descrevem o que ja esta dentro do informado.</param>
/// <param name="DevedorCentavos">O que ainda falta sair da conta.</param>
public sealed record ResumoFatura(
    FaturaCartao Fatura,
    string NomeCartao,
    bool TotalFoiInformado,
    long InformadoCentavos,
    long AcrescentadoCentavos,
    int QuantidadeAcrescentada,
    long DetalhadoCentavos,
    int QuantidadeDetalhada,
    long TotalCentavos,
    long PagoCentavos,
    long DevedorCentavos,
    bool EmAtraso,
    int MesesDeAtraso,
    long JurosCentavos)
{
    /// <summary>O que a projecao desconta: o devedor mais os juros do atraso.</summary>
    public long ASairCentavos => DevedorCentavos + JurosCentavos;
}

/// <summary>R14 a R16: a fatura como gasto previsto, no lugar de cada compra solta.</summary>
public static class Faturas
{
    /// <summary>
    /// Chave de um ciclo. A data de vencimento identifica a fatura porque e ela que o saldo
    /// precisa saber: e o dia em que o dinheiro sai da conta.
    /// </summary>
    public static (int CartaoId, DateOnly Vencimento) Chave(FaturaCartao fatura) =>
        (fatura.CartaoId, fatura.Vencimento);

    /// <summary>
    /// R14. Total efetivo de cada fatura, com a conta aberta.
    ///
    /// Sem total informado, a fatura e a soma de TODOS os itens do ciclo: ai os itens sao a
    /// unica fonte. Com total informado, ele manda, e so os itens marcados como novo lancamento
    /// entram alem dele; detalhamento nao soma de novo, porque ja esta dentro do numero digitado.
    /// E isso que deixa cadastrar so o total, so os itens, ou os dois, sem contar nada duas vezes.
    /// </summary>
    public static List<ResumoFatura> Resumir(
        IEnumerable<FaturaCartao> faturas,
        IEnumerable<Lancamento> lancamentos,
        IReadOnlyList<Cartao> cartoes,
        DateOnly hoje)
    {
        var indiceCartoes = cartoes.ToDictionary(c => c.Id);
        var itens = ItensPorCiclo(lancamentos, indiceCartoes, faturas);
        var resumos = new List<ResumoFatura>();

        foreach (var fatura in faturas.OrderBy(f => f.Vencimento).ThenBy(f => f.CartaoId))
        {
            itens.TryGetValue(Chave(fatura), out var doCiclo);
            doCiclo ??= [];

            var acrescentados = doCiclo.Where(i => i.Papel == PapelNaFatura.NovoLancamento).ToList();
            var detalhados = doCiclo.Where(i => i.Papel == PapelNaFatura.Detalhamento).ToList();

            var acrescentado = acrescentados.Sum(i => i.ValorCentavos);
            var detalhado = detalhados.Sum(i => i.ValorCentavos);

            var informou = fatura.TotalInformadoCentavos is not null;
            var informado = fatura.TotalInformadoCentavos ?? 0;
            var total = informou ? informado + acrescentado : acrescentado + detalhado;

            var devedor = Math.Max(0, total - fatura.PagoCentavos);
            var cartao = indiceCartoes.TryGetValue(fatura.CartaoId, out var c) ? c : null;

            // R15: passou do vencimento e ainda falta pagar. Os juros do rotativo contam por mes
            // cheio de atraso, sobre o que sobrou, nao sobre o total.
            var meses = devedor > 0 && fatura.Vencimento < hoje ? MesesEntre(fatura.Vencimento, hoje) : 0;
            var juros = meses > 0 && cartao is not null
                ? ComJuros(devedor, cartao.JurosRotativoMensal, meses) - devedor
                : 0;

            resumos.Add(new ResumoFatura(
                fatura,
                cartao?.Nome ?? "Cartão apagado",
                informou,
                informado,
                acrescentado,
                acrescentados.Count,
                detalhado,
                detalhados.Count,
                total,
                fatura.PagoCentavos,
                devedor,
                EmAtraso: devedor > 0 && fatura.Vencimento < hoje,
                meses,
                juros));
        }

        return resumos;
    }

    private sealed record ItemDeFatura(long ValorCentavos, PapelNaFatura Papel);

    /// <summary>
    /// As parcelas de lancamentos de Cartao que caem em cada ciclo. Uma compra em 3 vezes gera
    /// uma parcela em cada uma das 3 faturas seguintes: o ciclo so conta a parcela dele.
    /// </summary>
    private static Dictionary<(int, DateOnly), List<ItemDeFatura>> ItensPorCiclo(
        IEnumerable<Lancamento> lancamentos,
        IReadOnlyDictionary<int, Cartao> cartoes,
        IEnumerable<FaturaCartao> faturas)
    {
        var ciclos = faturas.Select(Chave).ToHashSet();
        var limite = ciclos.Count == 0 ? DateOnly.MinValue : ciclos.Max(c => c.Vencimento);
        var itens = new Dictionary<(int, DateOnly), List<ItemDeFatura>>();

        foreach (var lancamento in lancamentos)
        {
            if (lancamento.Tipo != TipoLancamento.Cartao ||
                lancamento.ValorCentavos <= 0 ||
                lancamento.CartaoId is not int cartaoId)
            {
                continue;
            }

            var efetiva = Ocorrencias.DataEfetiva(lancamento, cartoes);

            foreach (var data in Ocorrencias.Ate(efetiva, lancamento.Vezes, limite, lancamento.Periodicidade))
            {
                var chave = (cartaoId, data);

                if (!ciclos.Contains(chave))
                {
                    continue;
                }

                if (!itens.TryGetValue(chave, out var lista))
                {
                    lista = [];
                    itens[chave] = lista;
                }

                lista.Add(new ItemDeFatura(lancamento.ValorCentavos, lancamento.PapelNaFatura));
            }
        }

        return itens;
    }

    /// <summary>Meses cheios entre duas datas. Atraso de 40 dias conta 1 mes, nao 2.</summary>
    public static int MesesEntre(DateOnly de, DateOnly ate)
    {
        var meses = ((ate.Year - de.Year) * 12) + ate.Month - de.Month;

        if (ate.Day < de.Day)
        {
            meses--;
        }

        return Math.Max(0, meses);
    }

    /// <summary>
    /// Juros compostos ao mes, em centavos. Arredonda uma vez no fim: arredondar a cada mes
    /// acumularia erro no sentido do app, e o banco cobra o que cobra.
    /// </summary>
    public static long ComJuros(long centavos, decimal jurosMensal, int meses)
    {
        if (centavos <= 0 || jurosMensal <= 0 || meses <= 0)
        {
            return centavos;
        }

        var fator = 1m;

        for (var i = 0; i < meses; i++)
        {
            fator *= 1m + jurosMensal;
        }

        return (long)Math.Round(centavos * fator, MidpointRounding.AwayFromZero);
    }
}
