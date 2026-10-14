using MinhasFinancas.Domain.Modelo;

namespace MinhasFinancas.Domain;

/// <summary>R17: ganho variavel registrado mes a mes.</summary>
public static class GanhosVariaveis
{
    /// <summary>
    /// O que foi registrado para um mes. Zero quando nao ha registro: mes em branco nao rende
    /// nada, e o app nao chuta media nenhuma. O que nao e certo fica fora, como manda a secao 4.
    /// </summary>
    public static long DoMes(IEnumerable<GanhoVariavel> registros, int ano, int mes)
    {
        long total = 0;

        foreach (var registro in registros)
        {
            if (registro.Ano == ano && registro.Mes == mes)
            {
                total += registro.ValorCentavos;
            }
        }

        return total;
    }

    /// <summary>
    /// Media dos meses PREENCHIDOS, so para mostrar na tela como referencia. Mes em branco nao
    /// entra na conta da media: ele significa "ainda nao respondi", nao "ganhei zero".
    /// null quando nao ha nenhum mes preenchido.
    /// </summary>
    public static long? Media(IEnumerable<GanhoVariavel> registros)
    {
        long soma = 0;
        var quantos = 0;

        foreach (var registro in registros)
        {
            soma += registro.ValorCentavos;
            quantos++;
        }

        return quantos == 0 ? null : (long)Math.Round((decimal)soma / quantos, MidpointRounding.AwayFromZero);
    }

    /// <summary>Os registros do mais recente para o mais antigo, que e a ordem de leitura na tela.</summary>
    public static List<GanhoVariavel> MaisRecentesPrimeiro(IEnumerable<GanhoVariavel> registros) =>
        registros.OrderByDescending(r => r.Ano).ThenByDescending(r => r.Mes).ToList();
}
