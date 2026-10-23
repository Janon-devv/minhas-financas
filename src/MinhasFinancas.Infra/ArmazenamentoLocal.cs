namespace MinhasFinancas.Infra;

/// <summary>
/// Onde ficam os arquivos do app. Resolvido uma vez na subida, a partir da configuracao, para
/// que banco e copias de seguranca andem juntos: apontar o app para um banco de teste nao pode
/// misturar as copias dele com as do banco de verdade.
/// </summary>
/// <param name="CaminhoBanco">Caminho completo do arquivo SQLite.</param>
/// <param name="PastaBackups">
/// Pasta das copias automaticas e das copias de antes de migrar e de restaurar. Por padrao, a
/// pasta "backups" ao lado do banco; pode apontar para uma pasta sincronizada, como o OneDrive.
/// </param>
public sealed record ArmazenamentoLocal(string CaminhoBanco, string PastaBackups)
{
    public static ArmazenamentoLocal De(string? caminhoBanco, string? pastaBackups)
    {
        var banco = string.IsNullOrWhiteSpace(caminhoBanco)
            ? Infra.CaminhoBanco.Padrao()
            : Path.GetFullPath(caminhoBanco);

        Directory.CreateDirectory(Path.GetDirectoryName(banco)!);

        var backups = string.IsNullOrWhiteSpace(pastaBackups)
            ? Path.Combine(Path.GetDirectoryName(banco)!, "backups")
            : Path.GetFullPath(pastaBackups);

        return new ArmazenamentoLocal(banco, backups);
    }
}
