using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MinhasFinancas.Domain;
using MinhasFinancas.Infra;
using MinhasFinancas.Infra.Servicos;
using MinhasFinancas.Web;
using MinhasFinancas.Web.Components;

// Tudo em portugues do Brasil: formatacao de dinheiro, datas e nomes de mes.
var cultura = new CultureInfo("pt-BR");
CultureInfo.DefaultThreadCurrentCulture = cultura;
CultureInfo.DefaultThreadCurrentUICulture = cultura;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var caminhoBanco = builder.Configuration["MinhasFinancas:CaminhoBanco"];

if (string.IsNullOrWhiteSpace(caminhoBanco))
{
    caminhoBanco = CaminhoBanco.Padrao();
}
else
{
    caminhoBanco = Path.GetFullPath(caminhoBanco);
    Directory.CreateDirectory(Path.GetDirectoryName(caminhoBanco)!);
}

builder.Services.AddDbContextFactory<FinancasDbContext>(opcoes =>
    opcoes.UseSqlite($"Data Source={caminhoBanco}"));

builder.Services.AddSingleton<IRelogio, RelogioSaoPaulo>();
builder.Services.AddSingleton(new InfoApp(caminhoBanco));
builder.Services.AddScoped<PlanejamentoService>();
builder.Services.AddScoped<BackupService>();

var app = builder.Build();

// Cria ou atualiza o banco na subida: o app e de um usuario so e roda local.
using (var escopo = app.Services.CreateScope())
{
    var fabrica = escopo.ServiceProvider.GetRequiredService<IDbContextFactory<FinancasDbContext>>();
    await using var db = await fabrica.CreateDbContextAsync();
    await db.Database.MigrateAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Download do backup como arquivo. Um endpoint resolve melhor que interop no Safari do iPhone.
app.MapGet("/backup/baixar", async (BackupService backup) =>
{
    var json = await backup.ExportarAsync();
    return Results.File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", backup.NomeSugerido());
});

app.Run();
