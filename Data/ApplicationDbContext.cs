using GeotecnologiaKNS.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace GeotecnologiaKNS.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options): base(options)
        {

        }
        public DbSet<Produtor> Produtores { get; set; }
        public DbSet<Propriedade> Propriedades { get; set;}
    }
}