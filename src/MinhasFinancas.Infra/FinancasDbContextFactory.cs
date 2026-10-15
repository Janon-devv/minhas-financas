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
        // Banco de rascunho de proposito. "migrations add" so le o modelo, mas
        // "database update" e "database drop" conectam de verdade: apontar para o banco
        // real deixaria um comando digitado errado alcancar o historico financeiro.
        // Para mirar outro banco, MINHASFINANCAS_BANCO.
        var caminho = Environment.GetEnvironmentVariable("MINHASFINANCAS_BANCO")
                      ?? Path.Combine(Path.GetTempPath(), "minhasfinancas-design.db");

        var opcoes = new DbContextOptionsBuilder<FinancasDbContext>()
            .UseSqlite($"Data Source={caminho}")
            .Options;

        return new FinancasDbContext(opcoes);
    }
}
