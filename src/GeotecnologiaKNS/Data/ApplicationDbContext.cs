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

            modelBuilder.Entity<Propriedade>()
                .HasOne(x => x.Geozone);

            modelBuilder.Entity<Propriedade>()
                .HasMany(x => x.Documentos);

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

            modelBuilder.Entity<ApplicationUser>()
                .HasQueryFilter(x => !_userContext.TenantId.HasValue || x.TenantId == _userContext.TenantId);

            modelBuilder.Entity<ApplicationUser>()
                .HasOne(e => e.Industria)
                .WithMany(c => c.Usuarios)
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ApplicationRole>()
                .HasQueryFilter(x => !_userContext.TenantId.HasValue ||
                                     _userContext.IsApplicationAdmin.GetValueOrDefault() ||
                                     (x.Name != nameof(Infra.Roles.Administrador) && _userContext.IsTenantAdmin.GetValueOrDefault() && x.TenantId == _userContext.TenantId));

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
