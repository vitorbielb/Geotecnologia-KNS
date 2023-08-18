namespace GeotecnologiaKNS.Repositories.Interfaces
{
    public interface ISolicitacaoRepository
    {
        IEnumerable<Solicitacao> GetSolicitacoesByProdutor(int produtorId);
        IEnumerable<Solicitacao> GetSolicitacoesByPropriedade(int propriedadeId);
    }
}