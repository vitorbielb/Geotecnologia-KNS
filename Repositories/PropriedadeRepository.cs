using System.Collections.Generic;
using System.Linq;
using GeotecnologiaKNS.Data;
using GeotecnologiaKNS.Models;
using GeotecnologiaKNS.Repositories.Interfaces;

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
            return _context.Propriedade.ToList();
        }

        public Propriedade ObterPropriedadePorId(int id)
        {
            return _context.Propriedade.FirstOrDefault(p => p.Id == id);
        }

        public void CadastrarPropriedade(Propriedade propriedade)
        {
            _context.Propriedade.Add(propriedade);
            _context.SaveChanges();
        }

        public void AtualizarPropriedade(Propriedade propriedade)
        {
            _context.Propriedade.Update(propriedade);
            _context.SaveChanges();
        }

        public void RemoverPropriedade(Propriedade propriedade)
        {
            _context.Propriedade.Remove(propriedade);
            _context.SaveChanges();
        }
        public void Save(Propriedade propriedade)
        {
            if (propriedade != null)
            {
                _context.Propriedade.Add(propriedade);
                _context.SaveChanges();
            }
        }
    }
}

