using Microsoft.Data.Sqlite;
using MinhasFinancas.Domain;

namespace MinhasFinancas.Infra.Servicos;

/// <summary>
/// Copias do arquivo do banco. O financas.db e a unica copia do historico inteiro: sem isto, um
/// disco que falha, um arquivo corrompido ou uma restauracao errada nao tem volta.
/// </summary>
public sealed class CopiaDeSeguranca(ArmazenamentoLocal local, IRelogio relogio)
{
    /// <summary>Quantas copias diarias ficam guardadas. Um mes cobre o erro percebido tarde.</summary>
    public const int CopiasDiariasGuardadas = 30;

    public const string PrefixoDiaria = "diaria-";

    public const string PrefixoAntesDeMigrar = "antes-de-migrar-";

    public const string PrefixoAntesDeRestaurar = "antes-de-restaurar-";

    public string Pasta => local.PastaBackups;

    /// <summary>
    /// Faz a copia do dia, se ainda nao existe. Devolve o caminho da copia criada, ou null quando
    /// nao havia nada a fazer. Chamar varias vezes no mesmo dia e seguro.
    /// </summary>
    public async Task<string?> CopiaDoDiaAsync(CancellationToken cancelamento = default)
    {
        if (!File.Exists(local.CaminhoBanco))
        {
            return null;
        }

        var destino = Path.Combine(local.PastaBackups, $"{PrefixoDiaria}{relogio.Hoje:yyyy-MM-dd}.db");

        if (File.Exists(destino))
        {
            return null;
        }

        await CopiarBancoAsync(local.CaminhoBanco, destino, cancelamento);
        Rodizio(local.PastaBackups, PrefixoDiaria, ".db", CopiasDiariasGuardadas);
        return destino;
    }

    /// <summary>A copia diaria mais recente, para a tela dizer quando foi.</summary>
    public FileInfo? UltimaCopiaDiaria()
    {
        if (!Directory.Exists(local.PastaBackups))
        {
            return null;
        }

        return new DirectoryInfo(local.PastaBackups)
            .EnumerateFiles($"{PrefixoDiaria}*.db")
            .OrderByDescending(f => f.Name, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    /// <summary>
    /// Copia consistente do banco com VACUUM INTO: o SQLite le dentro de uma transacao, entao a
    /// copia sai inteira mesmo com o app aberto gravando. Copiar o arquivo direto pode pegar uma
    /// gravacao pela metade. Escreve num arquivo temporario e so depois renomeia, para uma copia
    /// interrompida nunca parecer uma copia boa.
    /// </summary>
    public static async Task CopiarBancoAsync(string origem, string destino, CancellationToken cancelamento = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destino)!);

        var temporario = destino + ".parcial";
        File.Delete(temporario);

        var conexao = new SqliteConnectionStringBuilder
        {
            DataSource = origem,
            Mode = SqliteOpenMode.ReadOnly,

            // Sem pool, a conexao fecha de verdade no fim e nao segura o arquivo aberto no Windows.
            Pooling = false
        }.ToString();

        await using (var sqlite = new SqliteConnection(conexao))
        {
            await sqlite.OpenAsync(cancelamento);
            await using var comando = sqlite.CreateCommand();
            comando.CommandText = "VACUUM INTO $destino";
            comando.Parameters.AddWithValue("$destino", temporario);
            await comando.ExecuteNonQueryAsync(cancelamento);
        }

        File.Move(temporario, destino, overwrite: true);
    }

    /// <summary>
    /// Apaga as copias mais antigas de um tipo, deixando as <paramref name="manter"/> mais novas.
    /// Os nomes levam a data no formato ano-mes-dia, entao a ordem alfabetica e a cronologica.
    /// </summary>
    public static void Rodizio(string pasta, string prefixo, string extensao, int manter)
    {
        if (!Directory.Exists(pasta))
        {
            return;
        }

        var antigas = new DirectoryInfo(pasta)
            .EnumerateFiles($"{prefixo}*{extensao}")
            .OrderByDescending(f => f.Name, StringComparer.Ordinal)
            .Skip(manter);

        foreach (var arquivo in antigas)
        {
            arquivo.Delete();
        }
    }
}
