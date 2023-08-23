using System.ComponentModel.DataAnnotations.Schema;

namespace GeotecnologiaKNS.Models
{
    public abstract class Arquivo : IPrimaryKeyInfo<int>
    {
        public int Id { get; set; }
        public string Descricao { get; set; }
        public byte[] Dados { get; set; }
        public string ContentType { get; set; }
        [NotMapped] public abstract int VinculoId { get; set; }
    }

    public class ProdutorArquivo : Arquivo
    {
        [ForeignKey("Produtor")] public override int VinculoId { get; set; }
    }

    public class PropriedadeArquivo : Arquivo
    {
        [ForeignKey("Propriedade")] public override int VinculoId { get; set; }
    }
}
