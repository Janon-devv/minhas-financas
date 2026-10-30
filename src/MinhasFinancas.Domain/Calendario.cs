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
        // Dia abaixo de 1 cai no dia 1, como em NoMes: um cartao com dia zero vindo de um arquivo
        // antigo nao pode derrubar a projecao inteira.
        var dia = Math.Max(1, diaDesejado ?? referencia.Day);
        var destino = PrimeiroDiaDoMes(referencia).AddMonths(meses);
        var ultimo = UltimoDiaDoMes(destino.Year, destino.Month);
        return new DateOnly(destino.Year, destino.Month, Math.Min(dia, ultimo));
    }

    /// <summary>Quantidade de meses de distancia entre dois meses de calendario (ignora o dia).</summary>
    public static int DistanciaEmMeses(DateOnly de, DateOnly ate) =>
        (ate.Year - de.Year) * 12 + (ate.Month - de.Month);

    public static bool MesmoMes(DateOnly a, DateOnly b) => a.Year == b.Year && a.Month == b.Month;

    /// <summary>
    /// Um dia dentro de um mes, caindo no ultimo dia quando ele nao existe: dia 31 em novembro
    /// vira 30. Serve para as datas de fechamento e vencimento de cartao, que sao dias do mes.
    /// </summary>
    public static DateOnly NoMes(int ano, int mes, int dia) =>
        new(ano, mes, Math.Min(Math.Max(1, dia), UltimoDiaDoMes(ano, mes)));

    public static DateOnly Maior(DateOnly a, DateOnly b) => a >= b ? a : b;

    public static DateOnly Menor(DateOnly a, DateOnly b) => a <= b ? a : b;
}
