namespace MinhasFinancas.Domain;

/// <summary>Os quatro tipos de lancamento do Plano de Contas (CONTEXTO.md secao 5).</summary>
public enum TipoLancamento
{
    Entrada = 0,
    Saida = 1,
    Cartao = 2,
    Economia = 3
}

/// <summary>Só se aplica a Entrada. Entrada Incerta fica fora da projecao por padrao (R3).</summary>
public enum Certeza
{
    Certa = 0,
    Incerta = 1
}

/// <summary>Situacao de um saldo (R6).</summary>
public enum Farol
{
    Ok = 0,
    Atencao = 1,
    Negativo = 2
}

/// <summary>Veredito do simulador de compra (R11).</summary>
public enum Veredito
{
    CabeComFolga = 0,
    CabeMasAperta = 1,
    NaoCabe = 2
}
