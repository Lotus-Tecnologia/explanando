using Explanando.Domain.Auditoria;
using Explanando.Domain.Regras;

namespace Explanando.Application.Consultas;

/// <summary>Uma linha do agrupamento de achados por regra e gravidade, como o banco devolve.</summary>
public sealed record AchadosAgrupados(string Codigo, Gravidade Gravidade, int Quantidade, decimal Peso);

/// <summary>
/// Monta a decomposição do índice a partir dos achados agrupados.
///
/// <para>Fica aqui, e não na consulta, porque é aritmética que precisa de teste sem banco: a
/// promessa da Fase 1 é que as fatias somem 100% e as parcelas somem o índice publicado.</para>
/// </summary>
public static class ComposicaoDoIndice
{
    public static IReadOnlyList<ParcelaDoIndice> Montar(
        IEnumerable<AchadosAgrupados> achados,
        int despesasAvaliadas)
    {
        ArgumentNullException.ThrowIfNull(achados);

        var porRegra = achados
            .GroupBy(linha => linha.Codigo, StringComparer.Ordinal)
            .Select(grupo => new
            {
                Codigo = grupo.Key,
                Quantidade = grupo.Sum(linha => linha.Quantidade),
                Peso = grupo.Sum(linha => linha.Peso),
                Altas = Contar(grupo, Gravidade.Alta),
                Medias = Contar(grupo, Gravidade.Media),
                Baixas = Contar(grupo, Gravidade.Baixa),
            })
            .ToList();

        var pesoTotal = porRegra.Sum(regra => regra.Peso);

        return [.. porRegra
            .OrderByDescending(regra => regra.Peso)
            .ThenBy(regra => regra.Codigo, StringComparer.Ordinal)
            .Select(regra => new ParcelaDoIndice
            {
                Codigo = regra.Codigo,
                Descricao = CatalogoDeRegras.DescricaoDe(regra.Codigo),
                Quantidade = regra.Quantidade,
                Peso = regra.Peso,

                // Sem peso não há fatia: dividir por zero aqui publicaria NaN na página.
                Fatia = pesoTotal == 0m ? 0m : regra.Peso / pesoTotal,

                // Mesma conta do ScoreRiscoParlamentar, parcela a parcela.
                PontosNoIndice = despesasAvaliadas == 0
                    ? 0m
                    : regra.Peso / despesasAvaliadas * 100m,

                Altas = regra.Altas,
                Medias = regra.Medias,
                Baixas = regra.Baixas,
            })];

        static int Contar(IEnumerable<AchadosAgrupados> grupo, Gravidade gravidade) =>
            grupo.Where(linha => linha.Gravidade == gravidade).Sum(linha => linha.Quantidade);
    }
}
