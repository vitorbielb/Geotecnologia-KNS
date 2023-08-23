using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Security.Claims;

namespace GeotecnologiaKNS.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
       : base(options)
        {
        }

        public DbSet<Industria> Industrias { get; set; } = default!;
        public DbSet<Propriedade> Propriedades { get; set; } = default!;
        public DbSet<Produtor> Produtores { get; set; } = default!;
        public DbSet<PropriedadeArquivo> PropriedadesArquivos { get; set; } = default!;
        public DbSet<ProdutorArquivo> ProdutoresArquivos { get; set; } = default!;
        public DbSet<Solicitacao>? Solicitacao { get; set; } = default!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Propriedade>()
                .HasMany(x => x.Documentos);

            modelBuilder.Entity<Propriedade>()
                .HasOne(e => e.Industria)
                .WithMany(c => c.Propriedades)
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent cascading delete

            modelBuilder.Entity<Produtor>()
                .HasMany(x => x.Documentos);

            modelBuilder.Entity<Produtor>()
                .HasOne(e => e.Industria)
                .WithMany(c => c.Produtores)
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent cascading delete

            modelBuilder.Entity<Solicitacao>()
                .HasOne(e => e.Industria)
                .WithMany(c => c.Solicitacoes)
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent cascading delete

            modelBuilder.Entity<ApplicationUser>()
                .HasOne(e => e.Industria)
                .WithMany(c => c.Usuarios)
                .HasForeignKey(e => e.TenantId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent cascading delete

            base.OnModelCreating(modelBuilder);
        }
    }
}
