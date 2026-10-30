using System.Net;
using System.Text.Encodings.Web;

namespace MinhasFinancas.Web.Seguranca;

/// <summary>
/// A porta de entrada de toda requisicao, antes de qualquer tela ou arquivo:
/// <list type="number">
/// <item>Cabecalhos que impedem outro site de abrir o app dentro de um quadro.</item>
/// <item>Filtro de endereco: so aceita localhost, IP e o nome deste computador. Bloqueia o
/// DNS rebinding, em que um site malicioso aberto no navegador do PC aponta o proprio dominio
/// para 127.0.0.1 e le o app (inclusive o backup completo) como se fosse dele.</item>
/// <item>PIN para quem nao e este computador (ver <see cref="ControleDeAcesso"/>).</item>
/// </list>
/// </summary>
public static class ProtecaoDeRede
{
    public const string CaminhoEntrar = "/entrar";

    /// <summary>
    /// O que a tela de entrada e o atalho do iPhone precisam antes do PIN. Nada aqui tem dado.
    /// </summary>
    private static readonly HashSet<string> LiberadosSemPin = new(StringComparer.OrdinalIgnoreCase)
    {
        "/app.css", "/icone-180.png", "/icone-512.png", "/manifest.json"
    };

    public static IApplicationBuilder UseProtecaoDeRede(this IApplicationBuilder app, IConfiguration configuracao)
    {
        // Nomes extras aceitos, separados por ponto e virgula. Ex.: o nome do PC no Tailscale.
        var hostsExtras = (configuracao["MinhasFinancas:HostsPermitidos"] ?? "")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return app.Use(async (contexto, proximo) =>
        {
            var cabecalhos = contexto.Response.Headers;
            cabecalhos.XFrameOptions = "DENY";
            cabecalhos.ContentSecurityPolicy = "frame-ancestors 'none'";
            cabecalhos.XContentTypeOptions = "nosniff";
            cabecalhos["Referrer-Policy"] = "no-referrer";

            if (!HostPermitido(contexto.Request.Host.Host, hostsExtras))
            {
                contexto.Response.StatusCode = StatusCodes.Status400BadRequest;
                contexto.Response.ContentType = "text/plain; charset=utf-8";
                await contexto.Response.WriteAsync("Endereço não reconhecido. Abra pelo IP do computador ou por localhost.");
                return;
            }

            var caminho = contexto.Request.Path.Value ?? "/";

            if (DoProprioComputador(contexto.Connection.RemoteIpAddress))
            {
                // Este computador nunca precisa de PIN; a tela de entrada aqui so levaria a um 404.
                if (caminho.Equals(CaminhoEntrar, StringComparison.OrdinalIgnoreCase))
                {
                    contexto.Response.Redirect("/");
                    return;
                }

                await proximo();
                return;
            }

            var acesso = contexto.RequestServices.GetRequiredService<ControleDeAcesso>();

            if (caminho.Equals(CaminhoEntrar, StringComparison.OrdinalIgnoreCase))
            {
                await TelaDeEntradaAsync(contexto, acesso);
                return;
            }

            if (LiberadosSemPin.Contains(caminho) ||
                acesso.CookieValido(contexto.Request.Cookies[ControleDeAcesso.NomeCookie]))
            {
                await proximo();
                return;
            }

            // Pagina aberta no navegador vai para a tela de entrada; o resto (WebSocket do Blazor,
            // download do backup chamado direto) so recebe a recusa.
            if (HttpMethods.IsGet(contexto.Request.Method) &&
                contexto.Request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase))
            {
                contexto.Response.Redirect(CaminhoEntrar);
                return;
            }

            contexto.Response.StatusCode = StatusCodes.Status401Unauthorized;
        });
    }

    public static bool HostPermitido(string? host, IReadOnlyCollection<string> extras)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return false;
        }

        host = host.Trim('[', ']');

        if (IPAddress.TryParse(host, out _) || host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var maquina = Environment.MachineName;

        return new[] { maquina, $"{maquina}.local", $"{maquina}.lan", $"{maquina}.home" }
            .Concat(extras)
            .Any(permitido => host.Equals(permitido, StringComparison.OrdinalIgnoreCase));
    }

    private static bool DoProprioComputador(IPAddress? endereco)
    {
        if (endereco is null)
        {
            return false;
        }

        if (endereco.IsIPv4MappedToIPv6)
        {
            endereco = endereco.MapToIPv4();
        }

        return IPAddress.IsLoopback(endereco);
    }

    private static async Task TelaDeEntradaAsync(HttpContext contexto, ControleDeAcesso acesso)
    {
        if (!acesso.TemPin)
        {
            contexto.Response.StatusCode = StatusCodes.Status403Forbidden;
            await EscreverAsync(contexto, "Acesso pela rede desligado",
                """
                <p class="dica" style="margin:0">Para abrir aqui, defina um PIN em <strong>Ajustes</strong>,
                no computador onde o app roda.</p>
                """);
            return;
        }

        string? erro = null;

        if (HttpMethods.IsPost(contexto.Request.Method) && contexto.Request.HasFormContentType)
        {
            var formulario = await contexto.Request.ReadFormAsync();
            var origem = contexto.Connection.RemoteIpAddress?.ToString() ?? "?";

            switch (acesso.Entrar(formulario["pin"], origem, out var espera))
            {
                case ResultadoEntrada.Ok:
                    contexto.Response.Cookies.Append(ControleDeAcesso.NomeCookie, acesso.EmitirCookie(), new CookieOptions
                    {
                        HttpOnly = true,
                        SameSite = SameSiteMode.Strict,
                        Secure = contexto.Request.IsHttps,
                        IsEssential = true,
                        Expires = DateTimeOffset.UtcNow + ControleDeAcesso.DuracaoDoAcesso
                    });
                    contexto.Response.Redirect("/");
                    return;

                case ResultadoEntrada.Bloqueado:
                    contexto.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    erro = $"Muitas tentativas. Tente de novo em {Math.Max(1, (int)Math.Ceiling(espera.TotalMinutes))} min.";
                    break;

                default:
                    contexto.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    erro = "PIN errado.";
                    break;
            }
        }

        var aviso = erro is null
            ? ""
            : $"""<div class="aviso ruim" role="alert"><div class="aviso-texto">{HtmlEncoder.Default.Encode(erro)}</div></div>""";

        await EscreverAsync(contexto, "Entrar",
            $"""
            <form method="post" action="{CaminhoEntrar}">
                <div class="campo-grupo">
                    <label class="rotulo-texto" for="pin">PIN</label>
                    <input id="pin" name="pin" class="campo" type="password" inputmode="numeric"
                           autocomplete="current-password" maxlength="{ControleDeAcesso.TamanhoMaximo}" required autofocus />
                </div>
                {aviso}
                <div class="acoes"><button type="submit" class="btn">Entrar</button></div>
            </form>
            <p class="dica">O PIN é definido em Ajustes, no computador onde o app roda.</p>
            """);
    }

    /// <summary>Pagina simples, fora do Blazor, com o cabecalho e os estilos do app.</summary>
    private static async Task EscreverAsync(HttpContext contexto, string titulo, string corpo)
    {
        contexto.Response.ContentType = "text/html; charset=utf-8";
        contexto.Response.Headers.CacheControl = "no-store";

        await contexto.Response.WriteAsync(
            $"""
            <!DOCTYPE html>
            <html lang="pt-BR">
            <head>
                <meta charset="utf-8" />
                <meta name="viewport" content="width=device-width, initial-scale=1.0, viewport-fit=cover" />
                <meta name="theme-color" content="#134E4A" />
                <meta name="apple-mobile-web-app-capable" content="yes" />
                <meta name="apple-mobile-web-app-status-bar-style" content="black-translucent" />
                <meta name="apple-mobile-web-app-title" content="Finanças" />
                <link rel="apple-touch-icon" href="/icone-180.png" />
                <link rel="manifest" href="/manifest.json" />
                <link rel="stylesheet" href="/app.css" />
                <title>{titulo} | Minhas Finanças</title>
            </head>
            <body>
                <main class="conteudo">
                    <div class="pagina">
                        <header class="cabecalho">
                            <div class="cabecalho-topo">
                                <div class="cabecalho-titulos">
                                    <span class="marca">Minhas Finanças</span>
                                    <h1>{titulo}</h1>
                                </div>
                            </div>
                        </header>
                        <div class="grupo"><div class="corpo">
                            {corpo}
                        </div></div>
                    </div>
                </main>
            </body>
            </html>
            """);
    }
}
