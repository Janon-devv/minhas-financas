using Microsoft.EntityFrameworkCore;
using MinhasFinancas.Domain.Modelo;

namespace MinhasFinancas.Infra;

public class FinancasDbContext(DbContextOptions<FinancasDbContext> options) : DbContext(options)
{
    public DbSet<Configuracao> Configuracoes => Set<Configuracao>();

    public DbSet<AncoraHistorico> Ancoras => Set<AncoraHistorico>();

    public DbSet<Cartao> Cartoes => Set<Cartao>();

    public DbSet<Tag> Tags => Set<Tag>();

    public DbSet<Meta> Metas => Set<Meta>();

    public DbSet<Lancamento> Lancamentos => Set<Lancamento>();

    public DbSet<DiarioReal> DiariosReais => Set<DiarioReal>();

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        modelo.Entity<Configuracao>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).ValueGeneratedNever();
        });

        modelo.Entity<DiarioReal>(e => e.HasKey(d => d.Data));

        modelo.Entity<Cartao>(e =>
        {
            e.HasIndex(c => c.Nome).IsUnique();
            e.Property(c => c.Nome).IsRequired();
        });

        modelo.Entity<Tag>(e =>
        {
            e.HasIndex(t => t.Nome).IsUnique();
            e.Property(t => t.Nome).IsRequired();
        });

        modelo.Entity<Meta>(e =>
        {
            e.HasIndex(m => m.Nome).IsUnique();
            e.Property(m => m.Nome).IsRequired();
        });

        modelo.Entity<Lancamento>(e =>
        {
            e.HasIndex(l => l.Data);
            e.Property(l => l.Descricao).IsRequired();

            // Enums gravados como texto para o banco continuar legivel num SELECT manual.
            e.Property(l => l.Tipo).HasConversion<string>().HasMaxLength(20);
            e.Property(l => l.Certeza).HasConversion<string>().HasMaxLength(20);

            // Sem propriedade de navegacao: so a integridade, para nao apagar um cartao em uso.
            e.HasOne<Cartao>().WithMany().HasForeignKey(l => l.CartaoId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Tag>().WithMany().HasForeignKey(l => l.TagId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Meta>().WithMany().HasForeignKey(l => l.MetaId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}

public static class CaminhoBanco
{
    /// <summary>
    /// Por padrao o banco fica na pasta de dados do usuario, fora do repositorio: assim um
    /// git clean ou uma recompilacao nunca leva o historico financeiro junto.
    /// </summary>
    public static string Padrao()
    {
        var pasta = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MinhasFinancas");

        Directory.CreateDirectory(pasta);
        return Path.Combine(pasta, "financas.db");
    }
}
