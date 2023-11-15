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

        public IEnumerable<Models.Propriedade> ObterTodasPropriedades()
        {
            return _context.Propriedades.ToList();
        }

        public Models.Propriedade ObterPropriedadePorId(int id)
        {
            return _context.Propriedades.FirstOrDefault(p => p.Id == id);
        }

        public void CadastrarPropriedade(Models.Propriedade propriedade)
        {
            _context.Propriedades.Add(propriedade);
            _context.SaveChanges();
        }

        public void AtualizarPropriedade(Models.Propriedade propriedade)
        {
            _context.Propriedades.Update(propriedade);
            _context.SaveChanges();
        }

        public void RemoverPropriedade(Models.Propriedade propriedade)
        {
            _context.Propriedades.Remove(propriedade);
            _context.SaveChanges();
        }
        public void Save(Models.Propriedade propriedade)
        {
            if (propriedade != null)
            {
                _context.Propriedades.Add(propriedade);
                _context.SaveChanges();
            }
        }
    }
}

