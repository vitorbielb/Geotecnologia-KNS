using GeotecnologiaKNS.Areas.Identity.Pages.Account;
using GeotecnologiaKNS.Validators;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeotecnologiaKNS.Models
{
    public class Industria : ITenantInfo
    {
        [Key]
        public int TenantId { get; set; }

        public byte[]? Imagem { get; set; }

        [Required(ErrorMessage = "Campo obrigatório")]
        [Display(Name = "Nome")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "O nome deve ter no mínimo 6 e no máximo 100 caracteres")]
        public string Nome { get; set; } = default!;

        [Required(ErrorMessage = "Campo obrigatório")]
        [Display(Name = "Nome resumido")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "O nome deve ter no mínimo 3 e no máximo 100 caracteres")]
        public string NomeResumido { get; set; } = default!;

        [Required(ErrorMessage = "Campo obrigatório")]
        [Display(Name = "Razão social")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "O nome deve ter no mínimo 6 e no máximo 100 caracteres")]
        public string RazaoSocial { get; set; } = default!;

        [Required(ErrorMessage = "Campo obrigatório")]
        [Display(Name = "CNPJ")]
        [CnpjValid(ErrorMessage = "Cnpj invalido")]
        public string Cnpj { get; set; } = default!;

        public ICollection<Produtor>? Produtores { get; set; }

        public ICollection<Propriedade>? Propriedades { get; set; }

        public ICollection<Solicitacao>? Solicitacoes { get; set; }
        public ICollection<Cartografia>? Cartografias { get; set; }

        public ICollection<ApplicationUser>? Usuarios { get; set; }
    }
}
