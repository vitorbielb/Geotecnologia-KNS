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
        [Display(Name = "Produtor Rural")]
        [StringLength(18, MinimumLength = 14, ErrorMessage = "O nome deve ter no mínimo 4 e no máximo 99 caracteres")]
        public string Nome { get; set; } = default!;

        [Required(ErrorMessage = "Campo obrigatório")]
        [Display(Name = "CPF/CNPJ")]
        [StringLength(18, MinimumLength = 14, ErrorMessage = "O nome deve ter no mínimo 14 e no máximo 18 caracteres")]
        public string Cpf { get; set; } = default!;

        public List<ProdutorArquivo>? Documentos { get; set; }

        public List<Propriedade>? Propriedades { get; set; }

        public Solicitacao? Solicitacoes { get; set; }
    }

}
