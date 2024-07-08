using Microsoft.EntityFrameworkCore;

namespace GeotecnologiaKNS.Repositories
{
    public class CartografiaRepository : ICartografiaRepository
    {
        private readonly ApplicationDbContext _context;

        public CartografiaRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public IEnumerable<Cartografia> GetCartografiasByPropriedade(int propriedadeId)
        {
            return _context.Cartografias
                .AsNoTracking()
                .Include(x => x.Propriedade)
                .Where(x => x.PropriedadeId == propriedadeId)
                .ToList();
        }
    }
}