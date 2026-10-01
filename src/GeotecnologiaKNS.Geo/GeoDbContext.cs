using GeotecnologiaKNS.Geo.Entities;
using Microsoft.EntityFrameworkCore;

namespace GeotecnologiaKNS.Geo;

/// <summary>
/// Contexto das bases geoespaciais de referência (PostGIS).
/// Separado do ApplicationDbContext de propósito: são dados públicos,
/// compartilhados entre tenants, com ciclo de atualização próprio.
/// </summary>
public class GeoDbContext : DbContext
{
    public const string Schema = "geo";

    /// <summary>WGS84. Todas as geometrias são normalizadas para este SRID na ingestão.</summary>
    public const int Srid = 4326;

    public GeoDbContext(DbContextOptions<GeoDbContext> options) : base(options)
    {
    }

    public DbSet<ImovelCar> ImoveisCar => Set<ImovelCar>();

    public DbSet<CargaBaseCar> Cargas => Set<CargaBaseCar>();

    public DbSet<CoberturaMunicipio> Cobertura => Set<CoberturaMunicipio>();

    public DbSet<LacunaCobertura> Lacunas => Set<LacunaCobertura>();

    public DbSet<CamadaReferencia> Camadas => Set<CamadaReferencia>();

    public DbSet<FeicaoReferencia> Feicoes => Set<FeicaoReferencia>();

    public DbSet<RestricaoDocumento> RestricoesPorDocumento => Set<RestricaoDocumento>();

    public DbSet<ListaRestritiva> ListasRestritivas => Set<ListaRestritiva>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.HasPostgresExtension("postgis");

        modelBuilder.Entity<ImovelCar>(entity =>
        {
            entity.ToTable("imovel_car");
            entity.HasKey(x => x.CodigoCar);

            entity.Property(x => x.CodigoCar).HasColumnName("codigo_car").HasMaxLength(100);
            entity.Property(x => x.Perimetro).HasColumnName("perimetro").HasColumnType($"geometry(Geometry,{Srid})").IsRequired();
            entity.Property(x => x.Centroide).HasColumnName("centroide").HasColumnType($"geometry(Point,{Srid})");
            entity.Property(x => x.AreaHa).HasColumnName("area_ha");
            entity.Property(x => x.AreaCalculadaHa).HasColumnName("area_calculada_ha");
            entity.Property(x => x.Municipio).HasColumnName("municipio").HasMaxLength(150);
            entity.Property(x => x.Uf).HasColumnName("uf").HasMaxLength(2);
            entity.Property(x => x.CodigoIbge).HasColumnName("codigo_ibge").HasMaxLength(7);
            entity.Property(x => x.Situacao).HasColumnName("situacao").HasMaxLength(50);
            entity.Property(x => x.Tipo).HasColumnName("tipo").HasMaxLength(20);
            entity.Property(x => x.AtualizadoEmOrigem).HasColumnName("atualizado_em_origem");
            entity.Property(x => x.CargaId).HasColumnName("carga_id");

            // Índice espacial: sem ele, cruzar contra camadas nacionais é inviável.
            entity.HasIndex(x => x.Perimetro).HasMethod("gist");
            entity.HasIndex(x => x.Uf);

            entity.HasOne(x => x.Carga)
                  .WithMany(x => x.Imoveis)
                  .HasForeignKey(x => x.CargaId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RestricaoDocumento>(entity =>
        {
            entity.ToTable("restricao_documento");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Documento).HasColumnName("documento").HasMaxLength(14).IsRequired();
            entity.Property(x => x.NomeTitular).HasColumnName("nome_titular").HasMaxLength(250);
            entity.Property(x => x.Tipo).HasColumnName("tipo").HasConversion<int>();
            entity.Property(x => x.Origem).HasColumnName("origem").HasMaxLength(200).IsRequired();
            entity.Property(x => x.Referencia).HasColumnName("referencia").HasMaxLength(60);
            entity.Property(x => x.Municipio).HasColumnName("municipio").HasMaxLength(150);
            entity.Property(x => x.Uf).HasColumnName("uf").HasMaxLength(2);
            entity.Property(x => x.DataRestricao).HasColumnName("data_restricao").HasMaxLength(40);
            entity.Property(x => x.TemGeometria).HasColumnName("tem_geometria");
            entity.Property(x => x.CarregadoEm).HasColumnName("carregado_em");
            entity.Property(x => x.Versao).HasColumnName("versao");

            // A consulta e sempre por documento exato; sem indice, cada analise
            // varreria as cem mil linhas da lista.
            // A consulta filtra pela versão publicada, então ela entra no índice:
            // durante a recarga as duas versões convivem na tabela.
            entity.HasIndex(x => new { x.Documento, x.Tipo, x.Versao }).HasDatabaseName("ix_restricao_documento");
        });

        modelBuilder.Entity<ListaRestritiva>(entity =>
        {
            entity.ToTable("lista_restritiva");
            entity.HasKey(x => x.Tipo);

            entity.Property(x => x.Tipo).HasColumnName("tipo").HasConversion<int>();
            entity.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(200).IsRequired();
            entity.Property(x => x.Origem).HasColumnName("origem").HasMaxLength(200).IsRequired();
            entity.Property(x => x.VersaoAtual).HasColumnName("versao_atual");
            entity.Property(x => x.TotalRegistros).HasColumnName("total_registros");
            entity.Property(x => x.AtualizadaEm).HasColumnName("atualizada_em");
            entity.Property(x => x.PeriodicidadeDias).HasColumnName("periodicidade_dias");
        });

        modelBuilder.Entity<CoberturaMunicipio>(entity =>
        {
            entity.ToTable("cobertura_municipio");
            entity.HasKey(x => x.CodigoIbge);

            entity.Property(x => x.CodigoIbge).HasColumnName("codigo_ibge").HasMaxLength(7);
            entity.Property(x => x.Uf).HasColumnName("uf").HasMaxLength(2).IsRequired();
            entity.Property(x => x.Municipio).HasColumnName("municipio").HasMaxLength(150);
            entity.Property(x => x.Imoveis).HasColumnName("imoveis");
            entity.Property(x => x.CargaId).HasColumnName("carga_id");
            entity.Property(x => x.CobertoEm).HasColumnName("coberto_em");

            entity.HasIndex(x => x.Uf);

            entity.HasOne(x => x.Carga)
                  .WithMany()
                  .HasForeignKey(x => x.CargaId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LacunaCobertura>(entity =>
        {
            entity.ToTable("lacuna_cobertura");

            // Chave por município e inquilino: permite contar quantos clientes
            // distintos pedem o mesmo município, que é o critério de prioridade.
            entity.HasKey(x => new { x.CodigoIbge, x.TenantId });

            entity.Property(x => x.CodigoIbge).HasColumnName("codigo_ibge").HasMaxLength(7);
            entity.Property(x => x.TenantId).HasColumnName("tenant_id");
            entity.Property(x => x.Uf).HasColumnName("uf").HasMaxLength(2).IsRequired();
            entity.Property(x => x.UltimoCodigoCar).HasColumnName("ultimo_codigo_car").HasMaxLength(100);
            entity.Property(x => x.Consultas).HasColumnName("consultas");
            entity.Property(x => x.PrimeiraEm).HasColumnName("primeira_em");
            entity.Property(x => x.UltimaEm).HasColumnName("ultima_em");

            entity.HasIndex(x => x.UltimaEm);
        });

        modelBuilder.Entity<CamadaReferencia>(entity =>
        {
            entity.ToTable("camada_referencia");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Chave).HasColumnName("chave").HasMaxLength(80).IsRequired();
            entity.Property(x => x.Nome).HasColumnName("nome").HasMaxLength(200).IsRequired();
            entity.Property(x => x.Tipo).HasColumnName("tipo").HasConversion<int>();
            entity.Property(x => x.Origem).HasColumnName("origem").HasMaxLength(200).IsRequired();
            entity.Property(x => x.AnoReferencia).HasColumnName("ano_referencia");
            entity.Property(x => x.Ativa).HasColumnName("ativa");
            entity.Property(x => x.AtualizadaEm).HasColumnName("atualizada_em");
            entity.Property(x => x.TotalFeicoes).HasColumnName("total_feicoes");
            entity.Property(x => x.VersaoAtual).HasColumnName("versao_atual");
            entity.Property(x => x.PeriodicidadeDias).HasColumnName("periodicidade_dias");

            entity.HasIndex(x => x.Chave).IsUnique();
            entity.HasIndex(x => x.Tipo);
        });

        modelBuilder.Entity<FeicaoReferencia>(entity =>
        {
            entity.ToTable("feicao_referencia");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.CamadaId).HasColumnName("camada_id");
            entity.Property(x => x.Geometria).HasColumnName("geometria").HasColumnType($"geometry(Geometry,{Srid})").IsRequired();
            entity.Property(x => x.AtributosJson).HasColumnName("atributos").HasColumnType("jsonb");
            entity.Property(x => x.Rotulo).HasColumnName("rotulo").HasMaxLength(300);
            entity.Property(x => x.Versao).HasColumnName("versao");

            // A consulta de sobreposição filtra pela versão publicada; sem o
            // índice combinado, a carga seguinte deixaria o cruzamento lento
            // enquanto as duas versões convivem na tabela.
            entity.HasIndex(x => new { x.CamadaId, x.Versao }).HasDatabaseName("ix_feicao_camada_versao");

            // O índice espacial é o que torna o cruzamento viável: sem ele cada
            // análise varreria milhões de polígonos nacionais.
            entity.HasIndex(x => x.Geometria).HasMethod("gist");

            entity.HasOne(x => x.Camada)
                  .WithMany(x => x.Feicoes)
                  .HasForeignKey(x => x.CamadaId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CargaBaseCar>(entity =>
        {
            entity.ToTable("carga_base_car");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Origem).HasColumnName("origem").HasMaxLength(200).IsRequired();
            entity.Property(x => x.Arquivo).HasColumnName("arquivo").HasMaxLength(400).IsRequired();
            entity.Property(x => x.HashArquivo).HasColumnName("hash_arquivo").HasMaxLength(64);
            entity.Property(x => x.Uf).HasColumnName("uf").HasMaxLength(2);
            entity.Property(x => x.IniciadaEm).HasColumnName("iniciada_em");
            entity.Property(x => x.ConcluidaEm).HasColumnName("concluida_em");
            entity.Property(x => x.RegistrosLidos).HasColumnName("registros_lidos");
            entity.Property(x => x.RegistrosGravados).HasColumnName("registros_gravados");
            entity.Property(x => x.RegistrosDescartados).HasColumnName("registros_descartados");
            entity.Property(x => x.Status).HasColumnName("status").HasConversion<int>();
            entity.Property(x => x.Erro).HasColumnName("erro");
        });
    }
}
