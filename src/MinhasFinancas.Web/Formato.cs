using System.Globalization;
using MinhasFinancas.Domain;

namespace MinhasFinancas.Web;

/// <summary>Formatacao e leitura de texto. So a borda da aplicacao mexe com isso.</summary>
public static class Formato
{
    public static readonly CultureInfo Br = new("pt-BR");

    private static readonly string[] Meses =
    [
        "janeiro", "fevereiro", "março", "abril", "maio", "junho",
        "julho", "agosto", "setembro", "outubro", "novembro", "dezembro"
    ];

    private static readonly string[] MesesCurtos =
    [
        "jan", "fev", "mar", "abr", "mai", "jun", "jul", "ago", "set", "out", "nov", "dez"
    ];

    private static readonly string[] DiasSemana = ["dom", "seg", "ter", "qua", "qui", "sex", "sáb"];

    private static readonly string[] DiasSemanaLongos =
        ["domingo", "segunda", "terça", "quarta", "quinta", "sexta", "sábado"];

    // ---------- Dinheiro ----------

    public static string Moeda(long centavos) => (centavos / 100m).ToString("C2", Br);

    public static string Moeda(long? centavos) => centavos is null ? "sem cálculo" : Moeda(centavos.Value);

    /// <summary>
    /// As partes de um valor para a tela poder diminuir os centavos no numero grande:
    /// sinal, parte inteira com milhar e centavos com a virgula. "-R$ 1.234,56" vira
    /// ("-", "1.234", ",56").
    /// </summary>
    public static (string Sinal, string Inteiro, string Centavos) Partes(long centavos)
    {
        var texto = Numero(Math.Abs(centavos));
        var virgula = texto.LastIndexOf(',');
        return (centavos < 0 ? "-" : "", texto[..virgula], texto[virgula..]);
    }

    /// <summary>Sem o R$, para tabelas onde a coluna ja diz que e dinheiro.</summary>
    public static string Numero(long centavos) => (centavos / 100m).ToString("N2", Br);

    public static string Numero(long? centavos) => centavos is null ? "" : Numero(centavos.Value);

    /// <summary>Valor so com milhares, para eixos de grafico.</summary>
    public static string Compacto(long centavos)
    {
        var reais = centavos / 100m;
        return Math.Abs(reais) >= 1000
            ? (reais / 1000m).ToString("N1", Br) + "k"
            : reais.ToString("N0", Br);
    }

    public static string ParaCampo(long centavos) => (centavos / 100m).ToString("N2", Br);

    /// <summary>
    /// Aceita o que a pessoa realmente digita: 50, 50,00, 1.234,56, 1234.56, R$ 90.
    /// Decide o separador decimal pelas casas que vem depois dele.
    /// </summary>
    public static bool TentarDinheiro(string? texto, out long centavos)
    {
        centavos = 0;

        if (string.IsNullOrWhiteSpace(texto))
        {
            return false;
        }

        var limpo = new string(texto.Where(c => char.IsDigit(c) || c is ',' or '.' or '-').ToArray());

        if (limpo.Length == 0)
        {
            return false;
        }

        var negativo = limpo.StartsWith('-');
        limpo = limpo.Replace("-", "");

        var posDecimal = Math.Max(limpo.LastIndexOf(','), limpo.LastIndexOf('.'));
        var inteiros = limpo;
        var decimais = "";

        if (posDecimal >= 0)
        {
            var casas = limpo.Length - posDecimal - 1;

            if (casas is 1 or 2)
            {
                inteiros = limpo[..posDecimal];
                decimais = limpo[(posDecimal + 1)..];
            }
        }

        inteiros = new string(inteiros.Where(char.IsDigit).ToArray());

        if (inteiros.Length == 0)
        {
            inteiros = "0";
        }

        // Dez digitos de reais: ate R$ 9.999.999.999,99, o teto do dominio (Validacao.ValorMaximoCentavos).
        // Mais que isso e zero a mais, e o campo recusar e melhor que gravar um valor mil vezes maior.
        if (inteiros.TrimStart('0').Length > 10)
        {
            return false;
        }

        decimais = (decimais + "00")[..2];

        if (!long.TryParse(inteiros, NumberStyles.None, CultureInfo.InvariantCulture, out var parteInteira) ||
            !long.TryParse(decimais, NumberStyles.None, CultureInfo.InvariantCulture, out var parteDecimal))
        {
            return false;
        }

        centavos = (parteInteira * 100) + parteDecimal;

        if (negativo)
        {
            centavos = -centavos;
        }

        return true;
    }

    // ---------- Datas ----------

    public static string Data(DateOnly data) => data.ToString("dd/MM/yyyy", Br);

    public static string Data(DateOnly? data) => data is null ? "sem data" : Data(data.Value);

    /// <summary>"15 de outubro", sem o ano: para o dia que esta em tela, onde o ano e obvio.</summary>
    public static string DataExtensa(DateOnly data) => $"{data.Day} de {Meses[data.Month - 1]}";

    public static string DiaSemanaLongo(DateOnly data) => DiasSemanaLongos[(int)data.DayOfWeek];

    public static string DataCurta(DateOnly data) => data.ToString("dd/MM", Br);

    public static string ParaCampo(DateOnly data) => data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string ParaCampo(DateOnly? data) => data is null ? "" : ParaCampo(data.Value);

    public static bool TentarData(string? texto, out DateOnly data) =>
        DateOnly.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out data);

    public static string DiaSemana(DateOnly data) => DiasSemana[(int)data.DayOfWeek];

    public static bool FimDeSemana(DateOnly data) =>
        data.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    public static string MesAno(int ano, int mes) => $"{Meses[mes - 1]} de {ano}";

    public static string MesAnoCurto(int ano, int mes) => $"{MesesCurtos[mes - 1]}/{ano}";

    public static string Mes(int mes) => Meses[mes - 1];

    /// <summary>
    /// Igual a Quando, mas devolve null quando a data esta longe e o texto relativo seria
    /// so a propria data repetida. Evita escrever "em 13/10/2028 (13/10/2028)".
    /// </summary>
    public static string? QuandoRelativo(DateOnly data, DateOnly hoje)
    {
        var texto = Quando(data, hoje);
        return texto == Data(data) ? null : texto;
    }

    /// <summary>Texto relativo curto, do tipo "hoje", "amanha", "em 3 dias", "ha 2 dias".</summary>
    public static string Quando(DateOnly data, DateOnly hoje)
    {
        var dias = data.DayNumber - hoje.DayNumber;

        return dias switch
        {
            0 => "hoje",
            1 => "amanhã",
            -1 => "ontem",
            > 1 and < 7 => $"em {dias} dias",
            < -1 and > -7 => $"há {-dias} dias",
            _ => Data(data)
        };
    }

    // ---------- Farol, tipos e vereditos ----------

    public static string ClasseFarol(Farol farol) => farol switch
    {
        Farol.Ok => "ok",
        Farol.Atencao => "atencao",
        _ => "negativo"
    };

    /// <summary>
    /// O farol diz a regra, nao um estado de espirito: "tranquilo" prometia mais do que o
    /// numero sabe, que e so a comparacao com o saldo de conforto.
    /// </summary>
    public static string TextoFarol(Farol farol) => farol switch
    {
        Farol.Ok => "acima do conforto",
        Farol.Atencao => "abaixo do conforto",
        _ => "negativo"
    };

    public static string ClasseValor(long centavos, long conforto) =>
        "valor-" + ClasseFarol(Projetor.FarolDe(centavos, conforto));

    public static string TextoTipo(TipoLancamento tipo) => tipo switch
    {
        TipoLancamento.Entrada => "Entrada",
        TipoLancamento.Saida => "Saída",
        TipoLancamento.Cartao => "Cartão",
        _ => "Economia"
    };

    public static string TextoTipoPlural(TipoLancamento tipo) => tipo switch
    {
        TipoLancamento.Entrada => "Entradas",
        TipoLancamento.Saida => "Saídas",
        TipoLancamento.Cartao => "Cartão",
        _ => "Economias"
    };

    public static string ClasseTipo(TipoLancamento tipo) => tipo switch
    {
        TipoLancamento.Entrada => "entrada",
        TipoLancamento.Saida => "saida",
        TipoLancamento.Cartao => "cartao",
        _ => "economia"
    };

    /// <summary>Nome do icone de cada tipo. Coincide com a classe, mas sao coisas diferentes.</summary>
    public static string IconeTipo(TipoLancamento tipo) => ClasseTipo(tipo);

    /// <summary>
    /// Nomes da paleta de tags, na ordem em que aparecem para escolher.
    /// Os pares de cor estao no app.css; aqui so os nomes, para nao ter hexadecimal solto no C#.
    /// </summary>
    public static readonly string[] CoresDeTag =
        ["rosa", "laranja", "ambar", "verde", "turquesa", "azul", "indigo", "roxo"];

    /// <summary>Classe da pilula de uma tag. Cor desconhecida ou vazia cai na neutra.</summary>
    public static string ClasseTag(string? cor) =>
        !string.IsNullOrEmpty(cor) && CoresDeTag.Contains(cor) ? $"tag-{cor}" : "neutra";

    public static string TextoVeredito(Veredito veredito) => veredito switch
    {
        Veredito.CabeComFolga => "Cabe com folga",
        Veredito.CabeMasAperta => "Cabe, mas aperta",
        _ => "Não cabe"
    };

    public static string ClasseVeredito(Veredito veredito) => veredito switch
    {
        Veredito.CabeComFolga => "bom",
        Veredito.CabeMasAperta => "atencao",
        _ => "ruim"
    };

    public static string Porcento(decimal fracao) => (fracao * 100).ToString("N0", Br) + "%";

    /// <summary>
    /// Como o valor se repete, escrito para ficar ao lado dele: "R$ 89,90 por mês, 4x".
    /// Deixa claro que o numero e de cada ocorrencia, nao o total.
    /// </summary>
    public static string Repeticao(int? vezes, Periodicidade periodicidade)
    {
        var intervalo = periodicidade == Periodicidade.Semanal ? "por semana" : "por mês";

        return vezes switch
        {
            null => intervalo,
            1 => "uma vez",
            var n => $"{intervalo}, {n}x"
        };
    }

    /// <summary>Quantidade com singular e plural: "1 conta", "3 contas".</summary>
    public static string Quantos(int quantos, string singular, string plural) =>
        $"{quantos} {(quantos == 1 ? singular : plural)}";

    public static string TextoSituacaoMeta(SituacaoMeta situacao) => situacao switch
    {
        SituacaoMeta.Concluida => "concluída",
        SituacaoMeta.PrazoVencido => "prazo vencido",
        SituacaoMeta.SemPrazo => "sem prazo",
        _ => "em andamento"
    };
}
