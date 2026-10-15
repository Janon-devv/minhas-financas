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

    // ---------- Dinheiro ----------

    public static string Moeda(long centavos) => (centavos / 100m).ToString("C2", Br);

    public static string Moeda(long? centavos) => centavos is null ? "nao informado" : Moeda(centavos.Value);

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

        if (inteiros.Length > 15)
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

    public static string Data(DateOnly? data) => data is null ? "nao informada" : Data(data.Value);

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

    public static string TextoFarol(Farol farol) => farol switch
    {
        Farol.Ok => "tranquilo",
        Farol.Atencao => "atenção",
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

    public static string ClasseTipo(TipoLancamento tipo) => tipo switch
    {
        TipoLancamento.Entrada => "entrada",
        TipoLancamento.Saida => "saida",
        TipoLancamento.Cartao => "cartao",
        _ => "economia"
    };

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

    public static string TextoVezes(int? vezes) => vezes switch
    {
        null => "todo mês, sem fim",
        1 => "uma vez",
        var n => $"{n} vezes, uma por mês"
    };

    public static string TextoSituacaoMeta(SituacaoMeta situacao) => situacao switch
    {
        SituacaoMeta.Concluida => "concluída",
        SituacaoMeta.PrazoVencido => "prazo vencido",
        SituacaoMeta.SemPrazo => "sem prazo",
        _ => "em andamento"
    };
}
