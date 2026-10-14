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
    /// Ocorrencias a partir da data efetiva, de mes em mes ou de semana em semana, parando no
    /// limite informado. Cada ocorrencia e derivada da data original, nunca da anterior: somando
    /// na anterior, um mes que cai no dia 30 puxaria todos os seguintes para o dia 30.
    /// </summary>
    public static IEnumerable<DateOnly> Ate(
        DateOnly dataEfetiva,
        int? vezes,
        DateOnly limite,
        Periodicidade periodicidade = Periodicidade.Mensal)
    {
        var dia = dataEfetiva.Day;
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
        Periodicidade periodicidade = Periodicidade.Mensal)
    {
        if (vezes is null)
        {
            return null;
        }

        var ultima = Math.Max(1, vezes.Value) - 1;

        return periodicidade == Periodicidade.Semanal
            ? dataEfetiva.AddDays(7 * ultima)
            : Calendario.AdicionarMeses(dataEfetiva, ultima, dataEfetiva.Day);
    }
}
