using Explanando.Domain.Comum;
using Explanando.Domain.Despesas;
using Explanando.Domain.Fornecedores;
using Explanando.Domain.Parlamentares;

namespace Explanando.Application.Ingestao;

/// <summary>
/// Converte o que a fonte externa diz no que o dominio entende.
///
/// E' aqui que defeito de dado vira (ou deixa de virar) defeito de sistema. Cada decisao
/// abaixo veio de algo observado no contrato real capturado na Etapa 01.
/// </summary>
public static class Tradutor
{
    /// <summary>
    /// Marcador para nota sem numero informado.
    ///
    /// String vazia seria pior: REG_KYB_005 procura numeracao baixa e sequencial, e um
    /// campo vazio silencioso seria lido como "numero muito baixo", gerando falso positivo
    /// exatamente na regra mais delicada do conjunto.
    /// </summary>
    public const string NumeroDocumentoAusente = "(sem numero)";

    public static NovoParlamentar ParaNovoParlamentar(DeputadoDaFonte deputado)
    {
        ArgumentNullException.ThrowIfNull(deputado);

        return new NovoParlamentar
        {
            IdExterno = deputado.IdExterno,
            NomeCivil = deputado.NomeCivil,
            NomeParlamentar = deputado.NomeParlamentar,
            SiglaPartido = deputado.SiglaPartido,
            Uf = deputado.Uf,
            Tipo = TipoParlamentar.DeputadoFederal,
            FotoUrl = deputado.FotoUrl,
        };
    }

    public static NovaDespesa ParaNovaDespesa(DespesaDaFonte despesa, Guid parlamentarId)
    {
        ArgumentNullException.ThrowIfNull(despesa);

        return new NovaDespesa
        {
            ParlamentarId = parlamentarId,
            DocumentoFornecedor = CpfCnpj.Criar(despesa.CnpjCpfFornecedor),
            NomeFornecedor = despesa.NomeFornecedor ?? string.Empty,
            Competencia = CompetenciaDe(despesa),
            TipoDespesa = despesa.TipoDespesa,
            CodigoDocumento = despesa.CodigoDocumento,
            NumeroDocumento = string.IsNullOrWhiteSpace(despesa.NumeroDocumento)
                ? NumeroDocumentoAusente
                : despesa.NumeroDocumento,
            DataDocumento = despesa.DataDocumento,
            ValorDocumento = despesa.ValorDocumento,
            ValorLiquido = despesa.ValorLiquido,
            ValorGlosa = despesa.ValorGlosa,
            UrlDocumento = despesa.UrlDocumento,
            CodigoLote = despesa.CodigoLote,
            Parcela = despesa.Parcela,
        };
    }

    public static NovoFornecedor ParaNovoFornecedor(EmpresaDaFonte empresa)
    {
        ArgumentNullException.ThrowIfNull(empresa);

        return new NovoFornecedor
        {
            Documento = CpfCnpj.Criar(empresa.Cnpj),
            RazaoSocial = empresa.RazaoSocial,
            NomeFantasia = empresa.NomeFantasia,
            CnaePrincipal = empresa.CnaePrincipal,
            CnaesSecundarios = empresa.CnaesSecundarios,
            SituacaoCadastral = ParaSituacaoCadastral(empresa.DescricaoSituacaoCadastral),
            DataSituacaoCadastral = empresa.DataSituacaoCadastral,
            DataInicioAtividade = empresa.DataInicioAtividade,
            OptantePeloMei = empresa.OptantePeloMei,
            Porte = empresa.Porte,
            Endereco = new EnderecoFornecedor
            {
                Logradouro = empresa.Logradouro,
                Numero = empresa.Numero,
                Bairro = empresa.Bairro,
                Municipio = empresa.Municipio,
                CodigoMunicipioIbge = empresa.CodigoMunicipioIbge,
                Uf = empresa.Uf,
                Cep = empresa.Cep,
            },
        };
    }

    /// <summary>
    /// Situacao desconhecida permanece desconhecida.
    ///
    /// Colapsar em "Ativa" faria REG_KYB_002 se calar diante de uma situacao que a Receita
    /// venha a criar; colapsar em "Baixada" acusaria empresa em dia. As duas saidas faceis
    /// estao erradas em direcoes opostas.
    /// </summary>
    public static SituacaoCadastral ParaSituacaoCadastral(string? descricao) =>
        descricao?.Trim().ToUpperInvariant() switch
        {
            "ATIVA" => SituacaoCadastral.Ativa,
            "BAIXADA" => SituacaoCadastral.Baixada,
            "SUSPENSA" => SituacaoCadastral.Suspensa,
            "INAPTA" => SituacaoCadastral.Inapta,
            "NULA" => SituacaoCadastral.Nula,
            _ => SituacaoCadastral.Desconhecida,
        };

    /// <summary>
    /// A API entrega `ano` e `mes` prontos, mas registro antigo pode traze-los zerados.
    /// A data do documento ainda diz de que mes a despesa e' — descartar o registro
    /// perderia gasto publico por causa de um campo de metadado.
    /// </summary>
    private static Competencia CompetenciaDe(DespesaDaFonte despesa) =>
        despesa.Ano >= 1900 && despesa.Mes is >= 1 and <= 12
            ? Competencia.Criar(despesa.Ano, despesa.Mes)
            : Competencia.De(despesa.DataDocumento);
}
