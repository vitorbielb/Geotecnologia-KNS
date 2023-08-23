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

    public interface IPrimaryKeyInfo<T>
    {
        public T Id { get; set; }
    }
}
