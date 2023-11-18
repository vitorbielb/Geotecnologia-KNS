namespace GeotecnologiaKNS.Repositories.Interfaces
{
    public interface IIndustriaRepository
    {
        IEnumerable<Industria> GetIndustrias();
        IAsyncEnumerable<Industria> GetIndustriasAsync();
    }
}