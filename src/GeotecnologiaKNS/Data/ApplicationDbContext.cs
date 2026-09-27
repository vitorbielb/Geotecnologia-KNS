using GeotecnologiaKNS.Areas.Identity.Pages.Account;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
#pragma warning disable CS8618

namespace GeotecnologiaKNS.Data
{
    public class ApplicationDbContext : IdentityDbContext
        <ApplicationUser
        , ApplicationRole
        , string
        , IdentityUserClaim<string>
        , IdentityUserRole<string>
        , IdentityUserLogin<string>
        , IdentityRoleClaim<string>
        , IdentityUserToken<string>>
    {
        private readonly IUserContext _userContext;

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IUserContext userContext)
            : base(options) => _userContext = userContext;

        public DbSet<Industria> Industrias { get; set; }
        public DbSet<Propriedade> Propriedades { get; set; }
        public DbSet<Produtor> Produtores { get; set; }
        public DbSet<PropriedadeArquivo> PropriedadesArquivos { get; set; }
        public DbSet<ProdutorArquivo> ProdutoresArquivos { get; set; }
        public DbSet<AnaliseArquivo> AnalisesArquivos { get; set; }
        public DbSet<Solicitacao> Solicitacao { get; set; }
        public DbSet<Geozone> Geozones { get; set; }
        public DbSet<CartografiaArquivo> CartografiasArquivos { get; set; }
        public DbSet<Cartografia> Cartografias { get; set; }
        public DbSet<AnaliseAutomatica> AnalisesAutomaticas { get; set; }
        public DbSet<AnaliseOcorrencia> AnalisesOcorrencias { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            var keysProperties = modelBuilder.Model.GetEntityTypes().Select(x => x.FindPrimaryKey()).SelectMany(x => x.Properties);
            foreach (var property in keysProperties)
            {
                property.ValueGenerated = ValueGenerated.OnAdd;
            }
       
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Propriedade>()
                .HasQueryFilter(x => !_userContext.TenantId.HasValue || x.TenantId == _userContext.TenantId);

            // Mapeamento mantido só para preservar os polígonos desenhados à mão
            // até a limpeza de schema. O perímetro corrente vem da base do CAR.
#pragma warning disable CS0618
            modelBuilder.Entity<Propriedade>()
                .HasOne(x => x.Geozone);
#pragma warning restore CS0618

            modelBuilder.Entity<Propriedade>()
                .HasMany(x => x.Documentos);

            // Busca por CAR é o caminho quente do cadastro e da reanálise.
            modelBuilder.Entity<Propriedade>()
                .HasIndex(x => x.CodigoCar);

            // O nome é opcional no formulário, mas nunca chega nulo ao banco:
            // quando vem em branco, o serviço do CAR gera um. Declarar
            // obrigatório aqui mantém a coluna NOT NULL sem tornar o campo
            // exigido na tela.
            modelBuilder.Entity<Propriedade>()
                .Property(x => x.NomePropriedade)
                .IsRequired();

            modelBuilder.Entity<Propriedade>()
                .HasOne(e => e.Industria)
                .WithMany(c => c.Propriedades)
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Produtor>()
                .HasQueryFilter(x => !_userContext.TenantId.HasValue || x.TenantId == _userContext.TenantId);

            modelBuilder.Entity<Produtor>()
                .HasMany(x => x.Documentos);

            modelBuilder.Entity<Produtor>()
                .HasOne(e => e.Industria)
                .WithMany(c => c.Produtores)
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Solicitacao>()
                .HasQueryFilter(x => !_userContext.TenantId.HasValue || x.TenantId == _userContext.TenantId);

            modelBuilder.Entity<Solicitacao>()
                .HasOne(e => e.Industria)
                .WithMany(c => c.Solicitacoes)
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Cartografia>()
                .HasQueryFilter(x => !_userContext.TenantId.HasValue || x.TenantId == _userContext.TenantId);

            modelBuilder.Entity<Cartografia>()
                .HasOne(e => e.Industria)
                .WithMany(c => c.Cartografias)
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Cartografia>()
                .HasMany(x => x.Arquivos);

            modelBuilder.Entity<AnaliseAutomatica>()
                .HasQueryFilter(x => !_userContext.TenantId.HasValue || x.TenantId == _userContext.TenantId);

            modelBuilder.Entity<AnaliseAutomatica>()
                .HasOne(e => e.Industria)
                .WithMany()
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AnaliseAutomatica>()
                .HasMany(x => x.Ocorrencias)
                .WithOne(x => x.Analise)
                .HasForeignKey(x => x.AnaliseId)
                .OnDelete(DeleteBehavior.Cascade);

            // Filtro espelhado no lado dependente: sem ele, uma ocorrência de
            // análise de outro tenant poderia ser lida por consulta direta.
            modelBuilder.Entity<AnaliseOcorrencia>()
                .HasQueryFilter(x => !_userContext.TenantId.HasValue || x.Analise!.TenantId == _userContext.TenantId);

            modelBuilder.Entity<ApplicationUser>()
                .HasQueryFilter(x => !_userContext.TenantId.HasValue || x.TenantId == _userContext.TenantId);

            modelBuilder.Entity<ApplicationUser>()
                .HasOne(e => e.Industria)
                .WithMany(c => c.Usuarios)
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ApplicationRole>()
                .HasQueryFilter(x => !_userContext.TenantId.HasValue ||
                                     _userContext.IsApplicationAdmin ||
                                     (x.Name != nameof(Infra.Roles.Administrador) && _userContext.IsTenantAdmin && x.TenantId == _userContext.TenantId));

            modelBuilder.Entity<ApplicationRole>()
                .HasMany(x => x.Claims)
                .WithOne()
                .HasForeignKey(e => e.RoleId)
                .HasConstraintName("RoleId");

            modelBuilder.Entity<ApplicationUser>()
                .HasMany(x => x.Claims)
                .WithOne()
                .HasForeignKey(e => e.UserId)
                .HasConstraintName("UserId");
        }
    }
}
