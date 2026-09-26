using Explanando.Infrastructure.Integracoes.Senado;
using Explanando.Application.Cadeiras;
using Explanando.Application.Ciclo;
using Explanando.Application.Auditoria;
using Explanando.Application.Consultas;
using Explanando.Application.Estado;
using Explanando.Application.Ingestao;
using Explanando.Application.Retencao;
using Explanando.Domain.Regras;
using Explanando.Infrastructure.Integracoes;
using Explanando.Infrastructure.Persistencia;
using Explanando.Infrastructure.Persistencia.Consultas;
using Explanando.Infrastructure.Persistencia.Repositorios;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;

namespace Explanando.Infrastructure;

/// <summary>
/// Ponto unico de composicao da infraestrutura. Api e Worker chamam so' este metodo e
/// nao precisam saber que existe Npgsql, Polly ou HttpClient atras.
/// </summary>
public static class ServicosDaInfraestrutura
{
    /// <summary>
    /// Desliga a negociação de GSS quando quem configurou não disse nada a respeito.
    ///
    /// <para>O Npgsql tenta criptografia GSS por padrão, e para isso carrega
    /// <c>libgssapi_krb5</c>. As imagens deste projeto são <c>chiseled</c> — sem gerenciador
    /// de pacotes e sem essa biblioteca —, por coerência com o
    /// <c>InvariantGlobalization=true</c> decidido na Etapa 00. O resultado é uma linha
    /// <c>Error: libgssapi_krb5.so.2: cannot open shared object file</c> em <b>toda</b>
    /// subida de contêiner.</para>
    ///
    /// <para>A conexão funciona: o Npgsql cai para o caminho sem GSS sozinho. Mas o log diz
    /// "Error" para uma coisa que não é erro — e neste projeto isso é defeito conhecido, não
    /// ruído tolerável: foi um <c>Falhas: 0</c> tecnicamente correto e completamente
    /// enganoso que produziu o incidente de 2026-08-21. Alguém depurando uma falha de
    /// verdade não pode gastar tempo nesta linha.</para>
    ///
    /// <para>Só preenche o que estiver <b>ausente</b>: quem precisar de Kerberos declara na
    /// própria string e a escolha é respeitada.</para>
    /// </summary>
    private static string NormalizarConexao(string stringDeConexao)
    {
        // A string crua, e nao ContainsKey do construtor: o construtor responde `true` para
        // chave conhecida mesmo quando ela nao foi informada, entao a condicao nunca era
        // satisfeita e a normalizacao nao acontecia. Medido — a linha do krb5 continuou
        // aparecendo depois da primeira versao deste metodo.
        var jaDeclarado = stringDeConexao.Contains("gss", StringComparison.OrdinalIgnoreCase);

        if (jaDeclarado)
        {
            return stringDeConexao;
        }

        var construtor = new Npgsql.NpgsqlConnectionStringBuilder(stringDeConexao)
        {
            GssEncryptionMode = Npgsql.GssEncryptionMode.Disable,
        };

        return construtor.ConnectionString;
    }

    public static IServiceCollection AdicionarInfraestrutura(
        this IServiceCollection servicos,
        string stringDeConexao)
    {
        servicos.AdicionarPersistencia(NormalizarConexao(stringDeConexao));
        servicos.AdicionarIntegracoes();

        servicos.AddScoped<IRepositorioDeIngestao, RepositorioDeIngestao>();
        servicos.AddScoped<IRepositorioDeCheckpoints, RepositorioDeCheckpoints>();
        servicos.AddSingleton<IRelogio, RelogioDoSistema>();

        servicos.AddScoped<IRepositorioDeVarredura, RepositorioDeVarredura>();

        // A eleição, do TSE. A fonte são arquivos numa pasta, e quem a aponta é o comando
        // `importar-tse` — por isso só o repositório entra aqui.
        servicos.AddScoped<Application.Eleicoes.IRepositorioDoTse, RepositorioDoTse>();
        servicos.AddScoped<IRepositorioDeRede, RepositorioDeRede>();
        servicos.AddScoped<IRepositorioDeRetencao, RepositorioDeRetencao>();
        servicos.AddSingleton<IRelogioDaRetencao, RelogioDaRetencaoDoSistema>();
        servicos.AddSingleton<IRelogioDaVarredura, RelogioDaVarreduraDoSistema>();
        servicos.AddSingleton(MotorDeRegras.Padrao());
        servicos.AddSingleton(MotorDeRede.Padrao());

        servicos.AddScoped<ServicoDeIngestao>();
        servicos.AddScoped<ServicoDeColeta>();
        servicos.AddScoped<ServicoDeVarredura>();
        servicos.AddScoped<ServicoDeVarreduraDeRede>();
        servicos.AddScoped<ServicoDeRetencao>();

        // O grafo do Estado. A fonte e' registrada pelo TIPO CONCRETO e ligada a porta
        // logo em seguida: nao ha' uma segunda implementacao disputando o registro como
        // acontece entre Camara e Senado, mas o cliente precisa do HttpClient da fabrica.
        servicos.AddScoped<IRepositorioDoEstado, RepositorioDoEstado>();
        servicos.AddScoped<ServicoDeColetaDoEstado>();

        // O lado de leitura do grafo. O cache aqui nao e' otimizacao: montar o mapa custa
        // carregar mais de cem mil arestas, e a estrutura do Estado muda em semanas.
        servicos.AddScoped<IRepositorioDePessoal, RepositorioDePessoal>();
        servicos.AddScoped<ServicoDeColetaDePessoal>();

        servicos.AddScoped<ConsultasDoEstadoEfCore>();
        servicos.AddScoped<IConsultasDoEstado>(provedor => new ConsultasDoEstadoComCache(
            provedor.GetRequiredService<ConsultasDoEstadoEfCore>(),
            provedor.GetRequiredService<HybridCache>()));

        // O mapa de fornecedores compartilhados — a vista de pessoas do mesmo contexto.
        // Cache obrigatorio: montar o mapa varre a tabela de despesas inteira.
        // O mapa de poder. Sem cache: sao ~100 linhas numa consulta sem juncao, e o
        // dado muda quando um coletor roda — poe-lo atras de seis horas de cache faria a
        // troca de um ministro levar seis horas para aparecer, sem ganho nenhum.
        servicos.AddScoped<IRepositorioDeCadeiras, RepositorioDeCadeiras>();
        servicos.AddScoped<ServicoDeColetaDeCadeiras>();
        servicos.AddScoped<IConsultasDeCadeiras, ConsultasDeCadeirasEfCore>();

        servicos.AddScoped<ConsultasDaRedeEfCore>();
        servicos.AddScoped<IConsultasDaRede>(provedor => new ConsultasDaRedeComCache(
            provedor.GetRequiredService<ConsultasDaRedeEfCore>(),
            provedor.GetRequiredService<HybridCache>()));

        // A passada mensal do ADR-0010 §4. Nao tem tabela propria: a agenda le a data da
        // varredura mais antiga em scores_risco, que a propria varredura grava.
        servicos.AddScoped<IRepositorioDoCiclo, RepositorioDoCiclo>();
        servicos.AddScoped<AgendaDoCicloMensal>();

        // As duas casas. A coleta registrada como ServicoDeColeta e' a da Camara (a fonte
        // padrao de IFonteDeParlamentares); a do Senado e' montada aqui sobre o ClienteDoSenado,
        // do mesmo jeito que o comando `coletar-senado` a monta.
        servicos.AddScoped(provedor =>
        {
            var senado = provedor.GetRequiredService<ClienteDoSenado>();

            return new ColetasDoCiclo(
            [
                provedor.GetRequiredService<ServicoDeColeta>(),
                new ServicoDeColeta(
                    senado,
                    new ServicoDeIngestao(
                        senado,
                        provedor.GetRequiredService<IFonteDeCadastroEmpresarial>(),
                        provedor.GetRequiredService<IRepositorioDeIngestao>()),
                    provedor.GetRequiredService<IRepositorioDeCheckpoints>(),
                    provedor.GetRequiredService<IRelogio>()),
            ]);
        });
        servicos.AddScoped<ServicoDeCicloMensal>();

        // O lado de leitura, com cache por cima. Registrado aqui e nao no projeto Api
        // porque a Api e' so' composicao: quem sabe que a consulta roda sobre EF Core, e
        // que o ranking e' cacheado, e' a infraestrutura.
        servicos.AddScoped<ConsultasPublicasEfCore>();
        servicos.AddScoped<IConsultasPublicas>(provedor => new ConsultasComCache(
            provedor.GetRequiredService<ConsultasPublicasEfCore>(),
            provedor.GetRequiredService<HybridCache>()));

        // Sem Redis configurado, o HybridCache roda so' em memoria — que e' o certo para
        // desenvolvimento e teste. Em producao a string de conexao liga o distribuido sem
        // que nenhuma consulta mude.
        var redis = Environment.GetEnvironmentVariable("EXPLANANDO_REDIS");

        if (!string.IsNullOrWhiteSpace(redis))
        {
            servicos.AddStackExchangeRedisCache(opcoes => opcoes.Configuration = redis);
        }

#pragma warning disable EXTEXP0018 // HybridCache continua em pre-visualizacao no .NET 10.
        servicos.AddHybridCache();
#pragma warning restore EXTEXP0018

        return servicos;
    }
}
