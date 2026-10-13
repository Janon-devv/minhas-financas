namespace MinhasFinancas.Web;

/// <summary>Onde o banco esta, para a tela de ajustes poder dizer isso ao usuario.</summary>
public sealed record InfoApp(string CaminhoBanco)
{
    public string Versao => "0.1";
}
