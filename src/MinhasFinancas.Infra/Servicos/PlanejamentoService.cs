using Microsoft.EntityFrameworkCore;
using MinhasFinancas.Domain;
using MinhasFinancas.Domain.Modelo;
using MinhasFinancas.Domain.Resultado;

namespace MinhasFinancas.Infra.Servicos;

/// <summary>Tudo que esta guardado, pronto para virar entrada do motor.</summary>
public sealed record DadosPlanejamento
{
    public required Configuracao Configuracao { get; init; }

    public List<Cartao> Cartoes { get; init; } = [];

    public List<Tag> Tags { get; init; } = [];

    public List<Meta> Metas { get; init; } = [];

    public List<Lancamento> Lancamentos { get; init; } = [];

    public List<DiarioReal> Diarios { get; init; } = [];
}

/// <summary>
/// Camada de aplicacao: le e grava, e entrega a projecao pronta. O calculo em si fica todo
/// no dominio, que nao conhece banco nem tela.
/// </summary>
public sealed class PlanejamentoService(IDbContextFactory<FinancasDbContext> fabrica, IRelogio relogio)
{
    public DateOnly Hoje => relogio.Hoje;

    public async Task<Configuracao> ObterConfiguracaoAsync()
    {
        await using var db = await fabrica.CreateDbContextAsync();
        return await ObterOuCriarAsync(db);
    }

    private async Task<Configuracao> ObterOuCriarAsync(FinancasDbContext db)
    {
        var cfg = await db.Configuracoes.FirstOrDefaultAsync(c => c.Id == 1);

        if (cfg is not null)
        {
            return cfg;
        }

        cfg = new Configuracao
        {
            Id = 1,
            DataAncora = relogio.Hoje,
            SaldoAncoraCentavos = 0,
            DiarioPrevistoCentavos = 0,
            SaldoConfortoCentavos = 0,
            HorizonteMeses = 24,
            ConfiguracaoConcluida = false
        };

        db.Configuracoes.Add(cfg);
        await db.SaveChangesAsync();
        return cfg;
    }

    public async Task<DadosPlanejamento> CarregarAsync()
    {
        await using var db = await fabrica.CreateDbContextAsync();
        var cfg = await ObterOuCriarAsync(db);

        return new DadosPlanejamento
        {
            Configuracao = cfg,
            Cartoes = await db.Cartoes.AsNoTracking().OrderBy(c => c.Nome).ToListAsync(),
            Tags = await db.Tags.AsNoTracking().OrderBy(t => t.Nome).ToListAsync(),
            Metas = await db.Metas.AsNoTracking().OrderBy(m => m.Nome).ToListAsync(),
            Lancamentos = await db.Lancamentos.AsNoTracking().OrderBy(l => l.Data).ToListAsync(),
            Diarios = await db.DiariosReais.AsNoTracking().ToListAsync()
        };
    }

    public async Task<ResultadoProjecao> ProjetarAsync(Simulacao? simulacao = null, DateOnly? hoje = null)
    {
        var dados = await CarregarAsync();
        return Projetar(dados, simulacao, hoje ?? relogio.Hoje);
    }

    public ResultadoProjecao Projetar(DadosPlanejamento dados, Simulacao? simulacao, DateOnly hoje) =>
        Projetor.Projetar(new EntradaProjecao
        {
            Configuracao = dados.Configuracao,
            Cartoes = dados.Cartoes,
            Tags = dados.Tags,
            Lancamentos = dados.Lancamentos,
            DiariosReais = dados.Diarios.ToDictionary(d => d.Data, d => d.ValorCentavos),
            Hoje = hoje,
            Simulacao = simulacao
        });

    public async Task<List<ResumoMeta>> ObterMetasAsync()
    {
        var dados = await CarregarAsync();
        var fim = Horizonte.Calcular(relogio.Hoje, dados.Configuracao.DataAncora, dados.Configuracao.HorizonteMeses);

        return CalculadoraMetas.Calcular(
            dados.Metas.Where(m => m.ArquivadaEm is null),
            dados.Lancamentos,
            dados.Cartoes,
            dados.Configuracao.DataAncora,
            relogio.Hoje,
            fim);
    }

    // ---------- Configuracao e ancora ----------

    public async Task SalvarConfiguracaoAsync(Configuracao novo, string? observacao = null)
    {
        await using var db = await fabrica.CreateDbContextAsync();
        var cfg = await ObterOuCriarAsync(db);

        var ancoraMudou = cfg.DataAncora != novo.DataAncora ||
                          cfg.SaldoAncoraCentavos != novo.SaldoAncoraCentavos;

        cfg.SaldoAncoraCentavos = novo.SaldoAncoraCentavos;
        cfg.DataAncora = novo.DataAncora;
        cfg.DiarioPrevistoCentavos = novo.DiarioPrevistoCentavos;
        cfg.SaldoConfortoCentavos = novo.SaldoConfortoCentavos;
        cfg.ContarEntradasIncertas = novo.ContarEntradasIncertas;
        cfg.JaGuardadoCentavos = novo.JaGuardadoCentavos;
        cfg.HorizonteMeses = novo.HorizonteMeses;
        cfg.ConfiguracaoConcluida = novo.ConfiguracaoConcluida;

        if (ancoraMudou)
        {
            db.Ancoras.Add(new AncoraHistorico
            {
                Data = novo.DataAncora,
                SaldoCentavos = novo.SaldoAncoraCentavos,
                CriadoEm = relogio.Agora,
                Observacao = observacao
            });
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Conferencia com o banco: troca a ancora por um saldo novo numa data nova.
    /// Nao apaga nada; o que ficou antes da nova ancora so deixa de contar.
    /// </summary>
    public async Task RecalibrarAsync(long saldoCentavos, DateOnly data, string? observacao = null)
    {
        await using var db = await fabrica.CreateDbContextAsync();
        var cfg = await ObterOuCriarAsync(db);

        cfg.SaldoAncoraCentavos = saldoCentavos;
        cfg.DataAncora = data;

        db.Ancoras.Add(new AncoraHistorico
        {
            Data = data,
            SaldoCentavos = saldoCentavos,
            CriadoEm = relogio.Agora,
            Observacao = observacao ?? "Conferencia com o banco"
        });

        await db.SaveChangesAsync();
    }

    public async Task<List<AncoraHistorico>> ObterHistoricoAncorasAsync()
    {
        await using var db = await fabrica.CreateDbContextAsync();
        // Ordena pelo Id porque o SQLite nao aceita ORDER BY em DateTimeOffset.
        // Como o Id e sequencial, da na mesma que ordenar pela data de criacao.
        return await db.Ancoras.AsNoTracking().OrderByDescending(a => a.Id).Take(50).ToListAsync();
    }

    // ---------- Lancamentos ----------

    public async Task<int> SalvarLancamentoAsync(Lancamento lancamento)
    {
        await using var db = await fabrica.CreateDbContextAsync();

        if (lancamento.Id == 0)
        {
            db.Lancamentos.Add(lancamento);
            await db.SaveChangesAsync();
            return lancamento.Id;
        }

        var atual = await db.Lancamentos.FirstAsync(l => l.Id == lancamento.Id);
        atual.Data = lancamento.Data;
        atual.Descricao = lancamento.Descricao;
        atual.Tipo = lancamento.Tipo;
        atual.ValorCentavos = lancamento.ValorCentavos;
        atual.Vezes = lancamento.Vezes;
        atual.CartaoId = lancamento.CartaoId;
        atual.Certeza = lancamento.Certeza;
        atual.TagId = lancamento.TagId;
        atual.MetaId = lancamento.MetaId;
        atual.Obs = lancamento.Obs;

        await db.SaveChangesAsync();
        return atual.Id;
    }

    public async Task<Lancamento?> ObterLancamentoAsync(int id)
    {
        await using var db = await fabrica.CreateDbContextAsync();
        return await db.Lancamentos.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id);
    }

    public async Task ExcluirLancamentoAsync(int id)
    {
        await using var db = await fabrica.CreateDbContextAsync();
        await db.Lancamentos.Where(l => l.Id == id).ExecuteDeleteAsync();
    }

    // ---------- Diario ----------

    public async Task RegistrarDiarioAsync(DateOnly data, long valorCentavos)
    {
        await using var db = await fabrica.CreateDbContextAsync();
        var atual = await db.DiariosReais.FirstOrDefaultAsync(d => d.Data == data);

        if (atual is null)
        {
            db.DiariosReais.Add(new DiarioReal { Data = data, ValorCentavos = valorCentavos });
        }
        else
        {
            atual.ValorCentavos = valorCentavos;
        }

        await db.SaveChangesAsync();
    }

    /// <summary>Apaga o registro do dia, para a projecao voltar a usar o diario previsto.</summary>
    public async Task VoltarParaPrevisaoAsync(DateOnly data)
    {
        await using var db = await fabrica.CreateDbContextAsync();
        await db.DiariosReais.Where(d => d.Data == data).ExecuteDeleteAsync();
    }

    // ---------- Cartoes, tags e metas ----------

    public async Task SalvarCartaoAsync(Cartao cartao)
    {
        await using var db = await fabrica.CreateDbContextAsync();

        if (cartao.Id == 0)
        {
            db.Cartoes.Add(cartao);
        }
        else
        {
            var atual = await db.Cartoes.FirstAsync(c => c.Id == cartao.Id);
            atual.Nome = cartao.Nome;
            atual.DiaFechamento = cartao.DiaFechamento;
            atual.DiaVencimento = cartao.DiaVencimento;
        }

        await db.SaveChangesAsync();
    }

    public async Task<int> ContarLancamentosDoCartaoAsync(int cartaoId)
    {
        await using var db = await fabrica.CreateDbContextAsync();
        return await db.Lancamentos.CountAsync(l => l.CartaoId == cartaoId);
    }

    /// <summary>Recusa apagar cartao em uso: um lancamento de Cartao sem cartao perde a data de vencimento.</summary>
    public async Task<string?> ExcluirCartaoAsync(int id)
    {
        var emUso = await ContarLancamentosDoCartaoAsync(id);

        if (emUso > 0)
        {
            return $"Esse cartao esta em {emUso} lancamento(s). Troque o cartao deles antes de excluir.";
        }

        await using var db = await fabrica.CreateDbContextAsync();
        await db.Cartoes.Where(c => c.Id == id).ExecuteDeleteAsync();
        return null;
    }

    public async Task SalvarTagAsync(Tag tag)
    {
        await using var db = await fabrica.CreateDbContextAsync();

        if (tag.Id == 0)
        {
            db.Tags.Add(tag);
        }
        else
        {
            var atual = await db.Tags.FirstAsync(t => t.Id == tag.Id);
            atual.Nome = tag.Nome;
        }

        await db.SaveChangesAsync();
    }

    /// <summary>Solta a tag dos lancamentos e depois apaga, para nao travar uma limpeza simples.</summary>
    public async Task ExcluirTagAsync(int id)
    {
        await using var db = await fabrica.CreateDbContextAsync();
        await db.Lancamentos.Where(l => l.TagId == id).ExecuteUpdateAsync(s => s.SetProperty(l => l.TagId, (int?)null));
        await db.Tags.Where(t => t.Id == id).ExecuteDeleteAsync();
    }

    public async Task SalvarMetaAsync(Meta meta)
    {
        await using var db = await fabrica.CreateDbContextAsync();

        if (meta.Id == 0)
        {
            db.Metas.Add(meta);
        }
        else
        {
            var atual = await db.Metas.FirstAsync(m => m.Id == meta.Id);
            atual.Nome = meta.Nome;
            atual.ObjetivoCentavos = meta.ObjetivoCentavos;
            atual.Prazo = meta.Prazo;
            atual.SaldoInicialCentavos = meta.SaldoInicialCentavos;
            atual.ArquivadaEm = meta.ArquivadaEm;
        }

        await db.SaveChangesAsync();
    }

    /// <summary>Arquivar preserva o historico. Apagar uma meta com aportes vinculados perderia contexto.</summary>
    public async Task ArquivarMetaAsync(int id, bool arquivar)
    {
        await using var db = await fabrica.CreateDbContextAsync();
        var meta = await db.Metas.FirstAsync(m => m.Id == id);
        meta.ArquivadaEm = arquivar ? relogio.Agora : null;
        await db.SaveChangesAsync();
    }
}
