using Microsoft.EntityFrameworkCore;

namespace GeotecnologiaKNS.Repositories
{
    public class IndustriaRepository : IIndustriaRepository
    {
        private readonly ApplicationDbContext _context;
        public IndustriaRepository(ApplicationDbContext context)
        {
            _context = context;
        }
        public IEnumerable<Industria> GetIndustrias()
        {
            return _context.Industrias
                .AsNoTracking()
                .ToList();
        }
        public IAsyncEnumerable<Industria> GetIndustriasAsync()
        {
            return _context.Industrias
                .AsNoTracking()
                .AsAsyncEnumerable();
        }
    }
}