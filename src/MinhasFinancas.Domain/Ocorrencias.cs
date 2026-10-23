using MinhasFinancas.Domain.Modelo;

namespace MinhasFinancas.Domain;

/// <summary>Regra R2: em quais datas um lancamento do Plano de Contas realmente acontece.</summary>
public static class Ocorrencias
{
    /// <summary>
    /// Data em que o dinheiro sai da conta. Para Cartao, o vencimento da fatura (R1).
    /// Cartao sem cartao valido cai na data da compra, que e a hipotese conservadora:
    /// e melhor o dinheiro aparecer saindo cedo demais do que desaparecer da projecao.
    /// </summary>
    public static DateOnly DataEfetiva(Lancamento lancamento, IReadOnlyDictionary<int, Cartao> cartoes)
    {
        if (lancamento.Tipo != TipoLancamento.Cartao)
        {
            return lancamento.Data;
        }

        if (lancamento.CartaoId is int id && cartoes.TryGetValue(id, out var cartao))
        {
            return RegrasCartao.DataEfetiva(lancamento.Data, cartao);
        }

        return lancamento.Data;
    }

    /// <summary>
    /// O dia do mes em que as ocorrencias mensais caem. Para Cartao e o dia de vencimento do
    /// cartao, e nao o dia da primeira data efetiva: um cartao que vence dia 30 tem a primeira
    /// fatura em 28/02 quando a compra e de janeiro, e usar esse 28 grudaria todas as parcelas
    /// seguintes no dia 28, fora do vencimento real e fora da fatura cadastrada para o ciclo.
    /// </summary>
    public static int DiaDeReferencia(Lancamento lancamento, IReadOnlyDictionary<int, Cartao> cartoes) =>
        lancamento.Tipo == TipoLancamento.Cartao &&
        lancamento.CartaoId is int id &&
        cartoes.TryGetValue(id, out var cartao)
            ? cartao.DiaVencimento
            : lancamento.Data.Day;

    /// <summary>Todas as ocorrencias de um lancamento ate o limite, com data efetiva e dia certos.</summary>
    public static IEnumerable<DateOnly> Do(
        Lancamento lancamento,
        IReadOnlyDictionary<int, Cartao> cartoes,
        DateOnly limite) =>
        Ate(DataEfetiva(lancamento, cartoes), lancamento.Vezes, limite, lancamento.Periodicidade,
            DiaDeReferencia(lancamento, cartoes));

    /// <summary>A ultima ocorrencia de um lancamento, com data efetiva e dia certos. null e sem fim.</summary>
    public static DateOnly? UltimaVezDo(Lancamento lancamento, IReadOnlyDictionary<int, Cartao> cartoes) =>
        UltimaVez(DataEfetiva(lancamento, cartoes), lancamento.Vezes, lancamento.Periodicidade,
            DiaDeReferencia(lancamento, cartoes));

    /// <summary>
    /// Ocorrencias a partir da data efetiva, de mes em mes ou de semana em semana, parando no
    /// limite informado. Cada ocorrencia e derivada da data original, nunca da anterior: somando
    /// na anterior, um mes que cai no dia 30 puxaria todos os seguintes para o dia 30.
    /// </summary>
    /// <param name="diaDoMes">
    /// Dia em que as mensais caem. Sem ele, o dia da data efetiva. Ver <see cref="DiaDeReferencia"/>.
    /// </param>
    public static IEnumerable<DateOnly> Ate(
        DateOnly dataEfetiva,
        int? vezes,
        DateOnly limite,
        Periodicidade periodicidade = Periodicidade.Mensal,
        int? diaDoMes = null)
    {
        var dia = diaDoMes ?? dataEfetiva.Day;
        var total = vezes is null ? (int?)null : Math.Max(1, vezes.Value);

        for (var k = 0; total is null || k < total.Value; k++)
        {
            var ocorrencia = periodicidade == Periodicidade.Semanal
                ? dataEfetiva.AddDays(7 * k)
                : Calendario.AdicionarMeses(dataEfetiva, k, dia);

            if (ocorrencia > limite)
            {
                yield break;
            }

            yield return ocorrencia;
        }
    }

    /// <summary>
    /// Ultima ocorrencia de um lancamento, ignorando o horizonte. null significa sem fim.
    /// Alimenta a coluna "Ultima vez" do Plano de Contas.
    /// </summary>
    public static DateOnly? UltimaVez(
        DateOnly dataEfetiva,
        int? vezes,
        Periodicidade periodicidade = Periodicidade.Mensal,
        int? diaDoMes = null)
    {
        if (vezes is null)
        {
            return null;
        }

        var ultima = Math.Max(1, vezes.Value) - 1;

        return periodicidade == Periodicidade.Semanal
            ? dataEfetiva.AddDays(7 * ultima)
            : Calendario.AdicionarMeses(dataEfetiva, ultima, diaDoMes ?? dataEfetiva.Day);
    }
}
