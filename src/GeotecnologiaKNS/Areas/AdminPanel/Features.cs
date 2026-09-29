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
        public IOperation Create { get; set; } = default!;
        public IOperation Read { get; set; } = default!;
        public IOperation Update { get; set; } = default!;
        public IOperation Delete { get; set; } = default!;
    }

    public class User : IFeature
    {
        public IOperation Create { get; set; } = default!;
        public IOperation Read { get; set; } = default!;
        public IOperation Update { get; set; } = default!;
        public IOperation Delete { get; set; } = default!;  
    }

    public class Produtor : IFeature
    {
        public IOperation Create { get; set; } = default!;
        public IOperation Read { get; set; } = default!;
        public IOperation Update { get; set; } = default!;
        public IOperation Delete { get; set; } = default!;
    }

    public class Propriedade : IFeature
    {
        public IOperation Create { get; set; } = default!;
        public IOperation Read { get; set; } = default!;
        public IOperation Update { get; set; } = default!;
        public IOperation Delete { get; set; } = default!;
    }

    public class Solicitacao : IFeature
    {
        public IOperation Create { get; set; } = default!;
        public IOperation Read { get; set; } = default!;
        public IOperation Update { get; set; } = default!;
        public IOperation Delete { get; set; } = default!;
    }

    public class Role : IFeature
    {
        public IOperation Create { get; set; } = default!;
        public IOperation Read { get; set; } = default!;    
        public IOperation Update { get; set; } = default!;
        public IOperation Delete { get; set; } = default!;
    }
}
