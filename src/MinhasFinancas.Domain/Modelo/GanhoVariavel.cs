namespace MinhasFinancas.Domain.Modelo;

/// <summary>
/// Quanto entrou de ganho variavel num mes (R17). Uma linha por mes.
///
/// Existe porque renda que muda todo mes nao cabe em Lancamento: nao tem valor fixo para repetir,
/// e cadastrar um lancamento novo a cada mes e trabalho que ninguem mantem. Aqui o mes ja tem o
/// campo esperando, e so preencher.
///
/// E registro do que JA CAIU na conta. Por isso nao entra na projecao de saldo: o saldo conferido
/// com o banco ja contem esse dinheiro, e soma-lo de novo mostraria dinheiro que nao existe.
/// Ele conta nos ganhos DO MES (R8), que e outra pergunta: quanto este mes rendeu.
/// </summary>
public class GanhoVariavel
{
    public int Id { get; set; }

    public int Ano { get; set; }

    /// <summary>1 a 12.</summary>
    public int Mes { get; set; }

    /// <summary>Zero e resposta valida: quer dizer "este mes nao rendeu nada".</summary>
    public long ValorCentavos { get; set; }

    /// <summary>De onde veio, quando o dono quiser anotar: "Uber", "Uber + freela".</summary>
    public string? Obs { get; set; }
}
