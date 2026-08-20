using Explanando.Domain.Usuarios;

namespace Explanando.Domain.Tests.Usuarios;

public sealed class UsuarioTests
{
    private static readonly Guid ParlamentarId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OutroParlamentarId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly DateTimeOffset Agora = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    private static Usuario Cidadao() =>
        Usuario.Criar("Cidada@Exemplo.com ", "hash-argon2", Agora);

    [Fact]
    public void Nasce_ativo_como_cidadao_com_email_normalizado()
    {
        var usuario = Cidadao();

        usuario.Email.ShouldBe("cidada@exemplo.com");
        usuario.Perfil.ShouldBe(PerfilDeUsuario.Cidadao);
        usuario.Ativo.ShouldBeTrue();
        usuario.ParlamentarVinculadoId.ShouldBeNull();
    }

    [Fact]
    public void Nao_guarda_nome_telefone_nem_documento()
    {
        // LGPD art. 6, III: dado pessoal que nao se coleta nao vaza. Se alguem adicionar
        // uma dessas propriedades, este teste cai e a decisao volta a ser discutida.
        var propriedades = typeof(Usuario)
            .GetProperties()
            .Select(p => p.Name)
            .ToList();

        propriedades.ShouldNotContain("Nome");
        propriedades.ShouldNotContain("Telefone");
        propriedades.ShouldNotContain("Cpf");
        propriedades.ShouldNotContain("Documento");
    }

    [Theory]
    [InlineData("", "hash")]
    [InlineData("   ", "hash")]
    [InlineData("email@exemplo.com", "")]
    public void Recusa_email_ou_hash_vazios(string email, string hash)
    {
        Should.Throw<ArgumentException>(() => Usuario.Criar(email, hash, Agora));
    }

    // ------------------------------------------------------------------
    //  Conta de gabinete: vinculo obrigatorio
    // ------------------------------------------------------------------

    [Fact]
    public void Conta_de_gabinete_exige_parlamentar_vinculado()
    {
        // Sem o vinculo, qualquer endereco @camara.leg.br responderia pela pagina de
        // qualquer deputado — o dominio inteiro viraria uma credencial coletiva.
        var erro = Should.Throw<ArgumentException>(() => Usuario.Criar(
            "gab@camara.leg.br",
            "hash",
            Agora,
            PerfilDeUsuario.Gabinete));

        erro.ParamName.ShouldBe("parlamentarVinculadoId");
    }

    [Fact]
    public void Conta_de_gabinete_com_vinculo_e_aceita()
    {
        var usuario = Usuario.Criar(
            "gab@camara.leg.br",
            "hash",
            Agora,
            PerfilDeUsuario.Gabinete,
            ParlamentarId);

        usuario.Perfil.ShouldBe(PerfilDeUsuario.Gabinete);
        usuario.ParlamentarVinculadoId.ShouldBe(ParlamentarId);
    }

    [Theory]
    [InlineData(PerfilDeUsuario.Cidadao)]
    [InlineData(PerfilDeUsuario.Pro)]
    public void Conta_que_nao_e_de_gabinete_nao_pode_ter_vinculo(PerfilDeUsuario perfil)
    {
        var erro = Should.Throw<ArgumentException>(() => Usuario.Criar(
            "alguem@exemplo.com",
            "hash",
            Agora,
            perfil,
            ParlamentarId));

        erro.ParamName.ShouldBe("parlamentarVinculadoId");
    }

    [Fact]
    public void Nao_expoe_colecao_de_favoritos()
    {
        // Favorito e' raiz propria. A coleção navegável foi removida depois que um teste de
        // integração mostrou que o EF classifica o filho recém-adicionado como Modified —
        // porque a Entidade ja' nasce com Id — e o UPDATE nao acha linha nenhuma.
        typeof(Usuario).GetProperty("Favoritos").ShouldBeNull();
        typeof(Usuario).GetMethod("Favoritar").ShouldBeNull();
    }
}

/// <summary>
/// O gatilho do ciclo de retorno: quando uma varredura financiada por outra pessoa encontra
/// achado novo, quem favoritou e' avisado.
/// </summary>
public sealed class FavoritoTests
{
    private static readonly Guid UsuarioId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ParlamentarId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly DateTimeOffset Agora = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Registra_o_vinculo_e_a_data()
    {
        var favorito = Favorito.Criar(UsuarioId, ParlamentarId, Agora);

        favorito.UsuarioId.ShouldBe(UsuarioId);
        favorito.ParlamentarId.ShouldBe(ParlamentarId);
        favorito.DataCriacao.ShouldBe(Agora);
        favorito.UltimaNotificacaoEm.ShouldBeNull("ninguem foi avisado ainda");
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Recusa_vinculo_incompleto(bool usuarioVazio, bool parlamentarVazio)
    {
        // Um favorito orfao nunca dispararia alerta e nunca apareceria numa lista —
        // ficaria ocupando linha e silencio.
        Should.Throw<ArgumentException>(() => Favorito.Criar(
            usuarioVazio ? Guid.Empty : UsuarioId,
            parlamentarVazio ? Guid.Empty : ParlamentarId,
            Agora));
    }

    [Fact]
    public void Registrar_notificacao_marca_o_momento_do_ultimo_aviso()
    {
        // A notificacao compara os achados novos com o que ja foi comunicado; sem esta
        // marca, o mesmo alerta seria reenviado a cada varredura.
        var favorito = Favorito.Criar(UsuarioId, ParlamentarId, Agora);
        var aviso = Agora.AddDays(2);

        favorito.RegistrarNotificacao(aviso);

        favorito.UltimaNotificacaoEm.ShouldBe(aviso);
    }

    [Fact]
    public void Notificacao_mais_recente_substitui_a_anterior()
    {
        var favorito = Favorito.Criar(UsuarioId, ParlamentarId, Agora);
        favorito.RegistrarNotificacao(Agora.AddDays(1));

        favorito.RegistrarNotificacao(Agora.AddDays(9));

        favorito.UltimaNotificacaoEm.ShouldBe(Agora.AddDays(9));
    }
}
