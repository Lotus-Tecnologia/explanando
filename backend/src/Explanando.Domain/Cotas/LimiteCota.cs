using Explanando.Domain.Comum;
using Explanando.Domain.Parlamentares;

namespace Explanando.Domain.Cotas;

/// <summary>
/// Teto mensal da Cota para o Exercicio da Atividade Parlamentar, por UF.
///
/// Entidade <b>ausente da especificacao original</b> e sem a qual REG_TX_002 ("gasto cravado
/// no limite") nao tem contra o que comparar. O valor varia por estado — a logica de
/// deslocamento ate Brasilia entra no calculo — e nao e' publicado por nenhuma API: a
/// tabela precisa ser curada e versionada aqui (ver docs/registro/etapa-01-contratos.md).
///
/// Modelada com vigencia porque o teto e' reajustado: uma despesa de 2023 tem de ser
/// comparada ao limite de 2023, nao ao de hoje.
///
/// <para>E modelada com <see cref="Casa"/> porque <b>sao duas cotas diferentes</b>: a CEAP
/// da Camara e a CEAPS do Senado tem valores proprios para a mesma UF. Sem o discriminador,
/// o teto do Acre para deputado e o do Acre para senador colidiriam no indice unico, e a
/// regra compararia o gasto de um senador contra o teto de um deputado.</para>
/// </summary>
public sealed class LimiteCota : Entidade
{
    private LimiteCota()
    {
        // EF Core.
        Uf = string.Empty;
    }

    /// <summary>Qual cota este teto governa: a CEAP da Camara ou a CEAPS do Senado.</summary>
    public TipoParlamentar Casa { get; private set; }

    public string Uf { get; private set; }

    public decimal ValorMensal { get; private set; }

    public DateOnly VigenciaInicio { get; private set; }

    /// <summary>Nulo enquanto for o teto em vigor.</summary>
    public DateOnly? VigenciaFim { get; private set; }

    public static LimiteCota Criar(
        string uf,
        decimal valorMensal,
        DateOnly vigenciaInicio,
        DateOnly? vigenciaFim = null,
        TipoParlamentar casa = TipoParlamentar.DeputadoFederal)
    {
        var normalizada = (uf ?? string.Empty).Trim().ToUpperInvariant();

        if (normalizada.Length != 2)
        {
            throw new ArgumentException(
                $"UF deve ter exatamente 2 letras; recebido '{uf}'.", nameof(uf));
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(valorMensal);

        if (vigenciaFim is not null && vigenciaFim < vigenciaInicio)
        {
            throw new ArgumentException(
                $"Fim de vigencia ({vigenciaFim}) anterior ao inicio ({vigenciaInicio}).",
                nameof(vigenciaFim));
        }

        return new LimiteCota
        {
            Casa = casa,
            Uf = normalizada,
            ValorMensal = valorMensal,
            VigenciaInicio = vigenciaInicio,
            VigenciaFim = vigenciaFim,
        };
    }

    public bool VigenteEm(DateOnly data) =>
        data >= VigenciaInicio && (VigenciaFim is null || data <= VigenciaFim);
}
