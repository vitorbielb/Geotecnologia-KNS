namespace GeotecnologiaKNS.Repositories
{
    public class SolicitacaoRepository : ISolicitacaoRepository
    {
        private readonly ApplicationDbContext _context;

        public SolicitacaoRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public IEnumerable<Solicitacao> GetSolicitacoesByPropriedade(int propriedadeId)
        {
            return _context.Solicitacao.Include(x => x.Propriedade).Where(x => x.PropriedadeId == propriedadeId);
        }

        public IEnumerable<Solicitacao> GetSolicitacoesByProdutor(int produtorId)
        {
            return _context.Solicitacao.Include(x => x.Propriedade).Where(x => x.Propriedade != null && x.Propriedade.ProdutorId == produtorId);
        }

        public IEnumerable<Solicitacao> GetRecentsSolicitacoes()
        {
            return _context.Solicitacao.Include(x => x.Propriedade).Take(10);
        }
    }
}
