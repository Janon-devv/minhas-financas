using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using MinhasFinancas.Domain;
using MinhasFinancas.Domain.Modelo;

namespace MinhasFinancas.Infra.Servicos;

public sealed record ArquivoBackup
{
    public int SchemaVersion { get; init; } = BackupService.VersaoSchema;

    public DateTimeOffset ExportadoEm { get; init; }

    public Configuracao? Configuracao { get; init; }

    public List<Cartao> Cartoes { get; init; } = [];

    public List<Tag> Tags { get; init; } = [];

    public List<Meta> Metas { get; init; } = [];

    public List<Lancamento> Lancamentos { get; init; } = [];

    public List<DiarioReal> Diarios { get; init; } = [];

    public List<AncoraHistorico> Ancoras { get; init; } = [];

    /// <summary>
    /// Versao 2 em diante. Num arquivo da versao 1 esta lista vem vazia, que e exatamente o que
    /// a versao 1 queria dizer: nenhuma ocorrencia tinha situacao registrada.
    /// </summary>
    public List<SituacaoOcorrencia> Situacoes { get; init; } = [];

    /// <summary>Versao 3 em diante. Vazia num arquivo antigo: so existia a conta corrente.</summary>
    public List<Carteira> Carteiras { get; init; } = [];

    /// <summary>Versao 3 em diante. Vazia num arquivo antigo: cada compra saia sozinha (R16).</summary>
    public List<FaturaCartao> Faturas { get; init; } = [];

    /// <summary>Versao 4 em diante. Vazia num arquivo antigo: nao existia ganho variavel (R17).</summary>
    public List<GanhoVariavel> GanhosVariaveis { get; init; } = [];

    /// <summary>
    /// Versao 5 em diante. Ate a 4, o pago ficava na propria fatura, somado, com a data do ultimo
    /// pagamento; a restauracao converte isso num pagamento so (ver BackupService).
    /// </summary>
    public List<PagamentoFatura> PagamentosFatura { get; init; } = [];
}

public sealed record ResultadoRestauracao
{
    public bool Ok { get; init; }

    public List<string> Erros { get; init; } = [];

    public List<string> Resumo { get; init; } = [];

    /// <summary>Onde foram guardados os dados que existiam antes da restauracao.</summary>
    public string? BackupDoQueExistia { get; init; }
}

/// <summary>
/// Backup e restauracao em JSON. A restauracao e integral e substitui tudo: nao existe
/// mesclagem automatica por nome, que e o tipo de coisa que duplica lancamento sem avisar.
/// </summary>
public sealed class BackupService(
    IDbContextFactory<FinancasDbContext> fabrica,
    IRelogio relogio,
    ArmazenamentoLocal local)
{
    /// <summary>
    /// 1: formato original. 2: cor na tag, liga/desliga do diario previsto e a situacao de cada
    /// ocorrencia. 3: repeticao semanal, ocorrencia pulada, carteiras e faturas. 4: ganho variavel.
    /// 5: pagamentos de fatura um a um. Todo campo novo tem padrao neutro, entao arquivo antigo e
    /// lido direto: o que falta nele simplesmente nao existia. A unica conversao e a do pago da
    /// fatura, que ate a versao 4 morava dentro dela.
    /// </summary>
    public const int VersaoSchema = 5;

    private static readonly JsonSerializerOptions Opcoes = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<string> ExportarAsync()
    {
        await using var db = await fabrica.CreateDbContextAsync();

        var arquivo = new ArquivoBackup
        {
            ExportadoEm = relogio.Agora,
            Configuracao = await db.Configuracoes.AsNoTracking().FirstOrDefaultAsync(),
            Cartoes = await db.Cartoes.AsNoTracking().ToListAsync(),
            Tags = await db.Tags.AsNoTracking().ToListAsync(),
            Metas = await db.Metas.AsNoTracking().ToListAsync(),
            Lancamentos = await db.Lancamentos.AsNoTracking().ToListAsync(),
            Diarios = await db.DiariosReais.AsNoTracking().ToListAsync(),
            Ancoras = await db.Ancoras.AsNoTracking().ToListAsync(),
            Situacoes = await db.Situacoes.AsNoTracking().ToListAsync(),
            Carteiras = await db.Carteiras.AsNoTracking().ToListAsync(),
            Faturas = await db.Faturas.AsNoTracking().ToListAsync(),
            PagamentosFatura = await db.PagamentosFatura.AsNoTracking().ToListAsync(),
            GanhosVariaveis = await db.GanhosVariaveis.AsNoTracking().ToListAsync()
        };

        return JsonSerializer.Serialize(arquivo, Opcoes);
    }

    public string NomeSugerido() => $"minhas-financas-{relogio.Agora:yyyy-MM-dd-HHmm}.json";

    /// <summary>
    /// Confere o arquivo e devolve um resumo. Com confirmar = false nada e alterado,
    /// que e o que a tela usa para mostrar o que vai entrar antes de o usuario decidir.
    /// </summary>
    public async Task<ResultadoRestauracao> RestaurarAsync(string json, bool confirmar)
    {
        ArquivoBackup? arquivo;

        try
        {
            arquivo = JsonSerializer.Deserialize<ArquivoBackup>(json, Opcoes);
        }
        catch (JsonException erro)
        {
            return new ResultadoRestauracao { Erros = ["O arquivo não é um JSON válido: " + erro.Message] };
        }

        if (arquivo is null)
        {
            return new ResultadoRestauracao { Erros = ["O arquivo está vazio."] };
        }

        if (arquivo.SchemaVersion < 5)
        {
            arquivo.PagamentosFatura.AddRange(PagamentosDoFormatoAntigo(json));
        }

        var erros = Conferir(arquivo);

        if (erros.Count > 0)
        {
            return new ResultadoRestauracao { Erros = erros };
        }

        var resumo = new List<string>
        {
            $"{arquivo.Lancamentos.Count} lançamento(s)",
            $"{arquivo.Diarios.Count} dia(s) com gasto registrado",
            $"{arquivo.Cartoes.Count} cartão(ões)",
            $"{arquivo.Tags.Count} tag(s)",
            $"{arquivo.Metas.Count} meta(s)",
            $"{arquivo.Situacoes.Count} conta(s) com pagamento registrado",
            $"{arquivo.Carteiras.Count} carteira(s) além da conta corrente",
            $"{arquivo.Faturas.Count} fatura(s) de cartão",
            $"{arquivo.PagamentosFatura.Count} pagamento(s) de fatura",
            $"{arquivo.GanhosVariaveis.Count} mês(es) com renda variável"
        };

        if (!confirmar)
        {
            return new ResultadoRestauracao { Ok = true, Resumo = resumo };
        }

        // Guarda o que existia antes, porque restaurar substitui tudo. Na pasta de backups do
        // banco em uso: restaurar num banco de teste nao pode deixar copia junto das de verdade.
        var anterior = await ExportarAsync();
        Directory.CreateDirectory(local.PastaBackups);
        var caminhoAnterior = Path.Combine(
            local.PastaBackups,
            $"{CopiaDeSeguranca.PrefixoAntesDeRestaurar}{relogio.Agora:yyyy-MM-dd-HHmmss}.json");
        await File.WriteAllTextAsync(caminhoAnterior, anterior);
        CopiaDeSeguranca.Rodizio(local.PastaBackups, CopiaDeSeguranca.PrefixoAntesDeRestaurar, ".json", manter: 20);

        try
        {
            await SubstituirTudoAsync(arquivo);
        }
        catch (Exception erro) when (erro is DbUpdateException or InvalidOperationException)
        {
            // A transacao nao foi confirmada, entao o banco continua exatamente como estava.
            return new ResultadoRestauracao
            {
                Erros = ["O banco recusou o arquivo e nada foi alterado. Detalhe: " +
                         (erro.InnerException?.Message ?? erro.Message)]
            };
        }

        return new ResultadoRestauracao
        {
            Ok = true,
            Resumo = resumo,
            BackupDoQueExistia = caminhoAnterior
        };
    }

    /// <summary>Apaga tudo e grava o arquivo numa transacao so: ou entra inteiro, ou nada muda.</summary>
    private async Task SubstituirTudoAsync(ArquivoBackup arquivo)
    {
        await using var db = await fabrica.CreateDbContextAsync();
        await using var transacao = await db.Database.BeginTransactionAsync();

        // A ordem importa por causa das chaves estrangeiras dos lancamentos.
        await db.Situacoes.ExecuteDeleteAsync();
        await db.Lancamentos.ExecuteDeleteAsync();
        await db.DiariosReais.ExecuteDeleteAsync();
        await db.GanhosVariaveis.ExecuteDeleteAsync();
        await db.PagamentosFatura.ExecuteDeleteAsync();
        await db.Faturas.ExecuteDeleteAsync();
        await db.Carteiras.ExecuteDeleteAsync();
        await db.Ancoras.ExecuteDeleteAsync();
        await db.Cartoes.ExecuteDeleteAsync();
        await db.Tags.ExecuteDeleteAsync();
        await db.Metas.ExecuteDeleteAsync();
        await db.Configuracoes.ExecuteDeleteAsync();

        if (arquivo.Configuracao is not null)
        {
            arquivo.Configuracao.Id = 1;
            db.Configuracoes.Add(arquivo.Configuracao);
        }

        db.Cartoes.AddRange(arquivo.Cartoes);
        db.Tags.AddRange(arquivo.Tags);
        db.Metas.AddRange(arquivo.Metas);
        db.Lancamentos.AddRange(arquivo.Lancamentos);
        db.DiariosReais.AddRange(arquivo.Diarios);
        db.Ancoras.AddRange(arquivo.Ancoras);
        db.Carteiras.AddRange(arquivo.Carteiras);
        db.Faturas.AddRange(arquivo.Faturas);
        db.PagamentosFatura.AddRange(arquivo.PagamentosFatura);
        db.GanhosVariaveis.AddRange(arquivo.GanhosVariaveis);
        db.Situacoes.AddRange(arquivo.Situacoes);

        await db.SaveChangesAsync();
        await transacao.CommitAsync();
    }

    /// <summary>
    /// Ate a versao 4, a fatura guardava o pago somado (pagoCentavos) e a data do ultimo
    /// pagamento (pagoEm). Esses campos nao existem mais no modelo, entao sao lidos direto do
    /// JSON e viram um pagamento so, na data do ultimo. E o mesmo que a migracao do banco faz:
    /// a informacao de quando saiu cada parte nunca foi gravada, entao nao ha como recupera-la.
    /// </summary>
    private static List<PagamentoFatura> PagamentosDoFormatoAntigo(string json)
    {
        var pagamentos = new List<PagamentoFatura>();
        using var documento = JsonDocument.Parse(json);

        if (Campo(documento.RootElement, "faturas") is not { ValueKind: JsonValueKind.Array } faturas)
        {
            return pagamentos;
        }

        foreach (var fatura in faturas.EnumerateArray())
        {
            if (Campo(fatura, "id") is not { ValueKind: JsonValueKind.Number } id ||
                Campo(fatura, "pagoCentavos") is not { ValueKind: JsonValueKind.Number } pago ||
                !pago.TryGetInt64(out var valor) || valor <= 0)
            {
                continue;
            }

            var data = Campo(fatura, "pagoEm") is { ValueKind: JsonValueKind.String } pagoEm &&
                       DateOnly.TryParse(pagoEm.GetString(), CultureInfo.InvariantCulture, out var quando)
                ? quando
                : Campo(fatura, "vencimento") is { ValueKind: JsonValueKind.String } vencimento &&
                  DateOnly.TryParse(vencimento.GetString(), CultureInfo.InvariantCulture, out var venceu)
                    ? venceu
                    : default;

            pagamentos.Add(new PagamentoFatura { FaturaId = id.GetInt32(), Data = data, ValorCentavos = valor });
        }

        return pagamentos;
    }

    /// <summary>Le um campo sem ligar para maiusculas, como a desserializacao do resto do arquivo.</summary>
    private static JsonElement? Campo(JsonElement objeto, string nome)
    {
        if (objeto.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var propriedade in objeto.EnumerateObject())
        {
            if (string.Equals(propriedade.Name, nome, StringComparison.OrdinalIgnoreCase))
            {
                return propriedade.Value;
            }
        }

        return null;
    }

    private static List<string> Conferir(ArquivoBackup arquivo)
    {
        var erros = new List<string>();

        // Arquivo mais antigo e aceito: os campos que faltam nele nascem no padrao, que e o
        // comportamento da versao em que ele foi gerado. Recusar deixaria o dono com uma pasta
        // de backups que o proprio app nao abre mais, que e o pior desfecho possivel.
        if (arquivo.SchemaVersion > VersaoSchema)
        {
            erros.Add($"O arquivo é da versão {arquivo.SchemaVersion}, e este app lê até a versão {VersaoSchema}. " +
                      "Ele veio de uma versão mais nova do app.");
        }
        else if (arquivo.SchemaVersion < 1)
        {
            erros.Add($"O arquivo diz ser da versão {arquivo.SchemaVersion}, que não existe.");
        }

        if (arquivo.Configuracao is null)
        {
            erros.Add("O arquivo não tem configuração.");
        }
        else
        {
            erros.AddRange(Validacao.Configuracao(arquivo.Configuracao));
        }

        // O que entra aqui e o que quebraria a gravacao (chave repetida, referencia solta) ou o
        // calculo (cartao com dia zero, data no ano 1). Regra de tela que nao quebra nada, como
        // "cartao nao aceita repeticao semanal", fica de fora de proposito: um backup antigo
        // feito antes da regra existir continua abrindo.
        IdsUnicos(erros, "cartões", arquivo.Cartoes.Select(c => c.Id));
        IdsUnicos(erros, "tags", arquivo.Tags.Select(t => t.Id));
        IdsUnicos(erros, "metas", arquivo.Metas.Select(m => m.Id));
        IdsUnicos(erros, "lançamentos", arquivo.Lancamentos.Select(l => l.Id));
        IdsUnicos(erros, "carteiras", arquivo.Carteiras.Select(c => c.Id));
        IdsUnicos(erros, "faturas", arquivo.Faturas.Select(f => f.Id));
        IdsUnicos(erros, "pagamentos de fatura", arquivo.PagamentosFatura.Select(p => p.Id));
        IdsUnicos(erros, "gastos do dia", arquivo.Diarios.Select(d => d.Id));
        IdsUnicos(erros, "contas confirmadas", arquivo.Situacoes.Select(s => s.Id));
        IdsUnicos(erros, "conferências", arquivo.Ancoras.Select(a => a.Id));
        IdsUnicos(erros, "meses de renda variável", arquivo.GanhosVariaveis.Select(g => g.Id));

        NomesUnicos(erros, "cartões", arquivo.Cartoes.Select(c => c.Nome));
        NomesUnicos(erros, "tags", arquivo.Tags.Select(t => t.Nome));
        NomesUnicos(erros, "metas", arquivo.Metas.Select(m => m.Nome));
        NomesUnicos(erros, "carteiras", arquivo.Carteiras.Select(c => c.Nome));

        foreach (var cartao in arquivo.Cartoes)
        {
            erros.AddRange(Validacao.Cartao(cartao).Select(e => $"Cartão \"{cartao.Nome}\": {e}"));
        }

        foreach (var carteira in arquivo.Carteiras)
        {
            erros.AddRange(Validacao.Carteira(carteira).Select(e => $"Carteira \"{carteira.Nome}\": {e}"));
        }

        var idsCartoes = arquivo.Cartoes.Select(c => c.Id).ToHashSet();
        var idsTags = arquivo.Tags.Select(t => t.Id).ToHashSet();
        var idsMetas = arquivo.Metas.Select(m => m.Id).ToHashSet();
        var idsCarteiras = arquivo.Carteiras.Select(c => c.Id).ToHashSet();
        var idsLancamentos = arquivo.Lancamentos.Select(l => l.Id).ToHashSet();
        var idsFaturas = arquivo.Faturas.Select(f => f.Id).ToHashSet();

        foreach (var lancamento in arquivo.Lancamentos)
        {
            var nome = $"O lançamento \"{lancamento.Descricao}\"";

            if (lancamento.CartaoId is int cartao && !idsCartoes.Contains(cartao))
            {
                erros.Add($"{nome} usa um cartão que não está no arquivo.");
            }

            if (lancamento.TagId is int tag && !idsTags.Contains(tag))
            {
                erros.Add($"{nome} usa uma tag que não está no arquivo.");
            }

            if (lancamento.MetaId is int meta && !idsMetas.Contains(meta))
            {
                erros.Add($"{nome} usa uma meta que não está no arquivo.");
            }

            if (lancamento.CarteiraId is int carteira && !idsCarteiras.Contains(carteira))
            {
                erros.Add($"{nome} usa uma carteira que não está no arquivo.");
            }

            if (lancamento.ValorCentavos <= 0 || !Validacao.ValorPlausivel(lancamento.ValorCentavos))
            {
                erros.Add($"{nome} tem valor zero, negativo ou alto demais.");
            }

            if (!Validacao.DataPlausivel(lancamento.Data))
            {
                erros.Add($"{nome} tem data fora de {Validacao.DataMinima.Year} a {Validacao.DataMaxima.Year}.");
            }
        }

        foreach (var situacao in arquivo.Situacoes)
        {
            if (!idsLancamentos.Contains(situacao.LancamentoId))
            {
                erros.Add("Uma conta confirmada aponta para um lançamento que não está no arquivo.");
            }
        }

        ChavesUnicas(erros, "contas confirmadas", arquivo.Situacoes.Select(s => (s.LancamentoId, s.Data)));

        foreach (var diario in arquivo.Diarios)
        {
            if (diario.ValorCentavos < 0 || !Validacao.ValorPlausivel(diario.ValorCentavos))
            {
                erros.Add("O arquivo tem gasto do dia com valor negativo ou alto demais.");
            }

            if (diario.CarteiraId is int carteira && !idsCarteiras.Contains(carteira))
            {
                erros.Add("Um gasto do dia aponta para uma carteira que não está no arquivo.");
            }

            if (!Validacao.DataPlausivel(diario.Data))
            {
                erros.Add("O arquivo tem gasto do dia com data fora da faixa.");
            }
        }

        ChavesUnicas(erros, "gastos do dia", arquivo.Diarios.Select(d => (d.CarteiraId, d.Data)));

        foreach (var fatura in arquivo.Faturas)
        {
            if (!idsCartoes.Contains(fatura.CartaoId))
            {
                erros.Add("Uma fatura aponta para um cartão que não está no arquivo.");
            }

            if (!Validacao.DataPlausivel(fatura.Vencimento))
            {
                erros.Add("O arquivo tem fatura com vencimento fora da faixa.");
            }

            if (fatura.TotalInformadoCentavos is long total && (total < 0 || !Validacao.ValorPlausivel(total)))
            {
                erros.Add("O arquivo tem fatura com total negativo ou alto demais.");
            }
        }

        ChavesUnicas(erros, "faturas", arquivo.Faturas.Select(f => (f.CartaoId, f.Vencimento)));

        foreach (var pagamento in arquivo.PagamentosFatura)
        {
            if (!idsFaturas.Contains(pagamento.FaturaId))
            {
                erros.Add("Um pagamento de fatura aponta para uma fatura que não está no arquivo.");
            }

            if (pagamento.ValorCentavos <= 0 || !Validacao.ValorPlausivel(pagamento.ValorCentavos))
            {
                erros.Add("O arquivo tem pagamento de fatura com valor zero, negativo ou alto demais.");
            }

            if (!Validacao.DataPlausivel(pagamento.Data))
            {
                erros.Add("O arquivo tem pagamento de fatura com data fora da faixa.");
            }
        }

        foreach (var ganho in arquivo.GanhosVariaveis)
        {
            if (ganho.Mes is < 1 or > 12 || ganho.Ano < Validacao.DataMinima.Year || ganho.Ano > Validacao.DataMaxima.Year)
            {
                erros.Add("O arquivo tem renda variável num mês que não existe.");
            }

            erros.AddRange(Validacao.GanhoVariavel(ganho.ValorCentavos));
        }

        ChavesUnicas(erros, "meses de renda variável", arquivo.GanhosVariaveis.Select(g => (g.Ano, g.Mes)));

        // Nao vale a pena despejar cem linhas iguais na tela.
        return erros.Distinct().Take(10).ToList();
    }

    /// <summary>Id repetido faria o banco recusar a gravacao no meio. Id zero o banco gera.</summary>
    private static void IdsUnicos(List<string> erros, string oQue, IEnumerable<int> ids)
    {
        if (ids.Where(id => id != 0).GroupBy(id => id).Any(g => g.Count() > 1))
        {
            erros.Add($"O arquivo tem {oQue} com o mesmo código repetido.");
        }
    }

    private static void NomesUnicos(List<string> erros, string oQue, IEnumerable<string> nomes)
    {
        var repetido = nomes
            .GroupBy(n => n.Trim(), StringComparer.CurrentCultureIgnoreCase)
            .FirstOrDefault(g => g.Count() > 1);

        if (repetido is not null)
        {
            erros.Add($"O arquivo tem {oQue} com o nome \"{repetido.Key}\" repetido.");
        }
    }

    /// <summary>As combinacoes que o banco exige unicas (uma fatura por cartao e vencimento etc.).</summary>
    private static void ChavesUnicas<T>(List<string> erros, string oQue, IEnumerable<T> chaves)
    {
        if (chaves.GroupBy(c => c).Any(g => g.Count() > 1))
        {
            erros.Add($"O arquivo repete {oQue} no mesmo dia ou mês.");
        }
    }
}
