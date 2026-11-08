using Explanando.Domain.Fornecedores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Explanando.Infrastructure.Persistencia.Configuracoes;

internal sealed class FornecedorConfiguracao : IEntityTypeConfiguration<Fornecedor>
{
    public void Configure(EntityTypeBuilder<Fornecedor> builder)
    {
        builder.ToTable("fornecedores");
        builder.HasKey(fornecedor => fornecedor.Id);

        builder
            .Property(fornecedor => fornecedor.Documento)
            .HasConversion(Conversores.CpfCnpj)
            .HasColumnName("documento")
            .HasMaxLength(14)
            .IsRequired();

        builder.Property(fornecedor => fornecedor.RazaoSocial).HasMaxLength(200).IsRequired();
        builder.Property(fornecedor => fornecedor.NomeFantasia).HasMaxLength(200);
        builder.Property(fornecedor => fornecedor.CnaePrincipal).HasMaxLength(20);

        // Lista primitiva -> text[] no Postgres. A Etapa 01 confirmou que a BrasilAPI
        // entrega os secundarios, e ignora-los produziria falso positivo em REG_KYB_001.
        builder
            .PrimitiveCollection<IReadOnlyList<string>>("CnaesSecundarios")
            .HasColumnName("cnaes_secundarios")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder
            .Property(fornecedor => fornecedor.SituacaoCadastral)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(fornecedor => fornecedor.DataSituacaoCadastral).HasColumnType("date");
        builder.Property(fornecedor => fornecedor.DataInicioAtividade).HasColumnType("date");

        // Tres estados de proposito: sim, nao e "a fonte nao informou". No CNPJ real da
        // amostra veio nulo, e colapsar isso em `false` faria REG_KYB_004 silenciar.
        builder.Property(fornecedor => fornecedor.OptantePeloMei);
        builder.Property(fornecedor => fornecedor.DataOpcaoPeloMei);
        builder.Property(fornecedor => fornecedor.DataExclusaoDoMei);
        builder.Property(fornecedor => fornecedor.Porte).HasMaxLength(50);

        builder.Ignore(fornecedor => fornecedor.EhMei);
        builder.Ignore(fornecedor => fornecedor.OpcaoPeloMeiConhecida);

        builder.OwnsOne(fornecedor => fornecedor.Endereco, endereco =>
        {
            endereco.Property(e => e.Logradouro).HasColumnName("endereco_logradouro").HasMaxLength(200);
            endereco.Property(e => e.Numero).HasColumnName("endereco_numero").HasMaxLength(20);
            endereco.Property(e => e.Bairro).HasColumnName("endereco_bairro").HasMaxLength(100);
            endereco.Property(e => e.Municipio).HasColumnName("endereco_municipio").HasMaxLength(100);
            endereco.Property(e => e.CodigoMunicipioIbge).HasColumnName("endereco_codigo_municipio_ibge");
            endereco.Property(e => e.Uf).HasColumnName("endereco_uf").HasMaxLength(2);
            endereco.Property(e => e.Cep).HasColumnName("endereco_cep").HasMaxLength(8);

            // REG_GEO_002 confronta a UF do fornecedor com a do parlamentar.
            endereco.HasIndex(e => e.Uf).HasDatabaseName("ix_fornecedores_endereco_uf");
        });

        builder.Navigation(fornecedor => fornecedor.Endereco).IsRequired();

        builder
            .HasIndex(fornecedor => fornecedor.Documento)
            .IsUnique()
            .HasDatabaseName("ix_fornecedores_documento");

        builder
            .HasMany(fornecedor => fornecedor.QuadroSocietario)
            .WithOne()
            .HasForeignKey(socio => socio.FornecedorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(fornecedor => fornecedor.QuadroSocietario)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class SociedadeConfiguracao : IEntityTypeConfiguration<Sociedade>
{
    public void Configure(EntityTypeBuilder<Sociedade> builder)
    {
        builder.ToTable("sociedades");
        builder.HasKey(socio => socio.Id);

        builder.Property(socio => socio.NomeSocio).HasMaxLength(200).IsRequired();

        // 14 caracteres cobrem a mascara com pontuacao ("***.456.789-**").
        // Os 5 digitos ocultos nunca chegaram ao sistema — nao ha o que proteger aqui.
        builder
            .Property(socio => socio.Cpf)
            .HasConversion(Conversores.CpfParcial)
            .HasColumnName("cpf_mascarado")
            .HasMaxLength(14)
            .IsRequired();

        builder.Property(socio => socio.Qualificacao).HasMaxLength(100);
        builder.Property(socio => socio.DataEntradaSociedade).HasColumnType("date");

        builder
            .HasIndex(socio => socio.FornecedorId)
            .HasDatabaseName("ix_sociedades_fornecedor_id");

        // REG_NET_001 procura socios por nome, ja que o CPF esta incompleto.
        builder
            .HasIndex(socio => socio.NomeSocio)
            .HasDatabaseName("ix_sociedades_nome_socio");
    }
}
