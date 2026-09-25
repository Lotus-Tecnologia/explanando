using System.Globalization;
using Explanando.Domain.Auditoria;
using Explanando.Domain.Comum;

namespace Explanando.Domain.Regras;

/// <summary>
/// REG_NET_001 — um sócio da empresa paga pela cota doou para a campanha do parlamentar.
///
/// <para>É a regra que o motor tinha na especificação desde o começo e que esperava a carga do
/// TSE. O cruzamento é <b>compatibilidade, não identidade</b>: nome idêntico (normalizado) e os
/// seis dígitos do CPF que a Receita publica. Os outros cinco ficam ocultos dos dois lados, e é
/// por isso que a confiança para em <see cref="CpfParcial.GrauDeConfiancaMaximo"/> e a frase diz
/// "compatível".</para>
///
/// <para><b>Uma flag por fornecedor, e não por despesa.</b> Uma empresa paga todo mês geraria
/// quarenta achados do mesmo fato, e o índice mediria a frequência do pagamento, não o
/// cruzamento. A flag vai na primeira despesa daquela empresa — a mesma escolha de
/// REG_KYB_005.</para>
///
/// <para>Doar é lícito, e contratar a empresa de quem doou também. A regra não conclui nada
/// sobre isso: ela põe as duas pontas, que estão em cadastros públicos diferentes, na mesma
/// página.</para>
/// </summary>
public sealed class RegraDoacaoCruzada : IRegraAnomalia
{
    public string Codigo => "REG_NET_001";

    public string Descricao => "Sócio do fornecedor compatível com doador da campanha do parlamentar.";

    /// <summary>
    /// Sem campanha ligada — senador eleito em 2018, parlamentar que o TSE não identificou —
    /// a regra não tem com o que cruzar. E sem quadro societário com CPF, também não.
    /// </summary>
    public DadoExigido Exige =>
        DadoExigido.CadastroDoFornecedor
        | DadoExigido.QuadroSocietarioDoFornecedor
        | DadoExigido.CampanhaDoParlamentar;

    public IEnumerable<FlagAnomalia> Avaliar(ContextoDeAvaliacao contexto)
    {
        ArgumentNullException.ThrowIfNull(contexto);

        if (contexto.DoacoesDaCampanha is not { Count: > 0 } doacoes)
        {
            yield break;
        }

        var porFornecedor = contexto.Despesas
            .Where(despesa => !despesa.EhEstorno && contexto.FornecedorDe(despesa) is not null)
            .GroupBy(despesa => despesa.DocumentoFornecedor.Numero, StringComparer.Ordinal);

        foreach (var grupo in porFornecedor)
        {
            var fornecedor = contexto.FornecedorDe(grupo.First())!;

            var casamentos = fornecedor.QuadroSocietario
                .Where(socio => socio.Cpf.TemDigitos)
                .Select(socio => (
                    Socio: socio,
                    Doacoes: doacoes.Where(doacao => doacao.VeioDe(socio.NomeSocio, socio.Cpf)).ToList()))
                .Where(par => par.Doacoes.Count > 0)
                .ToList();

            if (casamentos.Count == 0)
            {
                continue;
            }

            var primeira = grupo.MinBy(despesa => despesa.DataDocumento)!;
            var pagoPelaCota = grupo.Sum(despesa => despesa.ValorLiquido);

            var quem = string.Join("; ", casamentos.Select(par =>
                $"'{par.Socio.NomeSocio}' doou "
                + par.Doacoes.Sum(d => d.Valor).ToString("C", CulturaBrasileira.Padrao)
                + $" à campanha de {par.Doacoes[0].AnoDaEleicao}"));

            yield return FlagAnomalia.Criar(
                primeira.Id,
                Codigo,
                Gravidade.Media,
                $"Doação de campanha e fornecedor em comum: {quem}. O nome é idêntico e o CPF é "
                + $"compatível com o de um sócio de '{fornecedor.RazaoSocial}', que recebeu "
                + pagoPelaCota.ToString("C", CulturaBrasileira.Padrao)
                + $" da cota deste parlamentar em {grupo.Count().ToString(CultureInfo.InvariantCulture)} "
                + (grupo.Count() == 1 ? "despesa" : "despesas")
                + ". A compatibilidade usa os seis dígitos de CPF que a Receita publica.",
                grauDeConfianca: CpfParcial.GrauDeConfiancaMaximo);
        }
    }
}
