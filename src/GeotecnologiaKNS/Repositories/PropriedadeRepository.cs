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

        public IEnumerable<PropriedadeNoMapa> ObterParaMapa()
        {
            // A situação do imóvel é o veredito da análise mais recente dele.
            // Imóvel sem solicitação alguma fica com situação nula, que o mapa
            // pinta de cinza: nunca analisado não é o mesmo que liberado, e a
            // cor é o que mais rápido comunica isso.
            return _context.Propriedades
                .AsNoTracking()
                .Select(p => new PropriedadeNoMapa(
                    p.NomePropriedade,
                    p.Municipio,
                    p.Latitude,
                    p.Longitude,
                    p.PerimetroGeoJson,
                    _context.Solicitacao
                        .Where(s => s.PropriedadeId == p.Id)
                        .OrderByDescending(s => s.DataAnalise ?? s.DataSolicitacao)
                        .ThenByDescending(s => s.Id)
                        .Select(s => (Status?)s.Status)
                        .FirstOrDefault()))
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