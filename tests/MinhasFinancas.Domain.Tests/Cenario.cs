using System.Text.Json;
using System.Text.Json.Serialization;
using MinhasFinancas.Domain;
using MinhasFinancas.Domain.Modelo;

namespace MinhasFinancas.Domain.Tests;

/// <summary>
/// Le o cenario de referencia de docs/fixtures. Os numeros de lá foram conferidos por duas
/// implementacoes independentes, entao servem de gabarito para o motor.
/// </summary>
internal static class Cenario
{
    private static readonly JsonSerializerOptions Opcoes = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public static string Raiz { get; } = LocalizarRaiz();

    public static string Caminho(string nome) => Path.Combine(Raiz, "docs", "fixtures", nome);

    private static string LocalizarRaiz()
    {
        // Procura a pasta que contem os fixtures, subindo a partir da pasta de saida dos testes.
        var marcador = Path.Combine("docs", "fixtures", "cenario_exemplo.json");
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, marcador)))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException(
            $"Nao encontrei {marcador} subindo a partir de {AppContext.BaseDirectory}");
    }

    public static CenarioJson Carregar()
    {
        var json = File.ReadAllText(Caminho("cenario_exemplo.json"));
        return JsonSerializer.Deserialize<CenarioJson>(json, Opcoes)
               ?? throw new InvalidOperationException("cenario_exemplo.json vazio");
    }

    /// <summary>Converte o cenario do arquivo na entrada que o motor espera.</summary>
    public static EntradaProjecao Montar(this CenarioJson cenario)
    {
        var cartoes = cenario.Cartoes
            .Select((c, i) => new Cartao
            {
                Id = i + 1,
                Nome = c.Nome,
                DiaFechamento = c.DiaFechamento,
                DiaVencimento = c.DiaVencimento
            })
            .ToList();

        var tags = cenario.Tags
            .Select((nome, i) => new Tag { Id = i + 1, Nome = nome })
            .ToList();

        var lancamentos = cenario.Lancamentos
            .Select((l, i) => new Lancamento
            {
                Id = i + 1,
                Data = l.Data,
                Descricao = l.Descricao,
                Tipo = TipoDe(l.Tipo),
                ValorCentavos = l.ValorCentavos,
                Vezes = l.Vezes,
                CartaoId = l.Cartao is null ? null : cartoes.First(c => c.Nome == l.Cartao).Id,
                Certeza = CertezaDe(l.Certeza),
                TagId = l.Tag is null ? null : tags.First(t => t.Nome == l.Tag).Id
            })
            .ToList();

        var cfg = new Configuracao
        {
            SaldoAncoraCentavos = cenario.Configuracao.SaldoAncoraCentavos,
            DataAncora = cenario.Configuracao.DataAncora,
            DiarioPrevistoCentavos = cenario.Configuracao.DiarioPrevistoCentavos,
            SaldoConfortoCentavos = cenario.Configuracao.SaldoConfortoCentavos,
            ContarEntradasIncertas = cenario.Configuracao.ContarEntradasIncertas,
            JaGuardadoCentavos = cenario.Configuracao.JaGuardadoCentavos,
            ConfiguracaoConcluida = true
        };

        return new EntradaProjecao
        {
            Configuracao = cfg,
            Cartoes = cartoes,
            Tags = tags,
            Lancamentos = lancamentos,
            DiariosReais = cenario.DiarioReal.ToDictionary(d => d.Data, d => d.ValorCentavos),
            Hoje = cenario.Hoje,
            // O cenario fixa o fim do horizonte em vez de usar os 24 meses relativos a hoje.
            FimHorizonte = cenario.Configuracao.HorizonteAte,
            Simulacao = cenario.Simulacao is null
                ? null
                : new Simulacao
                {
                    Descricao = cenario.Simulacao.Descricao,
                    ValorTotalCentavos = cenario.Simulacao.ValorTotalCentavos,
                    Parcelas = cenario.Simulacao.Parcelas,
                    PrimeiraParcela = cenario.Simulacao.PrimeiraParcela
                }
        };
    }

    public static TipoLancamento TipoDe(string tipo) => tipo switch
    {
        "Entrada" => TipoLancamento.Entrada,
        "Saída" or "Saida" => TipoLancamento.Saida,
        "Cartão" or "Cartao" => TipoLancamento.Cartao,
        "Economia" => TipoLancamento.Economia,
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo desconhecido no cenario")
    };

    public static Certeza? CertezaDe(string? certeza) => certeza switch
    {
        null => null,
        "Certa" => MinhasFinancas.Domain.Certeza.Certa,
        "Incerta" => MinhasFinancas.Domain.Certeza.Incerta,
        _ => throw new ArgumentOutOfRangeException(nameof(certeza), certeza, "Certeza desconhecida")
    };

    public static Farol FarolDe(string farol) => farol switch
    {
        "ok" => Farol.Ok,
        "atencao" => Farol.Atencao,
        "negativo" => Farol.Negativo,
        _ => throw new ArgumentOutOfRangeException(nameof(farol), farol, "Farol desconhecido")
    };

    /// <summary>Le um CSV simples, sem campos com virgula dentro.</summary>
    public static List<Dictionary<string, string>> LerCsv(string nome)
    {
        var linhas = File.ReadAllLines(Caminho(nome))
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        var cabecalho = linhas[0].Split(',');

        return linhas
            .Skip(1)
            .Select(linha =>
            {
                var campos = linha.Split(',');
                return cabecalho
                    .Select((coluna, i) => (coluna, valor: campos[i]))
                    .ToDictionary(par => par.coluna, par => par.valor);
            })
            .ToList();
    }
}

internal sealed record CenarioJson
{
    public DateOnly Hoje { get; init; }
    public ConfiguracaoJson Configuracao { get; init; } = new();
    public List<CartaoJson> Cartoes { get; init; } = [];
    public List<string> Tags { get; init; } = [];
    public List<LancamentoJson> Lancamentos { get; init; } = [];
    public List<DiarioJson> DiarioReal { get; init; } = [];
    public SimulacaoJson? Simulacao { get; init; }
    public EsperadoJson Esperado { get; init; } = new();
}

internal sealed record ConfiguracaoJson
{
    public long SaldoAncoraCentavos { get; init; }
    public DateOnly DataAncora { get; init; }
    public long DiarioPrevistoCentavos { get; init; }
    public long SaldoConfortoCentavos { get; init; }
    public bool ContarEntradasIncertas { get; init; }
    public long JaGuardadoCentavos { get; init; }
    public DateOnly HorizonteAte { get; init; }
}

internal sealed record CartaoJson
{
    public string Nome { get; init; } = "";
    public int DiaFechamento { get; init; }
    public int DiaVencimento { get; init; }
}

internal sealed record LancamentoJson
{
    public DateOnly Data { get; init; }
    public string Descricao { get; init; } = "";
    public string Tipo { get; init; } = "";
    public long ValorCentavos { get; init; }
    public int? Vezes { get; init; }
    public string? Cartao { get; init; }
    public string? Certeza { get; init; }
    public string? Tag { get; init; }

    /// <summary>Valor esperado de R1 e R2, nao entrada do motor.</summary>
    public DateOnly CaiNaContaEm { get; init; }

    /// <summary>Valor esperado: data ou o texto "sem fim".</summary>
    public string UltimaVez { get; init; } = "";
}

internal sealed record DiarioJson
{
    public DateOnly Data { get; init; }
    public long ValorCentavos { get; init; }
}

internal sealed record SimulacaoJson
{
    public string Descricao { get; init; } = "";
    public long ValorTotalCentavos { get; init; }
    public int Parcelas { get; init; }
    public DateOnly PrimeiraParcela { get; init; }
}

internal sealed record EsperadoJson
{
    public CartoesResumoJson CartoesResumo { get; init; } = new();

    [JsonPropertyName("mes_2026_10")]
    public MesJson Mes202610 { get; init; } = new();

    public SimulacaoEsperadaJson Simulacao { get; init; } = new();
}

internal sealed record CartoesResumoJson
{
    public long SaldoHoje { get; init; }
    public long PodeGastarHoje { get; init; }
    public long MenorSaldoAFrente { get; init; }
    public DateOnly DataMenorSaldoAFrente { get; init; }
    public string PrimeiroDiaAbaixoConforto { get; init; } = "";
    public long GuardadoAteHoje { get; init; }
}

internal sealed record MesJson
{
    public long Entradas { get; init; }
    public long Saidas { get; init; }
    public long Cartao { get; init; }
    public long Diario { get; init; }
    public long Economias { get; init; }
    public long Performance { get; init; }
    public long CustoDeVida { get; init; }
    public decimal EconomizadoPct { get; init; }
    public long DiarioMedio { get; init; }
    public int DiasComDiarioReal { get; init; }
    public long EntradasIncertasFora { get; init; }
    public long SaldoFim { get; init; }
    public long MenorSaldo { get; init; }
    public DateOnly DataMenorSaldo { get; init; }
}

internal sealed record SimulacaoEsperadaJson
{
    public long MenorSem { get; init; }
    public DateOnly DataMenorSem { get; init; }
    public long MenorCom { get; init; }
    public DateOnly DataMenorCom { get; init; }
    public string PrimeiroDiaAbaixoConforto { get; init; } = "";
    public long Folga { get; init; }
    public string Veredito { get; init; } = "";
}
