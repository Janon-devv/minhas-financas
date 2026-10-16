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

    public List<SituacaoOcorrencia> Situacoes { get; init; } = [];

    /// <summary>Carteiras alem da conta corrente. Vazia quer dizer so conta corrente (R13).</summary>
    public List<Carteira> Carteiras { get; init; } = [];

    public List<FaturaCartao> Faturas { get; init; } = [];

    /// <summary>Ganho variavel registrado mes a mes (R17).</summary>
    public List<GanhoVariavel> GanhosVariaveis { get; init; } = [];
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
            Diarios = await db.DiariosReais.AsNoTracking().ToListAsync(),
            Situacoes = await db.Situacoes.AsNoTracking().ToListAsync(),
            Carteiras = await db.Carteiras.AsNoTracking().OrderBy(c => c.Ordem).ThenBy(c => c.Id).ToListAsync(),
            Faturas = await db.Faturas.AsNoTracking().OrderBy(f => f.Vencimento).ToListAsync(),
            GanhosVariaveis = await db.GanhosVariaveis.AsNoTracking()
                .OrderByDescending(g => g.Ano).ThenByDescending(g => g.Mes).ToListAsync()
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
            Diarios = dados.Diarios,
            Carteiras = dados.Carteiras,
            Faturas = dados.Faturas,
            GanhosVariaveis = dados.GanhosVariaveis,
            Situacoes = dados.Situacoes,
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
        cfg.DiarioPrevistoAtivo = novo.DiarioPrevistoAtivo;
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
    /// Conferencia com o banco: troca a ancora pelo saldo que o banco mostra.
    /// Nao apaga nada; o que ficou antes da nova ancora so deixa de contar.
    /// </summary>
    /// <param name="saldoCentavos">
    /// O saldo que o banco mostra agora, com os gastos do dia ja dentro dele.
    /// </param>
    /// <param name="conferidoEm">O dia em que a conferencia foi feita, normalmente hoje.</param>
    /// <remarks>
    /// A ancora e o saldo do INICIO de um dia, antes dos movimentos dele. O numero do banco
    /// e o de agora, com os gastos de hoje ja descontados. Entao ele e o saldo do inicio de
    /// AMANHA, nao o de hoje: gravar na data de hoje faria o app descontar o diario de hoje
    /// de novo, e a conferencia nunca fecharia.
    /// </remarks>
    public async Task RecalibrarAsync(long saldoCentavos, DateOnly conferidoEm, string? observacao = null)
    {
        await using var db = await fabrica.CreateDbContextAsync();
        var cfg = await ObterOuCriarAsync(db);

        var novaAncora = conferidoEm.AddDays(1);

        cfg.SaldoAncoraCentavos = saldoCentavos;
        cfg.DataAncora = novaAncora;

        db.Ancoras.Add(new AncoraHistorico
        {
            Data = novaAncora,
            SaldoCentavos = saldoCentavos,
            CriadoEm = relogio.Agora,
            Observacao = observacao ?? $"Conferencia com o banco em {conferidoEm:dd/MM/yyyy}"
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

        // SetValues em vez de campo a campo: campo novo no modelo passa a ser gravado
        // sozinho. Copiando na mao, quem esquecesse de incluir o campo aqui o perderia
        // silenciosamente em toda edicao, sem erro nenhum.
        var atual = await db.Lancamentos.FirstAsync(l => l.Id == lancamento.Id);
        db.Entry(atual).CurrentValues.SetValues(lancamento);

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

        // As situacoes apontam para o lancamento por Id. Sem apagar junto, elas ficariam orfas
        // e um Id reaproveitado herdaria pagamentos de outra conta.
        await db.Situacoes.Where(x => x.LancamentoId == id).ExecuteDeleteAsync();
        await db.Lancamentos.Where(l => l.Id == id).ExecuteDeleteAsync();
    }

    // ---------- Situacao de cada ocorrencia (R12) ----------

    /// <summary>
    /// Registra que a ocorrencia saiu de verdade. <paramref name="valorCentavos"/> so e gravado
    /// quando difere do previsto, que e o caso de conta que varia, como luz.
    /// </summary>
    public async Task MarcarPagoAsync(int lancamentoId, DateOnly data, DateOnly pagoEm, long? valorCentavos = null)
    {
        await using var db = await fabrica.CreateDbContextAsync();
        var atual = await db.Situacoes.FirstOrDefaultAsync(x => x.LancamentoId == lancamentoId && x.Data == data);

        if (atual is null)
        {
            db.Situacoes.Add(new SituacaoOcorrencia
            {
                LancamentoId = lancamentoId,
                Data = data,
                Pago = true,
                PagoEm = pagoEm,
                ValorCentavos = valorCentavos
            });
        }
        else
        {
            atual.Pago = true;
            atual.Pulada = false;
            atual.PagoEm = pagoEm;
            atual.ValorCentavos = valorCentavos ?? atual.ValorCentavos;
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Registra que a ocorrencia nao aconteceu neste periodo: academia trancada, mes sem a compra.
    /// Ela some da projecao sem mexer no lancamento, que continua valendo nos outros periodos.
    /// </summary>
    public async Task MarcarPuladaAsync(int lancamentoId, DateOnly data)
    {
        await using var db = await fabrica.CreateDbContextAsync();
        var atual = await db.Situacoes.FirstOrDefaultAsync(x => x.LancamentoId == lancamentoId && x.Data == data);

        if (atual is null)
        {
            db.Situacoes.Add(new SituacaoOcorrencia { LancamentoId = lancamentoId, Data = data, Pulada = true });
        }
        else
        {
            atual.Pulada = true;
            atual.Pago = false;
            atual.PagoEm = null;
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Registra que a ocorrencia ainda nao saiu. Em conta que venceu antes da ancora e isto que
    /// traz a saida de volta para a projecao, em vez de ela sumir dentro do saldo de partida.
    /// </summary>
    public async Task MarcarEmAbertoAsync(int lancamentoId, DateOnly data)
    {
        await using var db = await fabrica.CreateDbContextAsync();
        var atual = await db.Situacoes.FirstOrDefaultAsync(x => x.LancamentoId == lancamentoId && x.Data == data);

        if (atual is null)
        {
            db.Situacoes.Add(new SituacaoOcorrencia { LancamentoId = lancamentoId, Data = data, Pago = false });
        }
        else
        {
            atual.Pago = false;
            atual.Pulada = false;
            atual.PagoEm = null;
        }

        await db.SaveChangesAsync();
    }

    /// <summary>Apaga o registro: a ocorrencia volta a valer o padrao, como se nunca tivesse sido tocada.</summary>
    public async Task LimparSituacaoAsync(int lancamentoId, DateOnly data)
    {
        await using var db = await fabrica.CreateDbContextAsync();
        await db.Situacoes.Where(x => x.LancamentoId == lancamentoId && x.Data == data).ExecuteDeleteAsync();
    }

    /// <summary>
    /// As contas do periodo que importa para marcar pago: 45 dias para tras, que cobrem o mes
    /// passado inteiro em qualquer dia do mes sem virar historico, e para a frente ate o fim do
    /// mes corrente, para a conta do mes aparecer antes de vencer.
    /// </summary>
    public List<Pendencia> ListarPendencias(DadosPlanejamento dados, DateOnly hoje)
    {
        var fimDoMes = new DateOnly(hoje.Year, hoje.Month, DateTime.DaysInMonth(hoje.Year, hoje.Month));

        return Pendencias.Listar(
            dados.Lancamentos,
            dados.Cartoes.ToDictionary(c => c.Id),
            dados.Situacoes,
            hoje.AddDays(-45),
            fimDoMes,
            hoje,
            dados.Configuracao.DataAncora);
    }

    // ---------- Diario ----------

    public async Task RegistrarDiarioAsync(DateOnly data, long valorCentavos, int? carteiraId = null)
    {
        await using var db = await fabrica.CreateDbContextAsync();
        var atual = await db.DiariosReais.FirstOrDefaultAsync(d => d.Data == data && d.CarteiraId == carteiraId);

        if (atual is null)
        {
            db.DiariosReais.Add(new DiarioReal { Data = data, CarteiraId = carteiraId, ValorCentavos = valorCentavos });
        }
        else
        {
            atual.ValorCentavos = valorCentavos;
        }

        await db.SaveChangesAsync();
    }

    /// <summary>Apaga o registro do dia, para a projecao voltar a usar o diario previsto.</summary>
    public async Task VoltarParaPrevisaoAsync(DateOnly data, int? carteiraId = null)
    {
        await using var db = await fabrica.CreateDbContextAsync();
        await db.DiariosReais.Where(d => d.Data == data && d.CarteiraId == carteiraId).ExecuteDeleteAsync();
    }

    // ---------- Ganho variavel (R17) ----------

    /// <summary>
    /// Grava o ganho do mes. Valor zero e resposta valida ("este mes nao rendeu nada"), por isso
    /// quem apaga o registro e o LimparGanhoVariavelAsync, e nao um zero digitado.
    /// </summary>
    public async Task SalvarGanhoVariavelAsync(int ano, int mes, long valorCentavos, string? obs)
    {
        await using var db = await fabrica.CreateDbContextAsync();
        var atual = await db.GanhosVariaveis.FirstOrDefaultAsync(g => g.Ano == ano && g.Mes == mes);

        if (atual is null)
        {
            db.GanhosVariaveis.Add(new GanhoVariavel
            {
                Ano = ano,
                Mes = mes,
                ValorCentavos = valorCentavos,
                Obs = string.IsNullOrWhiteSpace(obs) ? null : obs.Trim()
            });
        }
        else
        {
            atual.ValorCentavos = valorCentavos;
            atual.Obs = string.IsNullOrWhiteSpace(obs) ? null : obs.Trim();
        }

        await db.SaveChangesAsync();
    }

    /// <summary>Volta o mes para "nao respondi ainda", que nao e o mesmo que ganhei zero.</summary>
    public async Task LimparGanhoVariavelAsync(int ano, int mes)
    {
        await using var db = await fabrica.CreateDbContextAsync();
        await db.GanhosVariaveis.Where(g => g.Ano == ano && g.Mes == mes).ExecuteDeleteAsync();
    }

    // ---------- Faturas (R14) ----------

    public async Task SalvarFaturaAsync(FaturaCartao fatura)
    {
        await using var db = await fabrica.CreateDbContextAsync();

        if (fatura.Id == 0)
        {
            db.Faturas.Add(fatura);
        }
        else
        {
            var atual = await db.Faturas.FirstAsync(f => f.Id == fatura.Id);
            db.Entry(atual).CurrentValues.SetValues(fatura);
        }

        await db.SaveChangesAsync();
    }

    public async Task ExcluirFaturaAsync(int id)
    {
        await using var db = await fabrica.CreateDbContextAsync();
        await db.Faturas.Where(f => f.Id == id).ExecuteDeleteAsync();
    }

    /// <summary>
    /// Registra um pagamento, somando ao que ja tinha sido pago. Pagamento parcial e a regra,
    /// nao a excecao: quem paga o minimo paga varias vezes a mesma fatura.
    /// </summary>
    public async Task PagarFaturaAsync(int id, long valorCentavos, DateOnly pagoEm)
    {
        await using var db = await fabrica.CreateDbContextAsync();
        var fatura = await db.Faturas.FirstAsync(f => f.Id == id);

        fatura.PagoCentavos += valorCentavos;
        fatura.PagoEm = pagoEm;

        await db.SaveChangesAsync();
    }

    public List<ResumoFatura> ResumirFaturas(DadosPlanejamento dados, DateOnly hoje) =>
        Faturas.Resumir(dados.Faturas, dados.Lancamentos, dados.Cartoes, hoje);

    // ---------- Carteiras (R13) ----------

    public async Task SalvarCarteiraAsync(Carteira carteira)
    {
        await using var db = await fabrica.CreateDbContextAsync();

        if (carteira.Id == 0)
        {
            db.Carteiras.Add(carteira);
        }
        else
        {
            var atual = await db.Carteiras.FirstAsync(c => c.Id == carteira.Id);
            db.Entry(atual).CurrentValues.SetValues(carteira);
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Recusa apagar carteira em uso: os lancamentos dela voltariam calados para a conta
    /// corrente e o saldo do banco passaria a descontar compra que saiu do beneficio.
    /// </summary>
    public async Task<string?> ExcluirCarteiraAsync(int id)
    {
        await using var db = await fabrica.CreateDbContextAsync();
        var emUso = await db.Lancamentos.CountAsync(l => l.CarteiraId == id);

        if (emUso > 0)
        {
            return $"Essa carteira está em {emUso} {(emUso == 1 ? "lançamento" : "lançamentos")}. Mude a carteira deles antes de excluir.";
        }

        await db.DiariosReais.Where(d => d.CarteiraId == id).ExecuteDeleteAsync();
        await db.Carteiras.Where(c => c.Id == id).ExecuteDeleteAsync();
        return null;
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
            db.Entry(atual).CurrentValues.SetValues(cartao);
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
            return $"Esse cartão está em {emUso} {(emUso == 1 ? "lançamento" : "lançamentos")}. Troque o cartão deles antes de excluir.";
        }

        await using var db = await fabrica.CreateDbContextAsync();
        var faturas = await db.Faturas.CountAsync(f => f.CartaoId == id);

        if (faturas > 0)
        {
            return $"Esse cartão tem {faturas} {(faturas == 1 ? "fatura" : "faturas")}. Apague antes de excluir.";
        }

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
            db.Entry(atual).CurrentValues.SetValues(tag);
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
            db.Entry(atual).CurrentValues.SetValues(meta);
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
