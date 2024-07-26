using GeotecnologiaKNS.Data;
using GeotecnologiaKNS.Models;
using GeotecnologiaKNS.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace GeotecnologiaKNS.Repositories
{
    public class PropriedadeRepository : IPropriedadeRepository
    {
        private readonly ApplicationDbContext _context;

        public PropriedadeRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public IEnumerable<Propriedade> ObterTodasPropriedades()
        {
            return _context.Propriedades
                .AsNoTracking()
                .ToList();
        }

        public Propriedade? ObterPropriedadePorId(int id)
        {
            return _context.Propriedades
                .AsNoTracking()
                .FirstOrDefault(p => p.Id == id);
        }

        public void CadastrarPropriedade(Propriedade propriedade)
        {
            _context.Propriedades.Add(propriedade);
            _context.SaveChanges();
        }

        public void AtualizarPropriedade(Propriedade propriedade)
        {
            _context.Propriedades.Update(propriedade);
            _context.SaveChanges();
        }

        public void RemoverPropriedade(Propriedade propriedade)
        {
            _context.Propriedades.Remove(propriedade);
            _context.SaveChanges();
        }

        public void Save(Propriedade propriedade)
        {
            if (propriedade is null)
                return;

            _context.Propriedades.Add(propriedade);
            _context.SaveChanges();
        }
    }
}