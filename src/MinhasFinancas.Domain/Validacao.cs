using MinhasFinancas.Domain.Modelo;

namespace MinhasFinancas.Domain;

/// <summary>
/// Regras de "pode salvar?" (CONTEXTO.md secao 11). Ficam no dominio para a tela e qualquer
/// importacao futura usarem o mesmo criterio.
/// </summary>
public static class Validacao
{
    public static List<string> Lancamento(Lancamento lancamento, IReadOnlyList<Cartao> cartoes)
    {
        var erros = new List<string>();

        if (string.IsNullOrWhiteSpace(lancamento.Descricao))
        {
            erros.Add("Escreva uma descricao.");
        }

        if (lancamento.ValorCentavos <= 0)
        {
            erros.Add("O valor precisa ser maior que zero.");
        }

        if (lancamento.Data == default)
        {
            erros.Add("Informe a data.");
        }

        if (lancamento.Vezes is int vezes && vezes < 1)
        {
            erros.Add("A quantidade de vezes precisa ser pelo menos 1, ou deixe em branco para sem fim.");
        }

        if (lancamento.Tipo == TipoLancamento.Cartao)
        {
            if (lancamento.CartaoId is null)
            {
                erros.Add("Lancamento de cartao precisa dizer qual cartao.");
            }
            else if (cartoes.All(c => c.Id != lancamento.CartaoId))
            {
                erros.Add("O cartao escolhido nao existe mais.");
            }
        }

        if (lancamento.Tipo != TipoLancamento.Entrada && lancamento.Certeza is not null)
        {
            erros.Add("Certeza so vale para Entrada.");
        }

        if (lancamento.Tipo != TipoLancamento.Economia && lancamento.MetaId is not null)
        {
            erros.Add("Vinculo com meta so vale para Economia.");
        }

        return erros;
    }

    public static List<string> Configuracao(Configuracao cfg)
    {
        var erros = new List<string>();

        if (cfg.DataAncora == default)
        {
            erros.Add("Informe a data do saldo.");
        }

        if (cfg.DiarioPrevistoCentavos < 0)
        {
            erros.Add("O diario previsto nao pode ser negativo.");
        }

        if (cfg.SaldoConfortoCentavos < 0)
        {
            erros.Add("O saldo de conforto nao pode ser negativo.");
        }

        if (cfg.HorizonteMeses is < 1 or > 120)
        {
            erros.Add("O horizonte precisa ficar entre 1 e 120 meses.");
        }

        return erros;
    }

    public static List<string> Cartao(Cartao cartao)
    {
        var erros = new List<string>();

        if (string.IsNullOrWhiteSpace(cartao.Nome))
        {
            erros.Add("De um nome ao cartao.");
        }

        if (cartao.DiaFechamento is < 1 or > 31)
        {
            erros.Add("O dia de fechamento precisa ficar entre 1 e 31.");
        }

        if (cartao.DiaVencimento is < 1 or > 31)
        {
            erros.Add("O dia de vencimento precisa ficar entre 1 e 31.");
        }

        return erros;
    }

    public static List<string> Meta(Meta meta)
    {
        var erros = new List<string>();

        if (string.IsNullOrWhiteSpace(meta.Nome))
        {
            erros.Add("De um nome a meta.");
        }

        if (meta.ObjetivoCentavos <= 0)
        {
            erros.Add("O objetivo precisa ser maior que zero.");
        }

        if (meta.SaldoInicialCentavos < 0)
        {
            erros.Add("O saldo inicial da meta nao pode ser negativo.");
        }

        return erros;
    }

    public static List<string> Simulacao(Simulacao simulacao, DateOnly hoje)
    {
        var erros = new List<string>();

        if (simulacao.ValorTotalCentavos <= 0)
        {
            erros.Add("O preco precisa ser maior que zero.");
        }

        if (simulacao.EntradaCentavos < 0 || simulacao.EntradaCentavos > simulacao.ValorTotalCentavos)
        {
            erros.Add("A entrada precisa ficar entre zero e o preco.");
        }

        if (simulacao.Parcelas is < 1 or > Simulador.MaximoParcelas)
        {
            erros.Add($"A quantidade de parcelas precisa ficar entre 1 e {Simulador.MaximoParcelas}.");
        }

        if (simulacao.TaxaMensal < 0)
        {
            erros.Add("A taxa de juros nao pode ser negativa.");
        }

        if (simulacao.EntradaCentavos > 0 && simulacao.DataEntrada is null)
        {
            erros.Add("Informe a data da entrada.");
        }

        if (simulacao.DataEntrada is DateOnly dataEntrada && dataEntrada < hoje)
        {
            erros.Add("A data da entrada nao pode ser no passado.");
        }

        if (simulacao.ValorFinanciadoCentavos > 0)
        {
            if (simulacao.PrimeiraParcela == default)
            {
                erros.Add("Informe a data da primeira parcela.");
            }
            else if (simulacao.PrimeiraParcela < hoje)
            {
                erros.Add("A primeira parcela nao pode ser no passado.");
            }
            else if (simulacao.DataEntrada is DateOnly entrada && simulacao.PrimeiraParcela < entrada)
            {
                erros.Add("A primeira parcela nao pode vir antes da entrada.");
            }
        }

        return erros;
    }
}
