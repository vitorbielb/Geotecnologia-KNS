namespace GeotecnologiaKNS.Repositories
{
    public class IndustriaRepository : IIndustriaRepository
    {
        private readonly ApplicationDbContext context;

        public IndustriaRepository(ApplicationDbContext context)
        {
            this.context = context;
        }

        public IEnumerable<Industria> GetIndustrias()
        {
            return this.context.Industrias.AsEnumerable();
        }

        public IAsyncEnumerable<Industria> GetIndustriasAsync()
        {
            return this.context.Industrias.AsAsyncEnumerable();
        }
    }
}
