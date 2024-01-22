namespace GeotecnologiaKNS.Models
{
    public interface ITenantInfo
    {
        public int TenantId { get; set; }
    }

    public interface IIndustriaInfo : ITenantInfo
    {
        public Industria Industria { get; }
    }
    public interface IPropriedadeInfo
    {
        public int PropriedadeId { get; set; }
    }

    public interface IPropriedadesInfo : IPropriedadeInfo
    {
        public Propriedade Propriedade { get; }
    }
    public interface ICartografiasInfo : IPropriedadeInfo
    {
        public Cartografia Cartografia { get; }
    }
    public interface IPrimaryKeyInfo<T>
    {
        public T Id { get; set; }
    }
}
