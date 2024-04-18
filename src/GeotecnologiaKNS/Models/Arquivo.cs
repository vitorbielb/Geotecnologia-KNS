using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeotecnologiaKNS.Models
{
    public abstract class Arquivo : IPrimaryKeyInfo<int>
    {
        private const string RequiredMessage = "Campo obrigatório";

        public int Id { get; set; }

        [Required(ErrorMessage = RequiredMessage)]
        [DisplayName("Descrição")]
        [StringLength(200, ErrorMessage = "A descrição deve ter no máximo {1} caracteres.")]
        public string Descricao { get; set; } = string.Empty;

        [Required(ErrorMessage = RequiredMessage)]
        public byte[] Dados { get; set; } = Array.Empty<byte>();

        [Required(ErrorMessage = RequiredMessage)]
        [DisplayName("Tipo de Conteúdo")]
        [StringLength(150, ErrorMessage = "O ContentType deve ter no máximo {1} caracteres.")]
        public string ContentType { get; set; } = string.Empty;

        [NotMapped]
        public abstract int VinculoId { get; set; }
    }

    public class ProdutorArquivo : Arquivo
    {
        [ForeignKey(nameof(Produtor))]
        public override int VinculoId { get; set; }
    }

    public class PropriedadeArquivo : Arquivo
    {
        [ForeignKey(nameof(Propriedade))]
        public override int VinculoId { get; set; }
    }

    public class AnaliseArquivo : Arquivo
    {
        [ForeignKey(nameof(Analise))]
        public override int VinculoId { get; set; }

        [DisplayName("Data da Análise")]
        public DateTime DataAnalise { get; set; } = DateTime.Now;
    }

    public class CartografiaArquivo : Arquivo
    {
        [ForeignKey(nameof(Cartografia))]
        public override int VinculoId { get; set; }

        [DisplayName("Data da Cartografia")]
        public DateTime DataCartografia { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "Campo obrigatório")]
        [DisplayName("Tipo")]
        [StringLength(100, ErrorMessage = "O tipo deve ter no máximo {1} caracteres.")]
        public string Tipo { get; set; } = string.Empty;
    }
}