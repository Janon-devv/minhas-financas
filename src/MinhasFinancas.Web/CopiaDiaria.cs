using MinhasFinancas.Infra.Servicos;

namespace MinhasFinancas.Web;

/// <summary>
/// Faz a copia diaria do banco sem ninguem precisar lembrar: na subida e depois de hora em hora,
/// que pega a virada do dia mesmo com o PC ligado direto. Falha na copia vai para o log e nao
/// derruba o app: ficar sem a copia de hoje e ruim, ficar sem o app e pior.
/// </summary>
public sealed class CopiaDiaria(CopiaDeSeguranca copia, ILogger<CopiaDiaria> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancelamento)
    {
        using var intervalo = new PeriodicTimer(TimeSpan.FromHours(1));

        do
        {
            try
            {
                if (await copia.CopiaDoDiaAsync(cancelamento) is string feita)
                {
                    log.LogInformation("Copia diaria do banco salva em {Caminho}", feita);
                }
            }
            catch (Exception erro) when (erro is not OperationCanceledException)
            {
                log.LogError(erro, "Nao consegui fazer a copia diaria do banco em {Pasta}", copia.Pasta);
            }
        }
        while (await intervalo.WaitForNextTickAsync(cancelamento));
    }
}
