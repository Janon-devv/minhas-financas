namespace MinhasFinancas.Domain;

/// <summary>
/// Ate onde a projecao vai. No app o horizonte e sempre relativo a hoje, diferente da planilha,
/// que dependia de estender linhas na mao.
/// </summary>
public static class Horizonte
{
    public static DateOnly Calcular(DateOnly hoje, DateOnly ancora, int meses)
    {
        var baseData = Calendario.Maior(hoje, ancora);
        var fim = Calendario.AdicionarMeses(baseData, Math.Max(1, meses));
        return fim < ancora ? ancora : fim;
    }
}

/// <summary>Relogio injetavel: o motor nunca le a data do sistema por conta propria.</summary>
public interface IRelogio
{
    DateOnly Hoje { get; }

    DateTimeOffset Agora { get; }
}

/// <summary>Relogio real, fixado no fuso de Sao Paulo.</summary>
public sealed class RelogioSaoPaulo : IRelogio
{
    private static readonly TimeZoneInfo Fuso = ResolverFuso();

    public DateTimeOffset Agora => TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, Fuso);

    public DateOnly Hoje => DateOnly.FromDateTime(Agora.DateTime);

    private static TimeZoneInfo ResolverFuso()
    {
        foreach (var id in new[] { "America/Sao_Paulo", "E. South America Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Local;
    }
}

/// <summary>Relogio fixo, para testes e para conferir cenarios em outra data.</summary>
public sealed class RelogioFixo(DateOnly hoje) : IRelogio
{
    public DateOnly Hoje { get; } = hoje;

    public DateTimeOffset Agora { get; } = new(hoje.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(-3));
}
