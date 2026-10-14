namespace MinhasFinancas.Domain;

/// <summary>Os quatro tipos de lancamento do Plano de Contas (CONTEXTO.md secao 5).</summary>
public enum TipoLancamento
{
    Entrada = 0,
    Saida = 1,
    Cartao = 2,
    Economia = 3
}

/// <summary>
/// O que um lancamento de Cartao significa dentro da fatura do ciclo dele (R14).
/// NovoLancamento e 0 para ser o padrao: antes das faturas existirem, todo lancamento de cartao
/// somava na projecao, e qualquer outro padrao mudaria o comportamento de dado ja gravado.
/// </summary>
public enum PapelNaFatura
{
    /// <summary>Soma ao total da fatura.</summary>
    NovoLancamento = 0,

    /// <summary>So descreve o que ja esta dentro do total informado. Nao soma de novo.</summary>
    Detalhamento = 1
}

/// <summary>Que tipo de bolsa de dinheiro a carteira e (R13).</summary>
public enum TipoCarteira
{
    Banco = 0,
    Beneficio = 1
}

/// <summary>
/// De quanto em quanto tempo o lancamento se repete (R2). Mensal e 0 para ser o padrao de todo
/// registro que ja existia: trocar a ordem aqui mudaria o significado dos dados gravados.
/// </summary>
public enum Periodicidade
{
    Mensal = 0,
    Semanal = 1
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
