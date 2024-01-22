using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeotecnologiaKNS.Models
{
    public class Cartografia : IIndustriaInfo, IPropriedadesInfo, IPrimaryKeyInfo<int>
    {
        public int Id { get; set; }

        [ForeignKey(nameof(Industria))]
        public int TenantId { get; set; }
        public Industria? Industria { get; }

        [Required(ErrorMessage = "Campo obrigatório")]
        [Display(Name = "Propriedade")]
        [ForeignKey(nameof(Propriedade))]
        public int PropriedadeId { get; set; }
        public Propriedade? Propriedade { get; set; }

        [Required(ErrorMessage = "Campo obrigatório")]
        [Display(Name = "Tipo")]
        public string? Tipo { get; set; }

        [Display(Name = "Data da solicitação")]
        public List<CartografiaArquivo>? Documentos { get; set; }
    }
}
