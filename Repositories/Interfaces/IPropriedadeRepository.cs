using System.Collections.Generic;
using GeotecnologiaKNS.Models;

namespace GeotecnologiaKNS.Repositories.Interfaces
{
    public interface IPropriedadeRepository
    {
        IEnumerable<Propriedade> ObterTodasPropriedades();
        Propriedade ObterPropriedadePorId(int id);
        void CadastrarPropriedade(Propriedade propriedade);
        void AtualizarPropriedade(Propriedade propriedade);
        void RemoverPropriedade(Propriedade propriedade);
        void Save(Propriedade propriedade);
    }
}

