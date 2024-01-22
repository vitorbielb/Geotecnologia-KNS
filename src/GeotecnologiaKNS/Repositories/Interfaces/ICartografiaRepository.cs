namespace GeotecnologiaKNS.Repositories.Interfaces
{
    public interface ICartografiaRepository
    {
        IEnumerable<Cartografia> GetCartografiasByPropriedade(int propriedadeId);
    }
}
