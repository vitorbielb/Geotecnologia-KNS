using GeotecnologiaKNS.Data;
using GeotecnologiaKNS.Models;
using GeotecnologiaKNS.Repositories.Interfaces;

namespace GeotecnologiaKNS.Repositories
{
    public class ProdutorRepository : IProdutorRepository
    {
        private readonly ApplicationDbContext _context;
        private Models.Produtor produtor;

        public ProdutorRepository(ApplicationDbContext context)
        {
            _context = context;
        }
        public IEnumerable<Models.Produtor> ObterTodos()
        {
            return _context.Produtores.ToList();
        }

        public Models.Produtor ObterPorId(int id)
        {
            return _context.Produtores.FirstOrDefault(p => p.Id == id);
        }

        public void CadastrarProdutor(Models.Produtor produtor)
        {
            _context.Produtores.Add(produtor);
            _context.SaveChanges();
        }

        public void RemoverProdutor(Models.Produtor produtor)
        {
            _context.Produtores.Remove(produtor);
            _context.SaveChanges();
        }
        

        public void AtualizarProdutor(Models.Produtor produtor)
        {
             _context.Produtores.Update(produtor);
            _context.SaveChanges();
        }

    }
}
