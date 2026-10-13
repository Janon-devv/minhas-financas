#:project ../src/MinhasFinancas.Infra/MinhasFinancas.Infra.csproj
#:property PublishAot=false
#:property IsAotCompatible=false

// Carrega o cenario ficticio de docs/fixtures/cenario_exemplo.json num banco, para conferir as telas
// com dados sem precisar digitar tudo na mao. Nada aqui e dado real de ninguem.
//
// Uso: dotnet run tools/semear-cenario.cs -- "C:\caminho\de\teste.db"
//
// Exige um caminho explicito e se recusa a rodar contra o banco padrao do app, porque a
// primeira coisa que ele faz e apagar tudo que encontra. Dado de verdade nao se mistura
// com cenario ficticio.

using Microsoft.EntityFrameworkCore;
using MinhasFinancas.Domain;
using MinhasFinancas.Domain.Modelo;
using MinhasFinancas.Infra;
using System.Text.Json;

if (args.Length == 0)
{
    Console.Error.WriteLine("Informe o caminho de um banco de TESTE.");
    Console.Error.WriteLine("Exemplo: dotnet run tools/semear-cenario.cs -- \"C:\\temp\\exemplo.db\"");
    return 1;
}

var caminho = Path.GetFullPath(args[0]);

if (string.Equals(caminho, Path.GetFullPath(CaminhoBanco.Padrao()), StringComparison.OrdinalIgnoreCase))
{
    Console.Error.WriteLine("Esse e o banco onde ficam os seus dados de verdade.");
    Console.Error.WriteLine("O semeador apaga tudo antes de carregar, entao use outro caminho.");
    return 1;
}
var raiz = Directory.GetCurrentDirectory();
var arquivo = Path.Combine(raiz, "docs", "fixtures", "cenario_exemplo.json");

if (!File.Exists(arquivo))
{
    Console.Error.WriteLine($"Nao encontrei {arquivo}. Rode a partir da raiz do repositorio.");
    return 1;
}

var json = JsonDocument.Parse(File.ReadAllText(arquivo)).RootElement;
var configuracao = json.GetProperty("configuracao");

var opcoes = new DbContextOptionsBuilder<FinancasDbContext>()
    .UseSqlite($"Data Source={caminho}")
    .Options;

await using var db = new FinancasDbContext(opcoes);
await db.Database.MigrateAsync();

// Limpa antes, para rodar duas vezes nao duplicar nada.
await db.Lancamentos.ExecuteDeleteAsync();
await db.DiariosReais.ExecuteDeleteAsync();
await db.Cartoes.ExecuteDeleteAsync();
await db.Tags.ExecuteDeleteAsync();
await db.Metas.ExecuteDeleteAsync();
await db.Configuracoes.ExecuteDeleteAsync();

db.Configuracoes.Add(new Configuracao
{
    Id = 1,
    SaldoAncoraCentavos = configuracao.GetProperty("saldo_ancora_centavos").GetInt64(),
    DataAncora = DateOnly.Parse(configuracao.GetProperty("data_ancora").GetString()!),
    DiarioPrevistoCentavos = configuracao.GetProperty("diario_previsto_centavos").GetInt64(),
    SaldoConfortoCentavos = configuracao.GetProperty("saldo_conforto_centavos").GetInt64(),
    ContarEntradasIncertas = configuracao.GetProperty("contar_entradas_incertas").GetBoolean(),
    JaGuardadoCentavos = configuracao.GetProperty("ja_guardado_centavos").GetInt64(),
    HorizonteMeses = 24,
    ConfiguracaoConcluida = true
});

var cartoes = new Dictionary<string, Cartao>();

foreach (var item in json.GetProperty("cartoes").EnumerateArray())
{
    var cartao = new Cartao
    {
        Nome = item.GetProperty("nome").GetString()!,
        DiaFechamento = item.GetProperty("dia_fechamento").GetInt32(),
        DiaVencimento = item.GetProperty("dia_vencimento").GetInt32()
    };

    cartoes[cartao.Nome] = cartao;
    db.Cartoes.Add(cartao);
}

var tags = new Dictionary<string, Tag>();

foreach (var item in json.GetProperty("tags").EnumerateArray())
{
    var tag = new Tag { Nome = item.GetString()! };
    tags[tag.Nome] = tag;
    db.Tags.Add(tag);
}

// Grava cartoes e tags primeiro para os lancamentos terem ids validos.
await db.SaveChangesAsync();

foreach (var item in json.GetProperty("lancamentos").EnumerateArray())
{
    var tipo = item.GetProperty("tipo").GetString() switch
    {
        "Entrada" => TipoLancamento.Entrada,
        "Saída" => TipoLancamento.Saida,
        "Cartão" => TipoLancamento.Cartao,
        _ => TipoLancamento.Economia
    };

    var nomeCartao = item.GetProperty("cartao").ValueKind == JsonValueKind.Null
        ? null
        : item.GetProperty("cartao").GetString();

    var nomeTag = item.GetProperty("tag").ValueKind == JsonValueKind.Null
        ? null
        : item.GetProperty("tag").GetString();

    var certeza = item.GetProperty("certeza").ValueKind == JsonValueKind.Null
        ? (Certeza?)null
        : Enum.Parse<Certeza>(item.GetProperty("certeza").GetString()!);

    db.Lancamentos.Add(new Lancamento
    {
        Data = DateOnly.Parse(item.GetProperty("data").GetString()!),
        Descricao = item.GetProperty("descricao").GetString()!,
        Tipo = tipo,
        ValorCentavos = item.GetProperty("valor_centavos").GetInt64(),
        Vezes = item.GetProperty("vezes").ValueKind == JsonValueKind.Null
            ? null
            : item.GetProperty("vezes").GetInt32(),
        CartaoId = nomeCartao is null ? null : cartoes[nomeCartao].Id,
        TagId = nomeTag is null ? null : tags[nomeTag].Id,
        Certeza = certeza
    });
}

foreach (var item in json.GetProperty("diario_real").EnumerateArray())
{
    db.DiariosReais.Add(new DiarioReal
    {
        Data = DateOnly.Parse(item.GetProperty("data").GetString()!),
        ValorCentavos = item.GetProperty("valor_centavos").GetInt64()
    });
}

await db.SaveChangesAsync();

Console.WriteLine($"Cenario de exemplo carregado em {caminho}");
Console.WriteLine($"Ancora: {configuracao.GetProperty("data_ancora").GetString()}, " +
                  $"{db.Lancamentos.Count()} lancamentos, {db.DiariosReais.Count()} diario(s).");
return 0;
