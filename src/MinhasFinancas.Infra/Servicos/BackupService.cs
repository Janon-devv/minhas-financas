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
    public const int VersaoSchema = 1;

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
            Ancoras = await db.Ancoras.AsNoTracking().ToListAsync()
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
            $"{arquivo.Metas.Count} meta(s)"
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
        await db.Lancamentos.ExecuteDeleteAsync();
        await db.DiariosReais.ExecuteDeleteAsync();
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

        if (arquivo.SchemaVersion != VersaoSchema)
        {
            erros.Add($"O arquivo e da versao {arquivo.SchemaVersion} e este programa le a versao {VersaoSchema}.");
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
