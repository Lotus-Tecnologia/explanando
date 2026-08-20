using Explanando.Domain.Comum;

namespace Explanando.Domain.Usuarios;

/// <summary>Papel do usuário na plataforma.</summary>
public enum PerfilDeUsuario
{
    /// <summary>Cidadão: favorita parlamentares, recebe alertas, paga varredura.</summary>
    Cidadao = 1,

    /// <summary>Gabinete: acesso à área de contestação, verificado por posse de e-mail.</summary>
    Gabinete = 2,

    /// <summary>Assinante Pro: exportação em massa e alertas customizados.</summary>
    Pro = 3,
}

/// <summary>
/// Conta de usuário.
///
/// <para>Guarda o mínimo: e-mail e hash de senha. Nada de nome, telefone ou documento — o
/// produto não precisa deles, e dado pessoal que não se coleta não vaza (LGPD art. 6º, III).
/// É a mesma disciplina aplicada ao parlamentar, cujo CPF a API entrega e que não é
/// ingerido.</para>
/// </summary>
public sealed class Usuario : Entidade
{
    private Usuario()
    {
        // EF Core.
        Email = string.Empty;
        HashSenha = string.Empty;
    }

    public string Email { get; private set; }

    public string HashSenha { get; private set; }

    public PerfilDeUsuario Perfil { get; private set; }

    /// <summary>
    /// Preenchido quando o perfil é <see cref="PerfilDeUsuario.Gabinete"/>: o parlamentar
    /// cuja caixa institucional foi comprovada. Amarra a conta a um parlamentar específico,
    /// e não ao domínio inteiro da Câmara.
    /// </summary>
    public Guid? ParlamentarVinculadoId { get; private set; }

    public bool Ativo { get; private set; }

    public DateTimeOffset DataCriacao { get; private set; }

    public static Usuario Criar(
        string email,
        string hashSenha,
        DateTimeOffset criadoEm,
        PerfilDeUsuario perfil = PerfilDeUsuario.Cidadao,
        Guid? parlamentarVinculadoId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(hashSenha);

        if (perfil == PerfilDeUsuario.Gabinete && parlamentarVinculadoId is null)
        {
            throw new ArgumentException(
                "Conta de gabinete precisa estar vinculada a um parlamentar. Sem isso, "
                + "qualquer endereco institucional responderia pela pagina de qualquer "
                + "deputado.",
                nameof(parlamentarVinculadoId));
        }

        if (perfil != PerfilDeUsuario.Gabinete && parlamentarVinculadoId is not null)
        {
            throw new ArgumentException(
                "So' conta de gabinete se vincula a parlamentar.",
                nameof(parlamentarVinculadoId));
        }

        return new Usuario
        {
            Email = email.Trim().ToLowerInvariant(),
            HashSenha = hashSenha,
            Perfil = perfil,
            ParlamentarVinculadoId = parlamentarVinculadoId,
            Ativo = true,
            DataCriacao = criadoEm,
        };
    }

}

/// <summary>
/// Um parlamentar que o usuário acompanha. É o gatilho do ciclo de retorno: quando uma
/// varredura financiada por outra pessoa encontra achado novo, quem favoritou é avisado.
///
/// <para><b>Entidade própria, e não coleção dentro de <see cref="Usuario"/></b>, por dois
/// motivos. O primeiro é de modelagem: nenhuma leitura real parte do usuário — favoritar
/// insere uma linha sem carregar a conta, e o job de notificação pergunta "quem acompanha
/// este parlamentar?", nunca o contrário.</para>
///
/// <para>O segundo é de persistência, e foi medido: como <see cref="Entidade"/> atribui o
/// <c>Id</c> no construtor, o EF Core classifica um filho recém-adicionado à navegação de um
/// agregado já carregado como <c>Modified</c>, não <c>Added</c> — e o UPDATE resultante não
/// encontra linha alguma. Um teste de integração provou isso antes de o código chegar a
/// produção. Ser raiz própria elimina o caminho inteiro.</para>
/// </summary>
public sealed class Favorito : Entidade
{
    private Favorito()
    {
        // EF Core.
    }

    public Guid UsuarioId { get; private set; }

    public Guid ParlamentarId { get; private set; }

    public DateTimeOffset DataCriacao { get; private set; }

    /// <summary>
    /// Quando o usuário foi avisado pela última vez sobre este parlamentar.
    ///
    /// Existe para não reenviar o mesmo alerta: a notificação compara os achados novos com
    /// o que já foi comunicado, não com o histórico inteiro.
    /// </summary>
    public DateTimeOffset? UltimaNotificacaoEm { get; private set; }

    /// <summary>
    /// Cria o vínculo. A idempotência — favoritar duas vezes não duplica — é garantida pelo
    /// índice único <c>(usuario_id, parlamentar_id)</c>, e não por uma checagem em memória:
    /// duas requisições simultâneas passariam por qualquer checagem sem se enxergar.
    /// </summary>
    public static Favorito Criar(Guid usuarioId, Guid parlamentarId, DateTimeOffset momento)
    {
        if (usuarioId == Guid.Empty)
        {
            throw new ArgumentException("Favorito exige um usuario.", nameof(usuarioId));
        }

        if (parlamentarId == Guid.Empty)
        {
            throw new ArgumentException("Favorito exige um parlamentar.", nameof(parlamentarId));
        }

        return new Favorito
        {
            UsuarioId = usuarioId,
            ParlamentarId = parlamentarId,
            DataCriacao = momento,
        };
    }

    public void RegistrarNotificacao(DateTimeOffset momento) => UltimaNotificacaoEm = momento;
}
