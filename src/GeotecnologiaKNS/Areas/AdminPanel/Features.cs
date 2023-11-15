namespace GeotecnologiaKNS.Infra;

public partial class Features
{
    public record List(
          Tenant Tenant
        , User User
        , Produtor Produtor
        , Propriedade Propriedade
        , Solicitacao Solicitacao
        , Role Role);

    public class Tenant : IFeature
    {
        public bool Create { get; set; }
        public bool Read { get; set; }
        public bool Update { get; set; }
        public bool Delete { get; set; }
    }

    public class User : IFeature
    {
        public bool Create { get; set; }
        public bool Read { get; set; }
        public bool Update { get; set; }
        public bool Delete { get; set; }
    }

    public class Produtor : IFeature
    {
        public bool Create { get; set; }
        public bool Read { get; set; }
        public bool Update { get; set; }
        public bool Delete { get; set; }
    }

    public class Propriedade : IFeature
    {
        public bool Create { get; set; }
        public bool Read { get; set; }
        public bool Update { get; set; }
        public bool Delete { get; set; }
    }

    public class Solicitacao : IFeature
    {
        public bool Create { get; set; }
        public bool Read { get; set; }
        public bool Update { get; set; }
        public bool Delete { get; set; }
    }

    public class Role : IFeature
    {
        public bool Create { get; set; }
        public bool Read { get; set; }
        public bool Update { get; set; }
        public bool Delete { get; set; }
    }
}
