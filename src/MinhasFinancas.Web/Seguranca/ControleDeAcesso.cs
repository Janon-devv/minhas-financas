using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using MinhasFinancas.Infra;

namespace MinhasFinancas.Web.Seguranca;

public enum ResultadoEntrada
{
    Ok,
    PinErrado,
    Bloqueado,
    SemPin
}

/// <summary>
/// PIN para entrar pela rede. O computador onde o app roda entra direto; qualquer outro aparelho
/// (o iPhone no Wi-Fi de casa, ou alguem na mesma rede) precisa do PIN. Sem PIN definido, o app
/// nao abre fora do computador: seguro por padrao, e quem quer o celular define um em Ajustes.
///
/// O PIN fica num arquivo ao lado do banco, guardado como hash PBKDF2, e nao entra nos backups.
/// Quem entra recebe um cookie protegido pelo Data Protection do ASP.NET, que vale 90 dias e
/// carrega um carimbo: trocar ou remover o PIN troca o carimbo e derruba todos os aparelhos.
/// </summary>
public sealed class ControleDeAcesso
{
    public const string NomeCookie = "mf_acesso";

    public const int TamanhoMinimo = 6;

    public const int TamanhoMaximo = 12;

    public static readonly TimeSpan DuracaoDoAcesso = TimeSpan.FromDays(90);

    /// <summary>Erros seguidos antes do primeiro bloqueio. Cada nova leva dobra a espera.</summary>
    private const int TentativasPorLeva = 5;

    private const int Iteracoes = 210_000;

    private readonly string _arquivo;
    private readonly IDataProtector _protetor;
    private readonly Lock _trava = new();
    private readonly ConcurrentDictionary<string, Tentativas> _tentativas = new();
    private Registro? _registro;

    private sealed record Registro(string Sal, string Hash, string Carimbo);

    private sealed record Tentativas(int Erros, DateTimeOffset LiberadoEm);

    public ControleDeAcesso(ArmazenamentoLocal local, IDataProtectionProvider protecao)
    {
        _arquivo = Path.Combine(Path.GetDirectoryName(local.CaminhoBanco)!, "acesso.json");
        _protetor = protecao.CreateProtector("MinhasFinancas.AcessoPelaRede.v1");
        _registro = Ler();
    }

    public bool TemPin => _registro is not null;

    /// <summary>So numeros: no iPhone o campo abre o teclado numerico, e seis digitos bastam com o bloqueio.</summary>
    public static string? ConferirPin(string? pin) =>
        pin is not null && pin.Length is >= TamanhoMinimo and <= TamanhoMaximo && pin.All(char.IsAsciiDigit)
            ? null
            : $"O PIN precisa ter de {TamanhoMinimo} a {TamanhoMaximo} números.";

    /// <summary>Define ou troca o PIN. Devolve o motivo quando recusa.</summary>
    public string? DefinirPin(string? pin)
    {
        if (ConferirPin(pin) is string recusa)
        {
            return recusa;
        }

        var sal = RandomNumberGenerator.GetBytes(16);
        var registro = new Registro(
            Convert.ToBase64String(sal),
            Convert.ToBase64String(Derivar(pin!, sal)),
            Convert.ToHexString(RandomNumberGenerator.GetBytes(16)));

        lock (_trava)
        {
            var temporario = _arquivo + ".novo";
            File.WriteAllText(temporario, JsonSerializer.Serialize(registro));
            File.Move(temporario, _arquivo, overwrite: true);
            _registro = registro;
        }

        _tentativas.Clear();
        return null;
    }

    /// <summary>Sem PIN o app volta a abrir so neste computador.</summary>
    public void RemoverPin()
    {
        lock (_trava)
        {
            File.Delete(_arquivo);
            _registro = null;
        }
    }

    /// <param name="origem">Endereco de quem tenta, para o bloqueio valer por aparelho.</param>
    /// <param name="espera">Quanto falta para poder tentar de novo, quando bloqueado.</param>
    public ResultadoEntrada Entrar(string? pin, string origem, out TimeSpan espera)
    {
        espera = TimeSpan.Zero;
        var registro = _registro;

        if (registro is null)
        {
            return ResultadoEntrada.SemPin;
        }

        var agora = DateTimeOffset.UtcNow;
        _tentativas.TryGetValue(origem, out var anteriores);

        if (anteriores is not null && anteriores.LiberadoEm > agora)
        {
            espera = anteriores.LiberadoEm - agora;
            return ResultadoEntrada.Bloqueado;
        }

        var certo = pin is not null && CryptographicOperations.FixedTimeEquals(
            Derivar(pin, Convert.FromBase64String(registro.Sal)),
            Convert.FromBase64String(registro.Hash));

        if (certo)
        {
            _tentativas.TryRemove(origem, out _);
            return ResultadoEntrada.Ok;
        }

        // 5 erros: 1 minuto. Mais 5: 2 minutos. Depois 4, 8... ate 1 hora.
        var erros = (anteriores?.Erros ?? 0) + 1;
        var liberadoEm = agora;

        if (erros % TentativasPorLeva == 0)
        {
            var leva = Math.Min(erros / TentativasPorLeva - 1, 6);
            liberadoEm = agora + TimeSpan.FromMinutes(Math.Min(60, 1 << leva));
        }

        _tentativas[origem] = new Tentativas(erros, liberadoEm);
        return ResultadoEntrada.PinErrado;
    }

    public string EmitirCookie() =>
        _protetor.Protect($"{_registro?.Carimbo}|{(DateTimeOffset.UtcNow + DuracaoDoAcesso).ToUnixTimeSeconds()}");

    /// <summary>Cookie de um PIN que ainda vale, dentro do prazo. Qualquer adulteracao invalida.</summary>
    public bool CookieValido(string? valor)
    {
        var registro = _registro;

        if (registro is null || string.IsNullOrEmpty(valor))
        {
            return false;
        }

        try
        {
            var partes = _protetor.Unprotect(valor).Split('|');

            return partes.Length == 2 &&
                   partes[0] == registro.Carimbo &&
                   long.TryParse(partes[1], out var expira) &&
                   DateTimeOffset.FromUnixTimeSeconds(expira) > DateTimeOffset.UtcNow;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    private static byte[] Derivar(string pin, byte[] sal) =>
        Rfc2898DeriveBytes.Pbkdf2(pin, sal, Iteracoes, HashAlgorithmName.SHA256, 32);

    private Registro? Ler()
    {
        try
        {
            return File.Exists(_arquivo) ? JsonSerializer.Deserialize<Registro>(File.ReadAllText(_arquivo)) : null;
        }
        catch (JsonException)
        {
            // Arquivo estragado vale como sem PIN: o app fecha para a rede, que e o lado seguro.
            return null;
        }
    }
}
