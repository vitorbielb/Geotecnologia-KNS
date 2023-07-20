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

        public DbSet<GeotecnologiaKNS.Models.Propriedade> Propriedade { get; set; } = default!;
        public DbSet<Produtor> Produtores { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Ignorar a classe que não deve ser mapeada para o banco de dados
            modelBuilder.Ignore<Dictionary<Estado, List<string>>>();

            base.OnModelCreating(modelBuilder);
        }
    }
}
