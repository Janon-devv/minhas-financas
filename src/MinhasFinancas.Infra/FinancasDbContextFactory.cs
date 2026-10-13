using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace MinhasFinancas.Infra;

/// <summary>
/// Usado so pelas ferramentas de migracao (dotnet ef), para elas nao dependerem
/// da inicializacao do projeto web.
/// </summary>
public sealed class FinancasDbContextFactory : IDesignTimeDbContextFactory<FinancasDbContext>
{
    public FinancasDbContext CreateDbContext(string[] args)
    {
        var opcoes = new DbContextOptionsBuilder<FinancasDbContext>()
            .UseSqlite($"Data Source={CaminhoBanco.Padrao()}")
            .Options;

        return new FinancasDbContext(opcoes);
    }
}
