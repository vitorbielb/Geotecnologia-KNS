namespace GeotecnologiaKNS.Models
{
    public class ProdutorPropriedadeViewModel
    {
        public Produtor Produtor { get; set;}
        public Propriedade Propriedade { get; set;}
        public Arquivo Arquivo { get; set;}
        public IEnumerable<Arquivo> Arquivos { get; set; }
    }
}
