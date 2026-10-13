namespace MinhasFinancas.Domain;

/// <summary>
/// Aritmetica de datas do projeto. Toda ocorrencia mensal e derivada da data original,
/// nunca da ocorrencia anterior: isso evita o erro de 31/01 virar 28/02 e depois travar em 28/03 (R2).
/// </summary>
public static class Calendario
{
    public static int UltimoDiaDoMes(int ano, int mes) => DateTime.DaysInMonth(ano, mes);

    public static DateOnly PrimeiroDiaDoMes(DateOnly data) => new(data.Year, data.Month, 1);

    public static DateOnly UltimoDiaDoMes(DateOnly data) =>
        new(data.Year, data.Month, UltimoDiaDoMes(data.Year, data.Month));

    /// <summary>
    /// Soma meses a uma data preservando o dia desejado. Quando o dia nao existe no mes de destino,
    /// cai no ultimo dia daquele mes (dia 31 em novembro vira 30/11; em fevereiro vira 28 ou 29).
    /// </summary>
    public static DateOnly AdicionarMeses(DateOnly referencia, int meses, int? diaDesejado = null)
    {
        var dia = diaDesejado ?? referencia.Day;
        var destino = PrimeiroDiaDoMes(referencia).AddMonths(meses);
        var ultimo = UltimoDiaDoMes(destino.Year, destino.Month);
        return new DateOnly(destino.Year, destino.Month, Math.Min(dia, ultimo));
    }

    /// <summary>Quantidade de meses de distancia entre dois meses de calendario (ignora o dia).</summary>
    public static int DistanciaEmMeses(DateOnly de, DateOnly ate) =>
        (ate.Year - de.Year) * 12 + (ate.Month - de.Month);

    public static bool MesmoMes(DateOnly a, DateOnly b) => a.Year == b.Year && a.Month == b.Month;

    public static DateOnly Maior(DateOnly a, DateOnly b) => a >= b ? a : b;

    public static DateOnly Menor(DateOnly a, DateOnly b) => a <= b ? a : b;
}
