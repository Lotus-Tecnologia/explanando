using Explanando.Application.Consultas;
using Explanando.Application.Estado;
using Explanando.Application.Ingestao;
using Explanando.Domain.Estado;
using Explanando.Infrastructure.Persistencia.Consultas;
using Explanando.Infrastructure.Persistencia.Repositorios;
using Microsoft.EntityFrameworkCore;

namespace Explanando.Integration.Tests;

/// <summary>
/// O grafo do Estado contra um Postgres de verdade.
///
/// <para>O que só um banco real prova, e o provedor em memória não: o índice único no
/// código do SIORG, o <c>timestamp with time zone</c>, os enums anuláveis que a raiz exige,
/// e — o que mais importa — que <b>duas coletas seguidas produzem a história</b>, com o
/// rastreamento do EF Core enxergando as alterações feitas nas entidades carregadas.</para>
/// </summary>
[Collection(ColecaoDeBanco.Nome)]
public sealed class GrafoDoEstadoNoBancoTests(BancoDeTestes banco)
{
    private static readonly DateTimeOffset Agora = new(2026, 9, 19, 3, 0, 0, TimeSpan.Zero);

    private static NovaUnidadeDoEstado Unidade(
        int codigo,
        int? pai = null,
        string? nome = null,
        Poder? poder = Poder.Executivo,
        TipoDeUnidade tipo = TipoDeUnidade.UnidadeAdministrativa) => new()
        {
            Codigo = codigo,
            CodigoPai = pai,
            CodigoOrgao = 26,
            Nome = nome ?? $"Unidade {codigo}",
            Sigla = $"U{codigo}",
            Poder = poder,
            Esfera = EsferaDeGoverno.Federal,
            Tipo = tipo,
            NaturezaJuridica = NaturezaJuridica.AdministracaoDireta,
            Normatizacao = NivelDeNormatizacao.AtoInterno,
        };

    private async Task LimparAsync()
    {
        await using var contexto = banco.CriarContexto();

        await contexto.UnidadesDoEstado.ExecuteDeleteAsync();
        await contexto.MudancasEstruturais.ExecuteDeleteAsync();
    }

    [Fact]
    public async Task A_raiz_sem_poder_e_sem_esfera_sobrevive_a_ida_e_volta()
    {
        // O caso que derrubaria um mapeamento com colunas obrigatorias: a Republica
        // Federativa do Brasil chega sem poder, sem esfera e sem natureza juridica.
        await LimparAsync();

        await using (var contexto = banco.CriarContexto())
        {
            contexto.UnidadesDoEstado.Add(UnidadeDoEstado.Criar(
                new NovaUnidadeDoEstado
                {
                    Codigo = 200000,
                    Nome = "República Federativa do Brasil",
                    Tipo = TipoDeUnidade.Ente,
                },
                Agora));

            await contexto.SaveChangesAsync();
        }

        await using var leitura = banco.CriarContexto();

        var raiz = await leitura.UnidadesDoEstado.SingleAsync(u => u.Codigo == 200000);

        raiz.Nome.ShouldBe("República Federativa do Brasil");
        raiz.Poder.ShouldBeNull();
        raiz.Esfera.ShouldBeNull();
        raiz.EhRaiz.ShouldBeTrue();
        raiz.Ativa.ShouldBeTrue();
    }

    [Fact]
    public async Task Duas_unidades_com_o_mesmo_codigo_sao_recusadas_pelo_banco()
    {
        // Duas linhas para o mesmo orgao seriam dois nos no desenho e arestas duplicadas.
        await LimparAsync();

        await using var contexto = banco.CriarContexto();

        contexto.UnidadesDoEstado.Add(UnidadeDoEstado.Criar(Unidade(4242), Agora));
        contexto.UnidadesDoEstado.Add(UnidadeDoEstado.Criar(Unidade(4242, nome: "Outra"), Agora));

        await Should.ThrowAsync<DbUpdateException>(() => contexto.SaveChangesAsync());
    }

    [Fact]
    public async Task Duas_coletas_produzem_a_historia_e_a_leitura_a_devolve_por_extenso()
    {
        await LimparAsync();

        // --- primeira coleta: linha de base ---
        var primeira = await ColetarAsync(
            [
                Unidade(26, nome: "Presidência da República", tipo: TipoDeUnidade.Orgao),
                Unidade(100, pai: 26),
                Unidade(101, pai: 26),
            ],
            Agora);

        primeira.EhLinhaDeBase.ShouldBeTrue();
        primeira.Mudancas.ShouldBe(0);

        // --- segunda coleta: 100 muda de nome, 101 some, 102 aparece ---
        var segunda = await ColetarAsync(
            [
                Unidade(26, nome: "Presidência da República", tipo: TipoDeUnidade.Orgao),
                Unidade(100, pai: 26, nome: "Secretaria rebatizada"),
                Unidade(102, pai: 26),
            ],
            Agora.AddDays(30));

        segunda.EhLinhaDeBase.ShouldBeFalse();
        segunda.UnidadesNovas.ShouldBe(1);
        segunda.UnidadesDesaparecidas.ShouldBe(1);
        segunda.Mudancas.ShouldBe(3);

        await using var contexto = banco.CriarContexto();

        var consultas = new ConsultasDoEstadoEfCore(contexto);
        var mudancas = await consultas.ListarMudancasAsync(50);

        mudancas.Count.ShouldBe(3);

        // A frase vem pronta, no vocabulario do leitor — nunca "CodigoPai" nem "Alterada".
        mudancas.ShouldAllBe(mudanca => mudanca.Descricao.Length > 0);
        mudancas.ShouldContain(mudanca => mudanca.Descricao == "mudou de nome");
        mudancas.ShouldContain(mudanca => mudanca.Descricao.Contains("deixou de constar"));

        // O nome congelado: a linha diz "Unidade 100", que era o nome NA EPOCA. Sem isso, a
        // noticia de hoje ficaria ilegivel depois da renomeacao de amanha.
        mudancas.ShouldContain(mudanca => mudanca.Unidade == "Unidade 100");

        // A unidade que sumiu permanece na base, inativa.
        var sumida = await contexto.UnidadesDoEstado.SingleAsync(u => u.Codigo == 101);
        sumida.Ativa.ShouldBeFalse();
    }

    [Fact]
    public async Task O_mapa_conta_as_subordinadas_em_qualquer_profundidade()
    {
        // A contagem e' o que da' escala ao desenho. Ela precisa ser RECURSIVA: contar so'
        // os filhos diretos faria um ministerio com tres secretarias e mil unidades
        // aparecer do tamanho de um conselho com tres pessoas.
        await LimparAsync();

        await ColetarAsync(
            [
                Unidade(26, nome: "Presidência", tipo: TipoDeUnidade.Orgao),
                Unidade(200, pai: 26),
                Unidade(300, pai: 200),
                Unidade(400, pai: 300),
            ],
            Agora);

        await using var contexto = banco.CriarContexto();

        var mapa = await new ConsultasDoEstadoEfCore(contexto).ObterMapaAsync();

        mapa.TotalDeUnidades.ShouldBe(4);

        var presidencia = mapa.Nos.Single(no => no.Codigo == 26);

        presidencia.UnidadesSubordinadas.ShouldBe(3);

        // Os rotulos chegam por extenso, e nao como numero de enum.
        presidencia.Tipo.ShouldBe("Órgão");
        presidencia.Poder.ShouldBe("Executivo");
        presidencia.BaseLegal.ShouldBe("Criada por ato interno");

        // Uma coleta so': nao ha' historia, e a tela precisa saber disso para nao dizer
        // "nada mudou" quando o certo e' "ainda nao comparamos".
        mapa.TemHistorico.ShouldBeFalse();
    }

    private async Task<ResultadoDaColetaDoEstado> ColetarAsync(
        IReadOnlyList<NovaUnidadeDoEstado> unidades,
        DateTimeOffset quando)
    {
        await using var contexto = banco.CriarContexto();

        var servico = new ServicoDeColetaDoEstado(
            new FonteFalsa(unidades),
            new RepositorioDoEstado(contexto),
            new RelogioFixo(quando));

        return await servico.ColetarAsync();
    }

    private sealed class FonteFalsa(IReadOnlyList<NovaUnidadeDoEstado> unidades)
        : IFonteDaEstruturaDoEstado
    {
        public Task<IReadOnlyList<NovaUnidadeDoEstado>> ListarOrgaosAsync(
            CancellationToken cancelamento = default) =>
            Task.FromResult<IReadOnlyList<NovaUnidadeDoEstado>>([unidades[0]]);

        public Task<SubarvoreDoEstado> ObterSubarvoreAsync(
            int codigoDoOrgao,
            CancellationToken cancelamento = default) =>
            Task.FromResult(new SubarvoreDoEstado
            {
                CodigoDoOrgao = codigoDoOrgao,
                Unidades = unidades,
            });

        public Task<NovaUnidadeDoEstado?> ObterUnidadeAsync(
            int codigo,
            CancellationToken cancelamento = default) =>
            Task.FromResult<NovaUnidadeDoEstado?>(null);
    }

    private sealed class RelogioFixo(DateTimeOffset agora) : IRelogio
    {
        public DateTimeOffset Agora => agora;
    }
}
