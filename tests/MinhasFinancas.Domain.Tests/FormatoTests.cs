using MinhasFinancas.Web;
using Xunit;

namespace MinhasFinancas.Domain.Tests;

/// <summary>
/// Leitura do que a pessoa digita num campo de dinheiro. Erra aqui e o valor salvo fica
/// cem vezes maior ou menor sem ninguem perceber na hora.
/// </summary>
public class FormatoTests
{
    [Theory]
    [InlineData("50", 5_000L)]
    [InlineData("50,00", 5_000L)]
    [InlineData("50.00", 5_000L)]
    [InlineData("0,99", 99L)]
    [InlineData("1.234,56", 123_456L)]
    [InlineData("1234.56", 123_456L)]
    [InlineData("R$ 90", 9_000L)]
    [InlineData("R$ 1.300,00", 130_000L)]
    [InlineData("4,5", 450L)]
    [InlineData("1.300", 130_000L)]
    [InlineData("-120,50", -12_050L)]
    [InlineData("0", 0L)]
    public void Le_dinheiro_do_jeito_que_a_pessoa_digita(string texto, long esperado)
    {
        Assert.True(Formato.TentarDinheiro(texto, out var centavos));
        Assert.Equal(esperado, centavos);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData(null)]
    public void Recusa_o_que_nao_e_numero(string? texto)
    {
        Assert.False(Formato.TentarDinheiro(texto, out _));
    }

    [Fact]
    public void Formata_em_real_brasileiro()
    {
        Assert.Equal("R$ 1.234,56", Formato.Moeda(123_456));
        Assert.Equal("1.234,56", Formato.Numero(123_456));
        Assert.Equal("-R$ 50,00", Formato.Moeda(-5_000));
    }

    [Fact]
    public void Ida_e_volta_do_campo_preserva_o_valor()
    {
        foreach (var centavos in new long[] { 0, 1, 99, 100, 123_456, -4_550, 999_999_99 })
        {
            Assert.True(Formato.TentarDinheiro(Formato.ParaCampo(centavos), out var lido));
            Assert.Equal(centavos, lido);
        }
    }

    [Fact]
    public void Datas_vao_e_voltam_no_formato_do_campo()
    {
        var data = new DateOnly(2026, 10, 13);
        Assert.Equal("2026-10-13", Formato.ParaCampo(data));
        Assert.True(Formato.TentarData("2026-10-13", out var lida));
        Assert.Equal(data, lida);
        Assert.Equal("13/10/2026", Formato.Data(data));
    }
}
