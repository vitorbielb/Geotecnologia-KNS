namespace GeotecnologiaKNS.Repositories.Interfaces
{
    public interface ISolicitacaoRepository
    {
        IEnumerable<Solicitacao> GetRecentsSolicitacoes();
        IEnumerable<Solicitacao> GetSolicitacoesByProdutor(int produtorId);
        IEnumerable<Solicitacao> GetSolicitacoesByPropriedade(int propriedadeId);
    }
}