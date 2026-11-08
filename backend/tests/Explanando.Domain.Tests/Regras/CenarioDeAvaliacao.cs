using Explanando.Domain.Auditoria;
using Explanando.Domain.Comum;
using Explanando.Domain.Cotas;
using Explanando.Domain.Despesas;
using Explanando.Domain.Fornecedores;
using Explanando.Domain.Parlamentares;
using Explanando.Domain.Regras;

namespace Explanando.Domain.Tests.Regras;

/// <summary>
/// Monta contextos de avaliacao a partir de dado literal, para que cada teste de regra diga
/// apenas o que lhe importa e o resto seja um cenario plausivel.
///
/// O deputado base e' do <b>AP</b> e o fornecedor base fica em <b>GO</b>: e' o caso real
/// encontrado na amostra da Etapa 01 (deputado do Amapa abastecendo em Goiania).
/// </summary>
internal sealed class CenarioDeAvaliacao
{
    public const string Combustivel = "COMBUSTÍVEIS E LUBRIFICANTES.";
    public const string Consultoria = "CONSULTORIAS, PESQUISAS E TRABALHOS TÉCNICOS.";
    public const string Alimentacao = "FORNECIMENTO DE ALIMENTAÇÃO DO PARLAMENTAR";
    public const string Telefonia = "TELEFONIA";

    /// <summary>0 = Nota Fiscal, 4 = NF-e, 999 = bilhete aereo (nao e' documento fiscal).</summary>
    public const int NotaFiscalEletronica = 4;
    public const int BilheteAereo = 999;

    private const string CnpjPadrao = "36826246000164";

    private readonly List<Despesa> _despesas = [];
    private readonly List<Fornecedor> _fornecedores = [];
    private readonly List<LimiteCota> _limites = [];
    private List<Eleicoes.DoacaoEleitoral>? _doacoes;

    public Parlamentar Parlamentar { get; private set; } = Parlamentar.Criar(new NovoParlamentar
    {
        IdExterno = 204379,
        NomeCivil = "ACÁCIO DA SILVA FAVACHO NETO",
        NomeParlamentar = "Acácio Favacho",
        SiglaPartido = "MDB",
        Uf = "AP",
        Tipo = TipoParlamentar.DeputadoFederal,
    });

    public ParametrosDoMotor Parametros { get; private set; } = ParametrosDoMotor.Padrao;

    public CenarioDeAvaliacao ParlamentarDe(string uf)
    {
        Parlamentar = Parlamentar.Criar(new NovoParlamentar
        {
            IdExterno = 204379,
            NomeCivil = "ACÁCIO DA SILVA FAVACHO NETO",
            NomeParlamentar = "Acácio Favacho",
            SiglaPartido = "MDB",
            Uf = uf,
            Tipo = TipoParlamentar.DeputadoFederal,
        });

        return this;
    }

    public CenarioDeAvaliacao ComParametros(Func<ParametrosDoMotor, ParametrosDoMotor> ajuste)
    {
        Parametros = ajuste(Parametros);
        return this;
    }

    public CenarioDeAvaliacao ComFornecedor(
        string cnpj = CnpjPadrao,
        string razaoSocial = "AUTO POSTO BOMTEMPO LTDA",
        string? cnaePrincipal = "4731800",
        IReadOnlyList<string>? cnaesSecundarios = null,
        SituacaoCadastral situacao = SituacaoCadastral.Ativa,
        DateOnly? dataSituacao = null,
        DateOnly? dataAbertura = null,
        bool? optantePeloMei = null,
        string uf = "GO",
        DateOnly? opcaoPeloMei = null,
        DateOnly? exclusaoDoMei = null)
    {
        _fornecedores.Add(Fornecedor.Criar(new NovoFornecedor
        {
            Documento = CpfCnpj.Criar(cnpj),
            RazaoSocial = razaoSocial,
            CnaePrincipal = cnaePrincipal,
            CnaesSecundarios = cnaesSecundarios ?? [],
            SituacaoCadastral = situacao,
            DataSituacaoCadastral = dataSituacao ?? new DateOnly(2005, 9, 24),
            DataInicioAtividade = dataAbertura ?? new DateOnly(1991, 7, 23),
            OptantePeloMei = optantePeloMei,
            DataOpcaoPeloMei = opcaoPeloMei,
            DataExclusaoDoMei = exclusaoDoMei,
            Endereco = new EnderecoFornecedor { Uf = uf, Municipio = "GOIANIA" },
        }));

        return this;
    }

    public CenarioDeAvaliacao ComLimiteDeCota(
        string uf,
        decimal valorMensal,
        int ano = 2026,
        TipoParlamentar casa = TipoParlamentar.DeputadoFederal)
    {
        _limites.Add(LimiteCota.Criar(uf, valorMensal, new DateOnly(ano, 1, 1), casa: casa));
        return this;
    }

    public CenarioDeAvaliacao ParlamentarDoSenado(string uf = "AP")
    {
        Parlamentar = Parlamentar.Criar(new NovoParlamentar
        {
            IdExterno = 5672,
            NomeCivil = "SENADOR DE TESTE",
            NomeParlamentar = "Senador de Teste",
            SiglaPartido = "MDB",
            Uf = uf,
            Tipo = TipoParlamentar.Senador,
        });

        return this;
    }

    public CenarioDeAvaliacao ComDespesa(
        decimal valor,
        int ano = 2026,
        int mes = 7,
        int dia = 6,
        string tipo = Combustivel,
        string cnpj = CnpjPadrao,
        string numeroDocumento = "8887",
        int codigoTipoDocumento = NotaFiscalEletronica,
        string? codigoDocumento = null)
    {
        var codigo = codigoDocumento ?? $"doc-{_despesas.Count + 1}";

        _despesas.Add(Despesa.Criar(new NovaDespesa
        {
            ParlamentarId = Parlamentar.Id,
            DocumentoFornecedor = CpfCnpj.Criar(cnpj),
            NomeFornecedor = "AUTO POSTO BOMTEMPO LTDA",
            Competencia = Competencia.Criar(ano, mes),
            TipoDespesa = tipo,
            CodigoDocumento = codigo,
            NumeroDocumento = numeroDocumento,
            CodigoTipoDocumento = codigoTipoDocumento,
            DataDocumento = new DateTimeOffset(ano, mes, dia, 0, 0, 0, TimeSpan.Zero),
            ValorDocumento = valor,
            ValorLiquido = valor,
        }));

        return this;
    }

    /// <summary>Atalho para gerar uma serie mensal do mesmo tipo e fornecedor.</summary>
    public CenarioDeAvaliacao ComSerieMensal(
        decimal valorPorMes,
        int primeiroMes,
        int quantidadeDeMeses,
        string tipo = Combustivel,
        string cnpj = CnpjPadrao,
        int ano = 2026)
    {
        for (var i = 0; i < quantidadeDeMeses; i++)
        {
            ComDespesa(valorPorMes, ano, primeiroMes + i, tipo: tipo, cnpj: cnpj);
        }

        return this;
    }

    /// <summary>Sócio no último fornecedor acrescentado.</summary>
    public CenarioDeAvaliacao ComSocio(string nome, string cpfMascarado, string qualificacao = "Sócio-Administrador")
    {
        _fornecedores[^1].RegistrarSocio(nome, cpfMascarado, qualificacao);
        return this;
    }

    /// <summary>A campanha do parlamentar existe na base do TSE, com ou sem doações.</summary>
    public CenarioDeAvaliacao ComCampanha()
    {
        _doacoes ??= [];
        return this;
    }

    /// <summary>Uma doação de pessoa física à campanha deste parlamentar.</summary>
    public CenarioDeAvaliacao ComDoacao(string nomeDoDoador, string cpfCompleto, decimal valor, int ano = 2022)
    {
        ComCampanha();
        _doacoes!.Add(Eleicoes.DoacaoEleitoral.Criar(
            ano, _doacoes.Count + 1, Eleicoes.DestinoDaDoacao.Candidato, 1, Parlamentar.Id,
            nomeDoDoador, cpfCompleto, "ACACIO DA SILVA FAVACHO NETO", "MDB", Parlamentar.Uf,
            "Deputado Federal", new DateOnly(ano, 9, 15), valor));
        return this;
    }

    public ContextoDeAvaliacao Construir() =>
        new(Parlamentar, _despesas, _fornecedores, _limites, Parametros, _doacoes);

    /// <summary>Roda uma regra isolada e devolve o que ela emitiu.</summary>
    public IReadOnlyList<FlagAnomalia> Rodar(IRegraAnomalia regra) =>
        regra.Avaliar(Construir()).ToList();
}
