using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeotecnologiaKNS.Models
{
    public class Produtor : IIndustriaInfo, IPrimaryKeyInfo<int>
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(Industria))]
        public int TenantId { get; set; }
        public Industria? Industria { get; } = default!;

        [Required(ErrorMessage = "Campo obrigatório")]
        [Display(Name = "Produtor")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "O nome deve ter no mínimo 6 e no máximo 100 caracteres")]
        public string Nome { get; set; } = default!;

        [Required(ErrorMessage = "Campo obrigatório")]
        [Display(Name = "CPF/CNPJ")]
        public string Cpf { get; set; } = default!;

        public List<ProdutorArquivo>? Documentos { get; set; }

        public List<Propriedade>? Propriedades { get; set; }

        public Solicitacao? Solicitacoes { get; set; }
    }

}
