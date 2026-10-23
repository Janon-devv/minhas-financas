using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MinhasFinancas.Domain;
using MinhasFinancas.Infra;
using MinhasFinancas.Infra.Servicos;
using MinhasFinancas.Web;
using MinhasFinancas.Web.Components;
using MinhasFinancas.Web.Seguranca;

// Tudo em portugues do Brasil: formatacao de dinheiro, datas e nomes de mes.
var cultura = new CultureInfo("pt-BR");
CultureInfo.DefaultThreadCurrentCulture = cultura;
CultureInfo.DefaultThreadCurrentUICulture = cultura;

var builder = WebApplication.CreateBuilder(args);

// O app roda com dotnet run, a partir da pasta de build, tambem fora de Development (o perfil
// "rede" usa Production para nao mostrar detalhes de erro a outros aparelhos). Sem isto, fora de
// Development o blazor.web.js e o CSS dos componentes dao 500. Num app publicado nao faz nada.
builder.WebHost.UseStaticWebAssets();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// MinhasFinancas:PastaBackups e opcional: sem ele, as copias ficam na pasta "backups" ao lado do banco.
var local = ArmazenamentoLocal.De(
    builder.Configuration["MinhasFinancas:CaminhoBanco"],
    builder.Configuration["MinhasFinancas:PastaBackups"]);

builder.Services.AddDbContextFactory<FinancasDbContext>(opcoes =>
    opcoes.UseSqlite($"Data Source={local.CaminhoBanco}"));

builder.Services.AddSingleton<IRelogio, RelogioSaoPaulo>();
builder.Services.AddSingleton(local);
builder.Services.AddSingleton(new InfoApp(local.CaminhoBanco));
builder.Services.AddSingleton<CopiaDeSeguranca>();
builder.Services.AddHostedService<CopiaDiaria>();
builder.Services.AddDataProtection();
builder.Services.AddSingleton<ControleDeAcesso>();
builder.Services.AddScoped<PlanejamentoService>();
builder.Services.AddScoped<BackupService>();

var app = builder.Build();

// Cria ou atualiza o banco na subida: o app e de um usuario so e roda local.
using (var escopo = app.Services.CreateScope())
{
    var fabrica = escopo.ServiceProvider.GetRequiredService<IDbContextFactory<FinancasDbContext>>();
    await using var db = await fabrica.CreateDbContextAsync();

    // Copia o banco antes de migrar. Migracao roda sozinha aqui, sem ninguem confirmar,
    // e algumas delas reconstroem tabela inteira no SQLite. A copia custa um arquivo e
    // uma vez por migracao; perder o historico de lancamentos nao tem desfazer. Se a copia
    // falhar, a excecao impede a migracao: melhor o app nao subir do que migrar sem copia.
    if (File.Exists(local.CaminhoBanco) && (await db.Database.GetPendingMigrationsAsync()).Any())
    {
        var agora = app.Services.GetRequiredService<IRelogio>().Agora;
        var copia = Path.Combine(local.PastaBackups, $"{CopiaDeSeguranca.PrefixoAntesDeMigrar}{agora:yyyyMMdd-HHmmss}.db");
        await CopiaDeSeguranca.CopiarBancoAsync(local.CaminhoBanco, copia);
        CopiaDeSeguranca.Rodizio(local.PastaBackups, CopiaDeSeguranca.PrefixoAntesDeMigrar, ".db", manter: 10);
    }

    await db.Database.MigrateAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

// Antes de tudo que serve tela ou arquivo: cabecalhos, filtro de endereco e PIN para a rede.
app.UseProtecaoDeRede(app.Configuration);

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
// O frame-ancestors do Blazor fica desligado porque a ProtecaoDeRede ja manda um mais restrito
// ('none') em toda resposta; com os dois, a pagina saia com dois cabecalhos CSP.
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode(opcoes => opcoes.ContentSecurityFrameAncestorsPolicy = null);

// Download do backup como arquivo. Um endpoint resolve melhor que interop no Safari do iPhone.
app.MapGet("/backup/baixar", async (BackupService backup) =>
{
    var json = await backup.ExportarAsync();
    return Results.File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", backup.NomeSugerido());
});

app.Run();
