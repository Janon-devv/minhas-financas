namespace MinhasFinancas.Domain.Resultado;

public sealed record ResultadoProjecao
{
    /// <summary>Um item por dia, da ancora ate o fim do horizonte, em ordem.</summary>
    public IReadOnlyList<DiaProjetado> Dias { get; init; } = [];

    public required ResumoAgora Agora { get; init; }

    /// <summary>Todos os meses tocados pela projecao. A tela de horizonte usa os 12 primeiros.</summary>
    public IReadOnlyList<ResumoMes> Meses { get; init; } = [];

    public ResultadoSimulacao? Simulacao { get; init; }

    /// <summary>Problemas de cadastro encontrados durante a projecao, para a UI mostrar sem travar nada.</summary>
    public IReadOnlyList<string> Avisos { get; init; } = [];

    public required DateOnly Inicio { get; init; }

    public required DateOnly Fim { get; init; }

    private Dictionary<DateOnly, DiaProjetado>? _indice;

    public DiaProjetado? Dia(DateOnly data)
    {
        _indice ??= Dias.ToDictionary(d => d.Data);
        return _indice.TryGetValue(data, out var dia) ? dia : null;
    }

    public ResumoMes? Mes(int ano, int mes) => Meses.FirstOrDefault(m => m.Ano == ano && m.Mes == mes);
}
