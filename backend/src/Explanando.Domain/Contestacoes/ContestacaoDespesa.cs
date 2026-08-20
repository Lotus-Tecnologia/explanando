using Explanando.Domain.Comum;

namespace Explanando.Domain.Contestacoes;

/// <summary>Situação de uma contestação enviada pelo gabinete.</summary>
public enum SituacaoDaContestacao
{
    /// <summary>Recebida e publicada. O produto não modera conteúdo de resposta oficial.</summary>
    Publicada = 1,

    /// <summary>Retirada pelo próprio gabinete.</summary>
    Retirada = 2,
}

/// <summary>
/// Resposta oficial de um gabinete a uma despesa sinalizada — o direito de resposta previsto
/// no documento de Requisitos de Negócio, §2.
///
/// <para><b>É publicada sem moderação de mérito.</b> O produto não julga se a justificativa é
/// boa; ele garante que ela apareça ao lado do achado. Moderar o conteúdo transformaria a
/// plataforma em árbitro da disputa, que é exatamente o papel que o
/// <c>ADR-0004</c> evita.</para>
///
/// <para>A autoria é verificada por posse do e-mail institucional do gabinete, publicado pela
/// própria API da Câmara. Validar apenas o domínio <c>@camara.leg.br</c> deixaria qualquer
/// assessoria responder pela página de qualquer deputado.</para>
/// </summary>
public sealed class ContestacaoDespesa : Entidade
{
    private ContestacaoDespesa()
    {
        // EF Core.
        Texto = string.Empty;
        EmailDoGabinete = string.Empty;
    }

    public Guid DespesaId { get; private set; }

    /// <summary>Nulo quando a contestação responde à despesa como um todo, não a uma flag.</summary>
    public Guid? FlagAnomaliaId { get; private set; }

    public Guid ParlamentarId { get; private set; }

    /// <summary>O e-mail institucional que comprovou a posse. Fica registrado para auditoria.</summary>
    public string EmailDoGabinete { get; private set; }

    public string Texto { get; private set; }

    /// <summary>Anexo opcional (recibo adicional, por exemplo), guardado no R2.</summary>
    public string? UrlDoAnexo { get; private set; }

    public SituacaoDaContestacao Situacao { get; private set; }

    public DateTimeOffset EnviadaEm { get; private set; }

    public DateTimeOffset? RetiradaEm { get; private set; }

    public bool EstaVisivel => Situacao == SituacaoDaContestacao.Publicada;

    /// <summary>Tamanho máximo do texto. Resposta é justificativa, não peça processual.</summary>
    public const int TamanhoMaximoDoTexto = 5_000;

    public static ContestacaoDespesa Criar(
        Guid despesaId,
        Guid parlamentarId,
        string emailDoGabinete,
        string texto,
        DateTimeOffset enviadaEm,
        Guid? flagAnomaliaId = null,
        string? urlDoAnexo = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(emailDoGabinete);
        ArgumentException.ThrowIfNullOrWhiteSpace(texto);

        if (texto.Length > TamanhoMaximoDoTexto)
        {
            throw new ArgumentException(
                $"Texto da contestacao tem {texto.Length} caracteres; o limite e' "
                + $"{TamanhoMaximoDoTexto}.",
                nameof(texto));
        }

        // Deliberadamente NAO passa pelo VocabularioDeCompliance: aquele filtro existe para
        // conter o que o PRODUTO afirma. O gabinete escreve o que quiser em sua defesa, e
        // censurar a resposta oficial seria pior que qualquer palavra que ela contenha.
        return new ContestacaoDespesa
        {
            DespesaId = despesaId,
            ParlamentarId = parlamentarId,
            FlagAnomaliaId = flagAnomaliaId,
            EmailDoGabinete = emailDoGabinete.Trim().ToLowerInvariant(),
            Texto = texto.Trim(),
            UrlDoAnexo = urlDoAnexo,
            Situacao = SituacaoDaContestacao.Publicada,
            EnviadaEm = enviadaEm,
        };
    }

    /// <summary>
    /// O gabinete retira a própria resposta. Não apaga: mantém o registro de que existiu,
    /// porque a página pode ter sido citada enquanto estava no ar.
    /// </summary>
    public void Retirar(DateTimeOffset momento)
    {
        if (Situacao == SituacaoDaContestacao.Retirada)
        {
            return;
        }

        Situacao = SituacaoDaContestacao.Retirada;
        RetiradaEm = momento;
    }
}
