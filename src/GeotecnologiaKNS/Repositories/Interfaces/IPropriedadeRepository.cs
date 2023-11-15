using System.Collections.Generic;
using GeotecnologiaKNS.Models;

namespace GeotecnologiaKNS.Repositories.Interfaces
{
    public interface IPropriedadeRepository
    {
        IEnumerable<Models.Propriedade> ObterTodasPropriedades();
        Models.Propriedade ObterPropriedadePorId(int id);
        void CadastrarPropriedade(Models.Propriedade propriedade);
        void AtualizarPropriedade(Models.Propriedade propriedade);
        void RemoverPropriedade(Models.Propriedade propriedade);
        void Save(Models.Propriedade propriedade);
    }
}

