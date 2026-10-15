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
public sealed class BackupService(IDbContextFactory<FinancasDbContext> fabrica, IRelogio relogio)
{
    /// <summary>
    /// 1: formato original. 2: cor na tag, liga/desliga do diario previsto e a situacao de cada
    /// ocorrencia. 3: repeticao semanal, ocorrencia pulada, carteiras e faturas. 4: ganho variavel. Todo campo novo tem padrao neutro, entao arquivo antigo e lido direto,
    /// sem conversao: o que falta nele simplesmente nao existia.
    /// </summary>
    public const int VersaoSchema = 4;

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
            return new ResultadoRestauracao { Erros = ["O arquivo nao e um JSON valido: " + erro.Message] };
        }

        if (arquivo is null)
        {
            return new ResultadoRestauracao { Erros = ["O arquivo esta vazio."] };
        }

        var erros = Conferir(arquivo);

        if (erros.Count > 0)
        {
            return new ResultadoRestauracao { Erros = erros };
        }

        var resumo = new List<string>
        {
            $"{arquivo.Lancamentos.Count} lancamento(s)",
            $"{arquivo.Diarios.Count} dia(s) com diario registrado",
            $"{arquivo.Cartoes.Count} cartao(oes)",
            $"{arquivo.Tags.Count} tag(s)",
            $"{arquivo.Metas.Count} meta(s)",
            $"{arquivo.Situacoes.Count} conta(s) com pagamento registrado",
            $"{arquivo.Carteiras.Count} carteira(s) alem da conta corrente",
            $"{arquivo.Faturas.Count} fatura(s) de cartao",
            $"{arquivo.GanhosVariaveis.Count} mes(es) com ganho variavel"
        };

        if (!confirmar)
        {
            return new ResultadoRestauracao { Ok = true, Resumo = resumo };
        }

        // Guarda o que existia antes, porque restaurar substitui tudo.
        var anterior = await ExportarAsync();
        var pastaBackup = Path.Combine(Path.GetDirectoryName(CaminhoBanco.Padrao())!, "backups");
        Directory.CreateDirectory(pastaBackup);
        var caminhoAnterior = Path.Combine(pastaBackup, $"antes-de-restaurar-{relogio.Agora:yyyy-MM-dd-HHmmss}.json");
        await File.WriteAllTextAsync(caminhoAnterior, anterior);

        await using var db = await fabrica.CreateDbContextAsync();
        await using var transacao = await db.Database.BeginTransactionAsync();

        // A ordem importa por causa das chaves estrangeiras dos lancamentos.
        await db.Situacoes.ExecuteDeleteAsync();
        await db.Lancamentos.ExecuteDeleteAsync();
        await db.DiariosReais.ExecuteDeleteAsync();
        await db.GanhosVariaveis.ExecuteDeleteAsync();
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
        db.GanhosVariaveis.AddRange(arquivo.GanhosVariaveis);
        db.Situacoes.AddRange(arquivo.Situacoes);

        await db.SaveChangesAsync();
        await transacao.CommitAsync();

        return new ResultadoRestauracao
        {
            Ok = true,
            Resumo = resumo,
            BackupDoQueExistia = caminhoAnterior
        };
    }

    private static List<string> Conferir(ArquivoBackup arquivo)
    {
        var erros = new List<string>();

        // Arquivo mais antigo e aceito: os campos que faltam nele nascem no padrao, que e o
        // comportamento da versao em que ele foi gerado. Recusar deixaria o dono com uma pasta
        // de backups que o proprio app nao abre mais, que e o pior desfecho possivel.
        if (arquivo.SchemaVersion > VersaoSchema)
        {
            erros.Add($"O arquivo e da versao {arquivo.SchemaVersion} e este programa le ate a versao {VersaoSchema}. " +
                      "Ele foi gerado por uma versao mais nova do app.");
        }
        else if (arquivo.SchemaVersion < 1)
        {
            erros.Add($"O arquivo diz ser da versao {arquivo.SchemaVersion}, que nao existe.");
        }

        if (arquivo.Configuracao is null)
        {
            erros.Add("O arquivo nao tem configuracao.");
        }
        else
        {
            erros.AddRange(Validacao.Configuracao(arquivo.Configuracao));
        }

        var idsCartoes = arquivo.Cartoes.Select(c => c.Id).ToHashSet();
        var idsTags = arquivo.Tags.Select(t => t.Id).ToHashSet();
        var idsMetas = arquivo.Metas.Select(m => m.Id).ToHashSet();

        foreach (var lancamento in arquivo.Lancamentos)
        {
            if (lancamento.CartaoId is int cartao && !idsCartoes.Contains(cartao))
            {
                erros.Add($"O lancamento \"{lancamento.Descricao}\" aponta para um cartao que nao esta no arquivo.");
            }

            if (lancamento.TagId is int tag && !idsTags.Contains(tag))
            {
                erros.Add($"O lancamento \"{lancamento.Descricao}\" aponta para uma tag que nao esta no arquivo.");
            }

            if (lancamento.MetaId is int meta && !idsMetas.Contains(meta))
            {
                erros.Add($"O lancamento \"{lancamento.Descricao}\" aponta para uma meta que nao esta no arquivo.");
            }

            if (lancamento.ValorCentavos <= 0)
            {
                erros.Add($"O lancamento \"{lancamento.Descricao}\" tem valor zero ou negativo.");
            }
        }

        if (arquivo.Diarios.Any(d => d.ValorCentavos < 0))
        {
            erros.Add("Existe diario com valor negativo no arquivo.");
        }

        // Nao vale a pena despejar cem linhas iguais na tela.
        return erros.Distinct().Take(10).ToList();
    }
}
