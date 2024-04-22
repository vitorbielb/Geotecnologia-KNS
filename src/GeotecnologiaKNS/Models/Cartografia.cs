using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeotecnologiaKNS.Models
{
    public class Cartografia : IIndustriaInfo, IPropriedadesInfo, IPrimaryKeyInfo<int>
    {
        private const string RequiredMessage = "Campo obrigatório";

        public int Id { get; set; }

        [ForeignKey(nameof(Industria))]
        public int TenantId { get; set; }

        public Industria? Industria { get; set; }

        [Required(ErrorMessage = RequiredMessage)]
        [Display(Name = "Propriedade")]
        [ForeignKey(nameof(Propriedade))]
        public int PropriedadeId { get; set; }

        public Propriedade? Propriedade { get; set; }

        public CartografiaArquivo? CartografiaArquivo { get; set; }

        [Display(Name = "Arquivos da Cartografia")]
        public List<CartografiaArquivo> Arquivos { get; set; } = new();
    }
}