using System.Globalization;
using Explanando.Domain.Auditoria;
using Explanando.Domain.Comum;
using Explanando.Domain.Contestacoes;
using Explanando.Domain.Despesas;
using Explanando.Domain.Parlamentares;
using Explanando.Domain.Usuarios;
using Explanando.Domain.Varreduras;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Explanando.Integration.Tests;

/// <summary>
/// Prova contra Postgres real o que o dominio nao consegue garantir sozinho: unicidade sob
/// concorrencia, indice filtrado e o comportamento das chaves estrangeiras quando algo e'
/// apagado. As entidades de produto sao escritas por acao de pessoa — sao justamente as que
/// recebem duas requisicoes ao mesmo tempo.
/// </summary>
[Collection(ColecaoDeBanco.Nome)]
public sealed class ProdutoNoBancoTests(BancoDeTestes banco)
{
    private static int _sequencia;

    /// <summary>
    /// Faixa 600.000, distinta das outras classes que compartilham o mesmo banco:
    /// 700.000 e' de VarreduraPontaAPonta, 800.000 de IngestaoPontaAPonta, 900.000 de
    /// Mapeamento. O indice unico (id_externo, tipo) e' global.
    /// </summary>
    private static int ProximoIdExterno() => Interlocked.Increment(ref _sequencia) + 600_000;

    private static string ProximoEmail() =>
        $"pessoa{Interlocked.Increment(ref _sequencia).ToString(CultureInfo.InvariantCulture)}@exemplo.com";

    private static readonly DateTimeOffset Agora = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);

    private static Parlamentar NovoDeputado() => Parlamentar.Criar(new NovoParlamentar
    {
        IdExterno = ProximoIdExterno(),
        NomeCivil = "ACÁCIO DA SILVA FAVACHO NETO",
        NomeParlamentar = "Acácio Favacho",
        SiglaPartido = "MDB",
        Uf = "AP",
        Tipo = TipoParlamentar.DeputadoFederal,
    });

    private static Despesa NovaDespesaDe(Guid parlamentarId, string codigoDocumento) =>
        Despesa.Criar(new NovaDespesa
        {
            ParlamentarId = parlamentarId,
            DocumentoFornecedor = CpfCnpj.Criar("36826246000164"),
            NomeFornecedor = "AUTO POSTO BOMTEMPO LTDA",
            Competencia = Competencia.Criar(2026, 7),
            TipoDespesa = "COMBUSTÍVEIS E LUBRIFICANTES.",
            CodigoDocumento = codigoDocumento,
            NumeroDocumento = "8887",
            DataDocumento = new DateTimeOffset(2026, 7, 6, 0, 0, 0, TimeSpan.Zero),
            ValorDocumento = 385.92m,
            ValorLiquido = 385.92m,
        });

    // ------------------------------------------------------------------
    //  Contestacao
    // ------------------------------------------------------------------

    [Fact]
    public async Task Contestacao_sobrevive_ao_banco_com_o_texto_do_gabinete_intacto()
    {
        // O texto vai ao banco sem passar por filtro de vocabulario. Este teste guarda
        // essa decisao: se alguem plugar o VocabularioDeCompliance no caminho, ele cai.
        var deputado = NovoDeputado();
        var despesa = NovaDespesaDe(deputado.Id, $"cont-{ProximoIdExterno()}");

        const string Defesa =
            "A acusacao de irregularidade e' improcedente: o servico foi prestado e "
            + "atestado pela Diretoria-Geral.";

        var contestacao = ContestacaoDespesa.Criar(
            despesa.Id,
            deputado.Id,
            "dep.acacio@camara.leg.br",
            Defesa,
            Agora);

        await using (var escrita = banco.CriarContexto())
        {
            escrita.Parlamentares.Add(deputado);
            escrita.Despesas.Add(despesa);
            escrita.Contestacoes.Add(contestacao);
            await escrita.SaveChangesAsync();
        }

        await using var leitura = banco.CriarContexto();
        var lida = await leitura.Contestacoes.SingleAsync(c => c.Id == contestacao.Id);

        lida.Texto.ShouldBe(Defesa);
        lida.EmailDoGabinete.ShouldBe("dep.acacio@camara.leg.br");
        lida.Situacao.ShouldBe(SituacaoDaContestacao.Publicada);
        lida.EstaVisivel.ShouldBeTrue();
        lida.FlagAnomaliaId.ShouldBeNull();
    }

    [Fact]
    public async Task Contestacao_aponta_para_a_flag_e_sobrevive_a_flag_ser_apagada()
    {
        // Se uma recalibragem remover a flag, a resposta do gabinete nao pode sumir junto:
        // ela continua sendo o registro de que o gabinete se manifestou.
        var deputado = NovoDeputado();
        var despesa = NovaDespesaDe(deputado.Id, $"flag-{ProximoIdExterno()}");
        var flag = FlagAnomalia.Criar(
            despesa.Id,
            "REG_KYB_001",
            Gravidade.Alta,
            "Fornecedor com CNAE incompatível com o tipo de despesa.",
            1.0m);
        flag.RegistrarVersaoDoMotor(2);

        var contestacao = ContestacaoDespesa.Criar(
            despesa.Id,
            deputado.Id,
            "gab@camara.leg.br",
            "O CNAE secundario do fornecedor cobre o servico contratado.",
            Agora,
            flagAnomaliaId: flag.Id);

        await using (var escrita = banco.CriarContexto())
        {
            escrita.Parlamentares.Add(deputado);
            escrita.Despesas.Add(despesa);
            escrita.FlagsAnomalias.Add(flag);
            escrita.Contestacoes.Add(contestacao);
            await escrita.SaveChangesAsync();
        }

        await using (var remocao = banco.CriarContexto())
        {
            remocao.FlagsAnomalias.Remove(
                await remocao.FlagsAnomalias.SingleAsync(f => f.Id == flag.Id));
            await remocao.SaveChangesAsync();
        }

        await using var leitura = banco.CriarContexto();
        var lida = await leitura.Contestacoes.SingleOrDefaultAsync(c => c.Id == contestacao.Id);

        lida.ShouldNotBeNull();
        lida.FlagAnomaliaId.ShouldBeNull("a flag sumiu, a resposta do gabinete permanece");
        lida.Texto.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Versao_do_motor_sobrevive_na_flag()
    {
        var deputado = NovoDeputado();
        var despesa = NovaDespesaDe(deputado.Id, $"versao-{ProximoIdExterno()}");
        var flag = FlagAnomalia.Criar(
            despesa.Id,
            "REG_TX_001",
            Gravidade.Media,
            "Sequência de documentos do mesmo fornecedor no mesmo mês.",
            1.0m);
        flag.RegistrarVersaoDoMotor(2);

        await using (var escrita = banco.CriarContexto())
        {
            escrita.Parlamentares.Add(deputado);
            escrita.Despesas.Add(despesa);
            escrita.FlagsAnomalias.Add(flag);
            await escrita.SaveChangesAsync();
        }

        await using var leitura = banco.CriarContexto();

        (await leitura.FlagsAnomalias.SingleAsync(f => f.Id == flag.Id))
            .VersaoDoMotor.ShouldBe(2);
    }

    // ------------------------------------------------------------------
    //  Usuario e favorito
    // ------------------------------------------------------------------

    [Fact]
    public async Task Email_de_usuario_e_unico_no_banco()
    {
        var email = ProximoEmail();

        await using (var escrita = banco.CriarContexto())
        {
            escrita.Usuarios.Add(Usuario.Criar(email, "hash-a", Agora));
            await escrita.SaveChangesAsync();
        }

        await using var segunda = banco.CriarContexto();
        segunda.Usuarios.Add(Usuario.Criar(email, "hash-b", Agora));

        var erro = await Should.ThrowAsync<DbUpdateException>(() => segunda.SaveChangesAsync());

        erro.InnerException.ShouldBeOfType<PostgresException>()
            .SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task Favoritar_nao_precisa_carregar_a_conta_do_usuario()
    {
        // O caminho real do produto: POST /favoritos insere uma linha. Este teste existe
        // porque a versao anterior modelava o favorito como colecao de Usuario, e o EF
        // classificava o filho recem-adicionado como Modified — o UPDATE nao achava linha,
        // e favoritar quebrava por inteiro.
        var deputado = NovoDeputado();
        var outro = NovoDeputado();
        var usuario = Usuario.Criar(ProximoEmail(), "hash", Agora);

        await using (var cadastro = banco.CriarContexto())
        {
            cadastro.Parlamentares.AddRange(deputado, outro);
            cadastro.Usuarios.Add(usuario);
            await cadastro.SaveChangesAsync();
        }

        await using (var favoritando = banco.CriarContexto())
        {
            favoritando.Favoritos.Add(Favorito.Criar(usuario.Id, deputado.Id, Agora));
            favoritando.Favoritos.Add(Favorito.Criar(usuario.Id, outro.Id, Agora));
            await favoritando.SaveChangesAsync();
        }

        await using var leitura = banco.CriarContexto();
        var favoritos = await leitura.Favoritos
            .Where(f => f.UsuarioId == usuario.Id)
            .ToListAsync();

        favoritos.Count.ShouldBe(2);
        favoritos.Select(f => f.ParlamentarId).ShouldBe([deputado.Id, outro.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task Mesmo_parlamentar_nao_pode_ser_favoritado_duas_vezes_pelo_mesmo_usuario()
    {
        // Nao ha checagem em memoria: duas requisicoes simultaneas passariam por ela sem
        // se enxergar. O indice unico e' a unica garantia real, e e' esta.
        var deputado = NovoDeputado();
        var usuario = Usuario.Criar(ProximoEmail(), "hash", Agora);

        await using (var escrita = banco.CriarContexto())
        {
            escrita.Parlamentares.Add(deputado);
            escrita.Usuarios.Add(usuario);
            escrita.Favoritos.Add(Favorito.Criar(usuario.Id, deputado.Id, Agora));
            await escrita.SaveChangesAsync();
        }

        await using var concorrente = banco.CriarContexto();
        concorrente.Favoritos.Add(Favorito.Criar(usuario.Id, deputado.Id, Agora.AddMinutes(1)));

        var erro = await Should.ThrowAsync<DbUpdateException>(() => concorrente.SaveChangesAsync());

        erro.InnerException.ShouldBeOfType<PostgresException>()
            .SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task Apagar_a_conta_leva_os_favoritos_junto()
    {
        // Aqui Cascade e' o certo, ao contrario da solicitacao paga: o favorito so' existe
        // para avisar aquela pessoa, e sem ela nao tem a quem avisar.
        var deputado = NovoDeputado();
        var usuario = Usuario.Criar(ProximoEmail(), "hash", Agora);

        await using (var escrita = banco.CriarContexto())
        {
            escrita.Parlamentares.Add(deputado);
            escrita.Usuarios.Add(usuario);
            escrita.Favoritos.Add(Favorito.Criar(usuario.Id, deputado.Id, Agora));
            await escrita.SaveChangesAsync();
        }

        await using (var remocao = banco.CriarContexto())
        {
            remocao.Usuarios.Remove(await remocao.Usuarios.SingleAsync(u => u.Id == usuario.Id));
            await remocao.SaveChangesAsync();
        }

        await using var leitura = banco.CriarContexto();

        (await leitura.Favoritos.CountAsync(f => f.UsuarioId == usuario.Id)).ShouldBe(0);
    }

    [Fact]
    public async Task Conta_de_gabinete_persiste_o_vinculo_com_o_parlamentar()
    {
        var deputado = NovoDeputado();
        var gabinete = Usuario.Criar(
            ProximoEmail(),
            "hash",
            Agora,
            PerfilDeUsuario.Gabinete,
            deputado.Id);

        await using (var escrita = banco.CriarContexto())
        {
            escrita.Parlamentares.Add(deputado);
            escrita.Usuarios.Add(gabinete);
            await escrita.SaveChangesAsync();
        }

        await using var leitura = banco.CriarContexto();
        var lido = await leitura.Usuarios.SingleAsync(u => u.Id == gabinete.Id);

        lido.Perfil.ShouldBe(PerfilDeUsuario.Gabinete);
        lido.ParlamentarVinculadoId.ShouldBe(deputado.Id);
    }

    // ------------------------------------------------------------------
    //  Solicitacao de varredura
    // ------------------------------------------------------------------

    [Fact]
    public async Task Mesmo_payment_id_nao_entra_duas_vezes()
    {
        // A outra metade da idempotencia do webhook. O dominio cobre o reenvio sequencial
        // sobre a mesma instancia; isto cobre dois webhooks processados em paralelo.
        var deputado = NovoDeputado();
        var paymentId = $"pay_{ProximoIdExterno().ToString(CultureInfo.InvariantCulture)}";

        var primeira = SolicitacaoVarredura.Criar(deputado.Id, 9.90m, Agora);
        primeira.ConfirmarPagamento(paymentId, Agora.AddMinutes(1));

        var segunda = SolicitacaoVarredura.Criar(deputado.Id, 9.90m, Agora);
        segunda.ConfirmarPagamento(paymentId, Agora.AddMinutes(1));

        await using (var escrita = banco.CriarContexto())
        {
            escrita.Parlamentares.Add(deputado);
            escrita.SolicitacoesVarredura.Add(primeira);
            await escrita.SaveChangesAsync();
        }

        await using var concorrente = banco.CriarContexto();
        concorrente.SolicitacoesVarredura.Add(segunda);

        var erro = await Should.ThrowAsync<DbUpdateException>(() => concorrente.SaveChangesAsync());

        erro.InnerException.ShouldBeOfType<PostgresException>()
            .SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task Varias_solicitacoes_sem_pagamento_convivem_no_indice_filtrado()
    {
        // O indice de payment_id e' unico mas filtrado por NOT NULL: sem o filtro, a
        // segunda solicitacao aguardando pagamento colidiria com a primeira.
        var deputado = NovoDeputado();

        await using var contexto = banco.CriarContexto();
        contexto.Parlamentares.Add(deputado);
        contexto.SolicitacoesVarredura.Add(SolicitacaoVarredura.Criar(deputado.Id, 9.90m, Agora));
        contexto.SolicitacoesVarredura.Add(SolicitacaoVarredura.Criar(deputado.Id, 9.90m, Agora));

        await Should.NotThrowAsync(() => contexto.SaveChangesAsync());
    }

    [Fact]
    public async Task Apagar_a_conta_preserva_o_registro_do_pagamento()
    {
        // LGPD art. 18, VI: o usuario pode sumir. O registro contabil de que houve uma
        // cobranca nao pode sumir com ele.
        var deputado = NovoDeputado();
        var usuario = Usuario.Criar(ProximoEmail(), "hash", Agora);
        var solicitacao = SolicitacaoVarredura.Criar(deputado.Id, 9.90m, Agora, usuario.Id);
        solicitacao.ConfirmarPagamento(
            $"pay_{ProximoIdExterno().ToString(CultureInfo.InvariantCulture)}",
            Agora.AddMinutes(1));

        await using (var escrita = banco.CriarContexto())
        {
            escrita.Parlamentares.Add(deputado);
            escrita.Usuarios.Add(usuario);
            escrita.SolicitacoesVarredura.Add(solicitacao);
            await escrita.SaveChangesAsync();
        }

        await using (var remocao = banco.CriarContexto())
        {
            remocao.Usuarios.Remove(await remocao.Usuarios.SingleAsync(u => u.Id == usuario.Id));
            await remocao.SaveChangesAsync();
        }

        await using var leitura = banco.CriarContexto();
        var lida = await leitura.SolicitacoesVarredura.SingleOrDefaultAsync(
            s => s.Id == solicitacao.Id);

        lida.ShouldNotBeNull();
        lida.UsuarioId.ShouldBeNull();
        lida.ValorPago.ShouldBe(9.90m);
        lida.Situacao.ShouldBe(SituacaoDaSolicitacao.Paga);
    }

    // ------------------------------------------------------------------
    //  Historico de score
    // ------------------------------------------------------------------

    [Fact]
    public async Task Historico_guarda_um_ponto_por_parlamentar_por_dia()
    {
        var deputado = NovoDeputado();
        var score = ScoreRiscoParlamentar.Criar(deputado.Id);
        score.Recalcular([], [], Agora, 100, 0);

        await using (var escrita = banco.CriarContexto())
        {
            escrita.Parlamentares.Add(deputado);
            escrita.HistoricoScores.Add(HistoricoScore.Capturar(score, 2, Agora));
            await escrita.SaveChangesAsync();
        }

        await using var segunda = banco.CriarContexto();

        // Mesma data de referencia, hora diferente: e' o mesmo ponto do grafico.
        segunda.HistoricoScores.Add(HistoricoScore.Capturar(score, 2, Agora.AddHours(6)));

        var erro = await Should.ThrowAsync<DbUpdateException>(() => segunda.SaveChangesAsync());

        erro.InnerException.ShouldBeOfType<PostgresException>()
            .SqlState.ShouldBe(PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task Historico_preserva_a_precisao_dos_dois_numeros()
    {
        // As colunas tem de casar com as de scores_risco: numeric(18,2) e numeric(10,4).
        // Se o historico arredondar diferente, a curva discorda do numero da pagina.
        var deputado = NovoDeputado();
        var despesas = Enumerable.Range(1, 150)
            .Select(i => NovaDespesaDe(deputado.Id, $"hist-{ProximoIdExterno()}-{i}"))
            .ToList();

        var flags = despesas.Take(7)
            .Select(d =>
            {
                var flag = FlagAnomalia.Criar(
                    d.Id,
                    "REG_KYB_001",
                    Gravidade.Alta,
                    "Fornecedor com CNAE incompatível com o tipo de despesa.",
                    1.0m);
                flag.RegistrarVersaoDoMotor(2);
                return flag;
            })
            .ToList();

        var score = ScoreRiscoParlamentar.Criar(deputado.Id);
        score.Recalcular(despesas, flags, Agora, 100, despesas.Count);

        var ponto = HistoricoScore.Capturar(score, MotorDeRegrasVersao, Agora);

        await using (var escrita = banco.CriarContexto())
        {
            escrita.Parlamentares.Add(deputado);
            escrita.HistoricoScores.Add(ponto);
            await escrita.SaveChangesAsync();
        }

        await using var leitura = banco.CriarContexto();
        var lido = await leitura.HistoricoScores.SingleAsync(h => h.Id == ponto.Id);

        lido.VolumeTotalGasto.ShouldBe(score.VolumeTotalGasto);
        lido.IndiceDeRisco.ShouldBe(score.IndiceDeRisco);
        lido.DespesasAvaliadas.ShouldBe(150);
        lido.TotalFlagsVermelhas.ShouldBe(7);
        lido.VersaoDoMotor.ShouldBe(MotorDeRegrasVersao);
        lido.Referencia.ShouldBe(new DateOnly(2026, 8, 20));
    }

    // ------------------------------------------------------------------
    //  O piso de cobertura de fornecedor (decisao de 26/09/2026)
    // ------------------------------------------------------------------

    [Fact]
    public async Task Abaixo_do_piso_de_cobertura_sai_do_ranking_por_indice_e_fica_no_de_volume()
    {
        // Auditoria de 25/09: 92 dos 498 deputados ranqueados tinham menos da metade das
        // despesas com fornecedor identificado. O indice deles descrevia menos da metade do
        // gasto, e disputava posicao com quem tinha tudo examinado.
        var coberto = NovoDeputado();
        var descoberto = NovoDeputado();

        await using (var escrita = banco.CriarContexto())
        {
            escrita.Parlamentares.AddRange(coberto, descoberto);

            foreach (var (deputado, comFornecedor) in new[] { (coberto, 150), (descoberto, 45) })
            {
                var despesas = Enumerable.Range(1, 150)
                    .Select(i => NovaDespesaDe(deputado.Id, $"cob-{ProximoIdExterno()}-{i}"))
                    .ToList();

                var score = ScoreRiscoParlamentar.Criar(deputado.Id);
                score.Recalcular(despesas, [], Agora, 100, comFornecedor);
                escrita.ScoresRisco.Add(score);
            }

            await escrita.SaveChangesAsync();
        }

        await using var leitura = banco.CriarContexto();
        var consultas = new Infrastructure.Persistencia.Consultas.ConsultasPublicasEfCore(leitura);

        var porIndice = await consultas.RanquearAsync(
            Application.Consultas.OrdenacaoDoRanking.Indice, TipoParlamentar.DeputadoFederal, 1, 200);
        var porVolume = await consultas.RanquearAsync(
            Application.Consultas.OrdenacaoDoRanking.Volume, TipoParlamentar.DeputadoFederal, 1, 200);

        porIndice.Itens.Select(item => item.Id).ShouldContain(coberto.Id);
        porIndice.Itens.Select(item => item.Id).ShouldNotContain(descoberto.Id);

        var noVolume = porVolume.Itens.Single(item => item.Id == descoberto.Id);
        noVolume.CoberturaDeFornecedor.ShouldBe(0.3m);
        noVolume.CoberturaSuficiente.ShouldBeFalse();

        // O indice continua publicado: ele existe, so' nao disputa posicao.
        noVolume.IndiceDeRisco.ShouldNotBeNull();

        var perfil = await consultas.ObterPerfilAsync(descoberto.Id);
        perfil!.CoberturaDeFornecedor.ShouldBe(0.3m);
        perfil.CoberturaSuficiente.ShouldBeFalse();
    }

    [Fact]
    public async Task Fornecedores_distintos_nao_contam_o_documento_vazio()
    {
        // Levantamento de 08/11/2026: a passagem aerea da Camara vem sem documento do
        // fornecedor, e o vazio contava como um fornecedor a mais em 500 dos 631 perfis.
        var deputado = NovoDeputado();

        await using (var escrita = banco.CriarContexto())
        {
            escrita.Parlamentares.Add(deputado);
            foreach (var documento in new[] { "36826246000164", "11222333000181", "" })
            {
                var despesa = Despesa.Criar(new NovaDespesa
                {
                    ParlamentarId = deputado.Id,
                    DocumentoFornecedor = CpfCnpj.Criar(documento),
                    NomeFornecedor = "FORNECEDOR",
                    Competencia = Competencia.Criar(2026, 7),
                    TipoDespesa = "PASSAGEM AÉREA - SIGEPA",
                    CodigoDocumento = $"forn-{ProximoIdExterno()}",
                    NumeroDocumento = "1",
                    DataDocumento = new DateTimeOffset(2026, 7, 6, 0, 0, 0, TimeSpan.Zero),
                    ValorDocumento = 100m,
                    ValorLiquido = 100m,
                });
                escrita.Despesas.Add(despesa);
            }

            await escrita.SaveChangesAsync();
        }

        await using var leitura = banco.CriarContexto();
        var perfil = await new Infrastructure.Persistencia.Consultas.ConsultasPublicasEfCore(leitura)
            .ObterPerfilAsync(deputado.Id);

        perfil!.FornecedoresDistintos.ShouldBe(2);
    }

    private const int MotorDeRegrasVersao = 2;
}
