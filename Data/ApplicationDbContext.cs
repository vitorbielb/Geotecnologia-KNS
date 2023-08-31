using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
#pragma warning disable CS8618

namespace GeotecnologiaKNS.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IServiceProvider services)
            : base(options)
        {
            foreach (IEntityHandler handler in services.GetServices<IEntityHandler>())
            {
                //SavedChanges += handler.Handle;
            }
        }

        public DbSet<Industria> Industrias { get; set; }
        public DbSet<Propriedade> Propriedades { get; set; }
        public DbSet<Produtor> Produtores { get; set; }
        public DbSet<PropriedadeArquivo> PropriedadesArquivos { get; set; }
        public DbSet<ProdutorArquivo> ProdutoresArquivos { get; set; }
        public DbSet<Solicitacao>? Solicitacao { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Propriedade>()
                .HasMany(x => x.Documentos);

            modelBuilder.Entity<Propriedade>()
                .HasOne(e => e.Industria)
                .WithMany(c => c.Propriedades)
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Produtor>()
                .HasMany(x => x.Documentos);

            modelBuilder.Entity<Produtor>()
                .HasOne(e => e.Industria)
                .WithMany(c => c.Produtores)
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Solicitacao>()
                .HasOne(e => e.Industria)
                .WithMany(c => c.Solicitacoes)
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ApplicationUser>()
                .HasOne(e => e.Industria)
                .WithMany(c => c.Usuarios)
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict);

            base.OnModelCreating(modelBuilder);
        }
    }
}
