namespace GeotecnologiaKNS.Infra;

public partial class Features
{
    public class Tenant : IFeature
    {
        public bool Create { get; set; }
        public bool Read { get; set; }
        public bool Update { get; set; }
        public bool Delete { get; set; }

        public string FeatureName => nameof(Tenants);
    }

    public class User : IFeature
    {
        public bool Create { get; set; }
        public bool Read { get; set; }
        public bool Update { get; set; }
        public bool Delete { get; set; }

        public string FeatureName => nameof(Users);
    }

    public class Produtor : IFeature
    {
        public bool Create { get; set; }
        public bool Read { get; set; }
        public bool Update { get; set; }
        public bool Delete { get; set; }

        public string FeatureName => nameof(Produtores);
    }

    public class Propriedade : IFeature
    {
        public bool Create { get; set; }
        public bool Read { get; set; }
        public bool Update { get; set; }
        public bool Delete { get; set; }

        public string FeatureName => nameof(Propriedades);
    }

    public class Solicitacao : IFeature
    {
        public bool Create { get; set; }
        public bool Read { get; set; }
        public bool Update { get; set; }
        public bool Delete { get; set; }

        public string FeatureName => nameof(Solicitacoes);
    }
}

public interface IFeature
{
    public string FeatureName { get; }
}
