using Explanando.Domain.Cadeiras;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Explanando.Infrastructure.Persistencia.Configuracoes;

/// <summary>
/// A tabela das cadeiras de comando.
///
/// <para><b>É a única tabela do sistema com nome de pessoa, e a forma dela registra
/// isso.</b> <c>fonte</c> e <c>verificado_em</c> são obrigatórios: uma linha sem o
/// endereço de onde o governo publicou aquele nome não pode existir no banco, e o
/// <c>NOT NULL</c> é o que torna a regra do domínio impossível de contornar por uma
/// escrita direta em SQL.</para>
/// </summary>
internal sealed class CadeiraConfiguracao : IEntityTypeConfiguration<CadeiraDeComando>
{
    public void Configure(EntityTypeBuilder<CadeiraDeComando> builder)
    {
        builder.ToTable("cadeiras_de_comando");
        builder.HasKey(cadeira => cadeira.Id);

        builder.Property(cadeira => cadeira.Coletor).HasMaxLength(40).IsRequired();
        builder.Property(cadeira => cadeira.Poder).IsRequired();
        builder.Property(cadeira => cadeira.Camada).IsRequired();

        builder.Property(cadeira => cadeira.Cargo).HasMaxLength(240).IsRequired();

        // Nulo e' cadeira VAGA, e nao "nao sabemos" — ver NovaCadeira.Ocupante. Por isso a
        // coluna e' anulavel de proposito, e nao por falta de cuidado.
        builder.Property(cadeira => cadeira.Ocupante).HasMaxLength(200);

        builder.Property(cadeira => cadeira.Orgao).HasMaxLength(240);
        builder.Property(cadeira => cadeira.CodigoSiorg);
        builder.Property(cadeira => cadeira.Partido).HasMaxLength(40);
        builder.Property(cadeira => cadeira.Uf).HasMaxLength(2);
        builder.Property(cadeira => cadeira.FotoUrl).HasMaxLength(500);

        builder.Property(cadeira => cadeira.Fonte).HasMaxLength(500).IsRequired();
        builder.Property(cadeira => cadeira.Origem).IsRequired();
        builder.Property(cadeira => cadeira.VerificadoEm).IsRequired();

        // A consulta da tela e' sempre "as cadeiras deste poder, em ordem de camada".
        builder
            .HasIndex(cadeira => new { cadeira.Poder, cadeira.Camada })
            .HasDatabaseName("ix_cadeiras_poder_camada");

        // E a escrita e' sempre "apague tudo deste coletor e grave de novo".
        builder
            .HasIndex(cadeira => cadeira.Coletor)
            .HasDatabaseName("ix_cadeiras_coletor");

        // Liga o mapa de poder ao mapa de instituicoes, quando a ponte por nome resolve.
        builder
            .HasIndex(cadeira => cadeira.CodigoSiorg)
            .HasDatabaseName("ix_cadeiras_siorg");
    }
}
