#:project ../src/MinhasFinancas.Infra/MinhasFinancas.Infra.csproj
#:property PublishAot=false
#:property IsAotCompatible=false

// Mostra o que existe dentro de um banco do app, para conferir se ele esta vazio
// ou se tem dados dentro. Nao altera nada.
//
// Uso: dotnet run tools/ver-banco.cs            (banco padrao do app)
//      dotnet run tools/ver-banco.cs -- caminho.db

using Microsoft.EntityFrameworkCore;
using MinhasFinancas.Infra;

var caminho = args.Length > 0 ? args[0] : CaminhoBanco.Padrao();

if (!File.Exists(caminho))
{
    Console.WriteLine($"Nao existe arquivo em {caminho}. O app ainda nao gravou nada.");
    return;
}

var opcoes = new DbContextOptionsBuilder<FinancasDbContext>()
    .UseSqlite($"Data Source={caminho}")
    .Options;

await using var db = new FinancasDbContext(opcoes);

Console.WriteLine($"Banco: {caminho}");
Console.WriteLine($"Tamanho: {new FileInfo(caminho).Length / 1024} KB");
Console.WriteLine();
Console.WriteLine($"Configuracoes: {await db.Configuracoes.CountAsync()}");
Console.WriteLine($"Lancamentos:   {await db.Lancamentos.CountAsync()}");
Console.WriteLine($"Diarios:       {await db.DiariosReais.CountAsync()}");
Console.WriteLine($"Cartoes:       {await db.Cartoes.CountAsync()}");
Console.WriteLine($"Tags:          {await db.Tags.CountAsync()}");
Console.WriteLine($"Metas:         {await db.Metas.CountAsync()}");
Console.WriteLine($"Ancoras:       {await db.Ancoras.CountAsync()}");
Console.WriteLine($"Carteiras:     {await Contar(db.Carteiras)}");
Console.WriteLine($"Faturas:       {await Contar(db.Faturas)}");
Console.WriteLine($"Pagamentos:    {await Contar(db.PagamentosFatura)}");
Console.WriteLine($"Renda variavel:{await Contar(db.GanhosVariaveis),2}");

var cfg = await db.Configuracoes.FirstOrDefaultAsync();

if (cfg is not null)
{
    Console.WriteLine();
    Console.WriteLine($"Saldo de partida: {cfg.SaldoAncoraCentavos} centavos em {cfg.DataAncora}");
    Console.WriteLine($"Diario previsto:  {cfg.DiarioPrevistoCentavos} centavos");
    Console.WriteLine($"Conforto:         {cfg.SaldoConfortoCentavos} centavos");
    Console.WriteLine($"Onboarding feito: {cfg.ConfiguracaoConcluida}");
}

foreach (var lancamento in await db.Lancamentos.OrderBy(l => l.Data).ToListAsync())
{
    Console.WriteLine($"  {lancamento.Data} {lancamento.Tipo,-8} {lancamento.ValorCentavos,10} {lancamento.Descricao}");
}

// Banco de uma versao antiga ainda nao tem as tabelas novas: o app cria na proxima abertura.
static async Task<string> Contar<T>(IQueryable<T> tabela)
{
    try
    {
        return (await tabela.CountAsync()).ToString();
    }
    catch (Microsoft.Data.Sqlite.SqliteException)
    {
        return "tabela ainda nao existe (o app cria ao abrir)";
    }
}
