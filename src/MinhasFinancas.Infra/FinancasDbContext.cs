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

    public DbSet<SituacaoOcorrencia> Situacoes => Set<SituacaoOcorrencia>();

    public DbSet<Carteira> Carteiras => Set<Carteira>();

    public DbSet<FaturaCartao> Faturas => Set<FaturaCartao>();

    public DbSet<GanhoVariavel> GanhosVariaveis => Set<GanhoVariavel>();

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        modelo.Entity<Configuracao>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).ValueGeneratedNever();

            // O padrao do banco precisa dizer o mesmo que o padrao da classe. Sem isto a
            // migracao nasce com defaultValue: false e desliga o diario de quem ja usava o
            // app, calado, no primeiro start depois da atualizacao.
            e.Property(c => c.DiarioPrevistoAtivo).HasDefaultValue(true);
        });

        // Chave propria, nao a data: com carteiras, o mesmo dia tem um diario por carteira.
        // Indice unico em vez de chave composta porque o SQLite nao aceita chave com coluna
        // anulavel, e CarteiraId e anulavel para a conta corrente continuar sendo null.
        modelo.Entity<DiarioReal>(e =>
        {
            e.HasKey(d => d.Id);
            e.HasIndex(d => new { d.CarteiraId, d.Data }).IsUnique();
        });

        // Um registro de ganho variavel por mes: a tela preenche um campo so, e dois registros
        // do mesmo mes dobrariam o ganho sem ninguem ver.
        modelo.Entity<GanhoVariavel>(e =>
            e.HasIndex(g => new { g.Ano, g.Mes }).IsUnique());

        // Uma fatura por cartao por vencimento. O indice unico evita duas faturas do mesmo
        // ciclo, que fariam a projecao descontar a mesma divida duas vezes.
        modelo.Entity<FaturaCartao>(e =>
            e.HasIndex(f => new { f.CartaoId, f.Vencimento }).IsUnique());

        modelo.Entity<Carteira>(e =>
        {
            e.HasIndex(c => c.Nome).IsUnique();
            e.Property(c => c.Nome).IsRequired();
        });

        // Uma ocorrencia so pode ter uma situacao. O indice unico evita que dois toques no
        // botao de pagar gravem dois registros e o pagamento conte duas vezes.
        modelo.Entity<SituacaoOcorrencia>(e =>
            e.HasIndex(x => new { x.LancamentoId, x.Data }).IsUnique());

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
