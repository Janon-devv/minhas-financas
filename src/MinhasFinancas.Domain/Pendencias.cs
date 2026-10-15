using MinhasFinancas.Domain.Modelo;

namespace MinhasFinancas.Domain;

public enum EstadoOcorrencia
{
    /// <summary>Ainda vai vencer.</summary>
    Prevista,

    /// <summary>Vence hoje.</summary>
    VenceHoje,

    /// <summary>Passou da data e ninguem disse que saiu.</summary>
    EmAberto,

    /// <summary>Confirmada: saiu (ou entrou) de verdade.</summary>
    Paga,

    /// <summary>Nao aconteceu neste periodo e nao vai acontecer.</summary>
    Pulada
}

/// <summary>
/// Uma ocorrencia do periodo olhado, com o que se sabe sobre ela.
/// </summary>
/// <param name="Registrada">
/// Alguem ja respondeu sobre esta ocorrencia. Sem registro, "em aberto" significa so que ninguem
/// disse nada ainda, e e diferente de o dono ter dito que nao pagou.
/// </param>
/// <param name="ForaDaProjecao">
/// A ocorrencia venceu antes da ancora e ninguem confirmou. O saldo de partida foi digitado
/// olhando o extrato, entao o dinheiro dela ainda esta na conta, mas o app nao a projeta: ela
/// some da conta de chegar. E este o caso que o painel precisa gritar.
/// </param>
public sealed record Pendencia(
    int LancamentoId,
    string Descricao,
    TipoLancamento Tipo,
    DateOnly DataPrevista,
    long ValorCentavos,
    EstadoOcorrencia Estado,
    DateOnly? PagoEm,
    int? TagId,
    bool Registrada,
    bool ForaDaProjecao);

/// <summary>
/// R12: as ocorrencias de um periodo com o estado de cada uma. Funcao pura, como o resto do motor.
/// </summary>
public static class Pendencias
{
    /// <param name="ate">
    /// Vai alem de hoje de proposito. Conta recorrente e marcada paga mes a mes, e no comeco do
    /// mes a do mes ainda nao venceu: sem olhar para a frente, ela so apareceria no dia do
    /// vencimento, tarde demais para servir de lista do que falta pagar.
    /// </param>
    public static List<Pendencia> Listar(
        IEnumerable<Lancamento> lancamentos,
        IReadOnlyDictionary<int, Cartao> cartoes,
        IEnumerable<SituacaoOcorrencia> situacoes,
        DateOnly desde,
        DateOnly ate,
        DateOnly hoje,
        DateOnly ancora)
    {
        var porChave = situacoes.ToDictionary(s => (s.LancamentoId, s.Data));
        var lista = new List<Pendencia>();

        foreach (var lancamento in lancamentos)
        {
            if (lancamento.ValorCentavos <= 0)
            {
                continue;
            }

            var dataEfetiva = Ocorrencias.DataEfetiva(lancamento, cartoes);

            foreach (var data in Ocorrencias.Ate(dataEfetiva, lancamento.Vezes, ate, lancamento.Periodicidade))
            {
                if (data < desde)
                {
                    continue;
                }

                porChave.TryGetValue((lancamento.Id, data), out var situacao);

                var estado = situacao?.Pulada == true
                    ? EstadoOcorrencia.Pulada
                    : situacao?.Pago == true
                    ? EstadoOcorrencia.Paga
                    : data < hoje
                        ? EstadoOcorrencia.EmAberto
                        : data == hoje
                            ? EstadoOcorrencia.VenceHoje
                            : EstadoOcorrencia.Prevista;

                lista.Add(new Pendencia(
                    lancamento.Id,
                    lancamento.Descricao,
                    lancamento.Tipo,
                    data,
                    situacao?.ValorCentavos ?? lancamento.ValorCentavos,
                    estado,
                    situacao?.PagoEm,
                    lancamento.TagId,
                    Registrada: situacao is not null,
                    ForaDaProjecao: estado is not (EstadoOcorrencia.Paga or EstadoOcorrencia.Pulada) &&
                                    data < ancora));
            }
        }

        return [.. lista.OrderBy(p => p.DataPrevista).ThenBy(p => p.Descricao)];
    }
}
