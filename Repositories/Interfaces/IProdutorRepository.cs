using GeotecnologiaKNS.Models;

namespace GeotecnologiaKNS.Repositories.Interfaces
{
    public interface IProdutorRepository
    {
        IEnumerable<Produtor> ObterTodos();
        Produtor ObterPorId(int id);
        void CadastrarProdutor(Produtor produtor);
        void AtualizarProdutor(Produtor produtor);
        void RemoverProdutor(Produtor produtor);
        
    }
}
