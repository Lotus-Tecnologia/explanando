using System.Runtime.CompilerServices;
using Explanando.Application.Ingestao;
using Explanando.Domain.Comum;
using Explanando.Domain.Despesas;
using Explanando.Domain.Fornecedores;
using Explanando.Domain.Ingestao;
using Explanando.Domain.Parlamentares;

namespace Explanando.Application.Tests.Ingestao;

public sealed class ServicoDeColetaTests
{
    private const int Legislatura = 57;
    private static readonly DateTimeOffset Agora = new(2026, 8, 19, 12, 0, 0, TimeSpan.Zero);

    private static DeputadoDaFonte Deputado(int id, bool emExercicio = true) => new()
    {
        IdExterno = id,
        NomeCivil = $"DEPUTADO {id}",
        NomeParlamentar = $"Dep {id}",
        SiglaPartido = "XYZ",
        Uf = "SP",
        EmExercicio = emExercicio,
    };

    private static DespesaDaFonte Despesa(string codigo) => new()
    {
        Ano = 2026,
        Mes = 7,
        TipoDespesa = "TELEFONIA",
        CodigoDocumento = codigo,
        NumeroDocumento = "1",
        DataDocumento = new DateTimeOffset(2026, 7, 6, 0, 0, 0, TimeSpan.Zero),
        ValorDocumento = 100m,
        ValorLiquido = 100m,
        CnpjCpfFornecedor = "36826246000164",
    };

    private static (ServicoDeColeta Coleta, CheckpointsFalsos Checkpoints) Montar(
        IReadOnlyList<DeputadoDaFonte> deputados,
        IReadOnlyList<DespesaDaFonte>? despesas = null,
        Func<int, bool>? falhaEm = null)
    {
        var fonte = new FonteFalsa(deputados, despesas ?? [Despesa("1")], falhaEm);
        var checkpoints = new CheckpointsFalsos();
        var ingestao = new ServicoDeIngestao(fonte, new CadastroVazio(), new RepositorioEmMemoria());

        return (new ServicoDeColeta(fonte, ingestao, checkpoints, new RelogioFixo(Agora)), checkpoints);
    }

    [Fact]
    public async Task Percorre_todos_os_deputados_da_fonte()
    {
        var (coleta, _) = Montar([Deputado(1), Deputado(2), Deputado(3)]);

        var resultado = await coleta.ColetarTudoAsync(Legislatura);

        resultado.DeputadosVisitados.ShouldBe(3);
        resultado.DespesasNovas.ShouldBe(3);
    }

    [Fact]
    public async Task Respeita_o_limite_de_deputados_da_amostragem()
    {
        // A passada completa gera dezenas de milhares de consultas a servicos publicos e
        // gratuitos. Para calibrar limiares, uma amostra responde a mesma pergunta.
        var (coleta, _) = Montar([Deputado(1), Deputado(2), Deputado(3), Deputado(4)]);

        var resultado = await coleta.ColetarTudoAsync(Legislatura, maxDeputados: 2);

        resultado.DeputadosVisitados.ShouldBe(2);
    }

    [Fact]
    public async Task Deputado_pulado_conta_para_o_limite()
    {
        // Se pulado nao contasse, um limite de 2 sobre uma base ja coletada percorreria
        // a lista inteira ate achar 2 nao coletados — justamente o que o limite evita.
        var (coleta, checkpoints) = Montar([Deputado(1), Deputado(2), Deputado(3)]);
        checkpoints.Semear(CheckpointDeIngestao.Criar(1, Legislatura, Agora, 5, 0, comAlerta: false));

        var resultado = await coleta.ColetarTudoAsync(Legislatura, maxDeputados: 2);

        resultado.DeputadosPulados.ShouldBe(1);
        resultado.DeputadosVisitados.ShouldBe(1);
    }

    [Fact]
    public async Task Registra_checkpoint_para_cada_deputado_coletado()
    {
        var (coleta, checkpoints) = Montar([Deputado(1), Deputado(2)]);

        await coleta.ColetarTudoAsync(Legislatura);

        checkpoints.Registrados.Count.ShouldBe(2);
        checkpoints.Registrados[0].ConcluidoEm.ShouldBe(Agora);
        checkpoints.Registrados[0].DespesasImportadas.ShouldBe(1);
    }

    [Fact]
    public async Task Pula_deputado_com_checkpoint_recente()
    {
        // Economia de tempo, nao de corretude: reprocessar tudo tambem funcionaria,
        // so' custaria as ~6.000 requisicoes de novo.
        var (coleta, checkpoints) = Montar([Deputado(1), Deputado(2)]);
        checkpoints.Semear(CheckpointDeIngestao.Criar(
            1, Legislatura, Agora.AddHours(-1), despesasImportadas: 10,
            despesasRejeitadas: 0, comAlerta: false));

        var resultado = await coleta.ColetarTudoAsync(Legislatura);

        resultado.DeputadosPulados.ShouldBe(1);
        resultado.DeputadosVisitados.ShouldBe(1);
    }

    [Fact]
    public async Task Nao_pula_deputado_com_checkpoint_vencido()
    {
        var (coleta, checkpoints) = Montar([Deputado(1)]);
        checkpoints.Semear(CheckpointDeIngestao.Criar(
            1, Legislatura, Agora.AddDays(-3), 10, 0, comAlerta: false));

        var resultado = await coleta.ColetarTudoAsync(Legislatura);

        resultado.DeputadosPulados.ShouldBe(0);
        resultado.DeputadosVisitados.ShouldBe(1);
    }

    [Fact]
    public async Task Nunca_pula_deputado_cujo_checkpoint_anterior_teve_alarme()
    {
        // Checkpoint com alarme registra uma coleta que provavelmente falhou. Trata-lo
        // como valido perpetuaria o buraco no banco para sempre.
        var (coleta, checkpoints) = Montar([Deputado(1)]);
        checkpoints.Semear(CheckpointDeIngestao.Criar(
            1, Legislatura, Agora.AddMinutes(-1), 0, 0, comAlerta: true));

        var resultado = await coleta.ColetarTudoAsync(Legislatura);

        resultado.DeputadosPulados.ShouldBe(0);
        resultado.DeputadosVisitados.ShouldBe(1);
    }

    [Fact]
    public async Task Atualiza_o_checkpoint_existente_em_vez_de_criar_outro()
    {
        var (coleta, checkpoints) = Montar([Deputado(1)]);
        checkpoints.Semear(CheckpointDeIngestao.Criar(
            1, Legislatura, Agora.AddDays(-3), 10, 0, comAlerta: false));

        await coleta.ColetarTudoAsync(Legislatura);

        checkpoints.Registrados.Count.ShouldBe(1, "a tabela tem indice unico por parlamentar e legislatura");
        checkpoints.Registrados[0].ConcluidoEm.ShouldBe(Agora);
    }

    // ------------------------------------------------------------------
    //  Resiliencia da passada completa
    // ------------------------------------------------------------------

    [Fact]
    public async Task Falha_num_deputado_nao_interrompe_os_demais()
    {
        // A Camara pode devolver 500 para um id especifico. Derrubar a coleta inteira
        // por causa disso jogaria fora todo o trabalho ja feito na mesma passada.
        var (coleta, _) = Montar([Deputado(1), Deputado(2), Deputado(3)], falhaEm: id => id == 2);

        var resultado = await coleta.ColetarTudoAsync(Legislatura);

        resultado.DeputadosVisitados.ShouldBe(2);
        resultado.Falhas.Count.ShouldBe(1);
        resultado.Falhas[0].ShouldContain("2");
    }

    [Fact]
    public async Task Deputado_que_falhou_nao_ganha_checkpoint()
    {
        // Sem checkpoint, a proxima passada tenta de novo — que e' o comportamento certo.
        var (coleta, checkpoints) = Montar([Deputado(1), Deputado(2)], falhaEm: id => id == 2);

        await coleta.ColetarTudoAsync(Legislatura);

        checkpoints.Registrados.Count.ShouldBe(1);
        checkpoints.Registrados[0].IdExternoParlamentar.ShouldBe(1);
    }

    [Fact]
    public async Task Propaga_os_alarmes_de_sanidade_dos_deputados()
    {
        var (coleta, _) = Montar([Deputado(1)], despesas: []);

        var resultado = await coleta.ColetarTudoAsync(Legislatura);

        resultado.Alertas.Count.ShouldBe(1);
        resultado.Alertas[0].ShouldContain("idLegislatura");
    }

    [Fact]
    public async Task Checkpoint_de_coleta_com_alarme_fica_marcado()
    {
        var (coleta, checkpoints) = Montar([Deputado(1)], despesas: []);

        await coleta.ColetarTudoAsync(Legislatura);

        checkpoints.Registrados[0].ComAlerta.ShouldBeTrue();
        checkpoints.Registrados[0].AindaVale(Agora, ServicoDeColeta.ValidadeDoCheckpoint)
            .ShouldBeFalse("checkpoint com alarme nunca dispensa nova tentativa");
    }

    // ==================================================================
    //  Dubles
    // ==================================================================

    private sealed class FonteFalsa(
        IReadOnlyList<DeputadoDaFonte> deputados,
        IReadOnlyList<DespesaDaFonte> despesas,
        Func<int, bool>? falhaEm) : IFonteDeParlamentares
    {
        public async IAsyncEnumerable<DeputadoDaFonte> ListarDeputadosAsync(
            [EnumeratorCancellation] CancellationToken cancelamento = default)
        {
            foreach (var deputado in deputados)
            {
                yield return deputado;
            }

            await Task.CompletedTask;
        }

        public Task<DeputadoDaFonte?> ObterDetalheAsync(int id, CancellationToken c = default) =>
            Task.FromResult(deputados.FirstOrDefault(d => d.IdExterno == id));

        public async IAsyncEnumerable<DespesaDaFonte> ListarDespesasAsync(
            int idExterno,
            int legislatura,
            [EnumeratorCancellation] CancellationToken cancelamento = default)
        {
            if (falhaEm?.Invoke(idExterno) == true)
            {
                throw new HttpRequestException($"Falha simulada para o deputado {idExterno}.");
            }

            foreach (var despesa in despesas)
            {
                yield return despesa;
            }

            await Task.CompletedTask;
        }
    }

    private sealed class CadastroVazio : IFonteDeCadastroEmpresarial
    {
        public Task<EmpresaDaFonte?> ConsultarAsync(string cnpj, CancellationToken c = default) =>
            Task.FromResult<EmpresaDaFonte?>(null);
    }

    private sealed class CheckpointsFalsos : IRepositorioDeCheckpoints
    {
        public List<CheckpointDeIngestao> Registrados { get; } = [];

        public void Semear(CheckpointDeIngestao checkpoint) => Registrados.Add(checkpoint);

        public Task<CheckpointDeIngestao?> ObterAsync(int idExterno, int legislatura, CancellationToken c = default) =>
            Task.FromResult(Registrados.Find(x =>
                x.IdExternoParlamentar == idExterno && x.Legislatura == legislatura));

        public void Adicionar(CheckpointDeIngestao checkpoint) => Registrados.Add(checkpoint);

        public Task SalvarAsync(CancellationToken c = default) => Task.CompletedTask;
    }

    private sealed class RelogioFixo(DateTimeOffset momento) : IRelogio
    {
        public DateTimeOffset Agora => momento;
    }

    private sealed class RepositorioEmMemoria : IRepositorioDeIngestao
    {
        private readonly List<Parlamentar> _parlamentares = [];
        private readonly List<Despesa> _despesas = [];

        public Task<Parlamentar?> ObterParlamentarAsync(int idExterno, CancellationToken c = default) =>
            Task.FromResult(_parlamentares.Find(p => p.IdExterno == idExterno));

        public Task<Fornecedor?> ObterFornecedorAsync(CpfCnpj documento, CancellationToken c = default) =>
            Task.FromResult<Fornecedor?>(null);

        public Task<HashSet<byte[]>> ObterHashesDeDespesasAsync(Guid parlamentarId, CancellationToken c = default) =>
            Task.FromResult(_despesas
                .Where(d => d.ParlamentarId == parlamentarId)
                .Select(d => d.HashUnico)
                .ToHashSet(ComparadorDeHash.Instancia));

        public void Adicionar(Parlamentar parlamentar) => _parlamentares.Add(parlamentar);

        public void Adicionar(Fornecedor fornecedor)
        {
            // Nao usado neste conjunto de testes.
        }

        public void Adicionar(Sociedade sociedade)
        {
            // Nao usado neste conjunto de testes.
        }

        public void Adicionar(Despesa despesa) => _despesas.Add(despesa);

        public Task SalvarAsync(CancellationToken c = default) => Task.CompletedTask;
    }
}
