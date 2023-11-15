using GeotecnologiaKNS.Models;

namespace GeotecnologiaKNS.Repositories.Interfaces
{
    public interface IProdutorRepository
    {
        IEnumerable<Models.Produtor> ObterTodos();
        Models.Produtor ObterPorId(int id);
        void CadastrarProdutor(Models.Produtor produtor);
        void AtualizarProdutor(Models.Produtor produtor);
        void RemoverProdutor(Models.Produtor produtor);
        
    }
}
