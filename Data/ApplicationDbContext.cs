using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using GeotecnologiaKNS.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace GeotecnologiaKNS.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
       : base(options)
        {
        }


        public DbSet<Propriedade> Propriedades { get; set; } = default!;
        public DbSet<Produtor> Produtores { get; set; }
        public DbSet<PropriedadeArquivo> PropriedadesArquivos { get; set; }
        public DbSet<ProdutorArquivo> ProdutoresArquivos { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Propriedade>()
                      .HasMany(x => x.Documentos);

            modelBuilder.Entity<Produtor>()
                        .HasMany(x => x.Documentos);

            base.OnModelCreating(modelBuilder);
        }
        public DbSet<Solicitacao>? Solicitacao { get; set; }
    }
}
