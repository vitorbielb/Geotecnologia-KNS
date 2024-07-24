using GeotecnologiaKNS.Data;
using GeotecnologiaKNS.Models;
using GeotecnologiaKNS.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GeotecnologiaKNS.Repositories
{
    public class ProdutorRepository : IProdutorRepository
    {
        private readonly ApplicationDbContext _context;

        public ProdutorRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public IEnumerable<Produtor> ObterTodos()
        {
            return _context.Produtores
                .AsNoTracking()
                .ToList();
        }

        public Produtor? ObterPorId(int id)
        {
            return _context.Produtores
                .AsNoTracking()
                .FirstOrDefault(p => p.Id == id);
        }

        public void CadastrarProdutor(Produtor produtor)
        {
            _context.Produtores.Add(produtor);
            _context.SaveChanges();
        }

        public void AtualizarProdutor(Produtor produtor)
        {
            _context.Produtores.Update(produtor);
            _context.SaveChanges();
        }

        public void RemoverProdutor(Produtor produtor)
        {
            _context.Produtores.Remove(produtor);
            _context.SaveChanges();
        }
    }
}