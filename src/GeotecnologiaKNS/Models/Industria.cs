using GeotecnologiaKNS.Validators;
using System.ComponentModel.DataAnnotations;

namespace GeotecnologiaKNS.Models
{
    public class Industria : ITenantInfo
    {
        private const string RequiredMessage = "Campo obrigatório";
        private const string NomeLengthMessage = "O nome deve ter no mínimo 2 e no máximo 100 caracteres";

        [Key]
        public int TenantId { get; set; }

        public byte[]? Imagem { get; set; }

        [Required(ErrorMessage = RequiredMessage)]
        [Display(Name = "Nome")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = NomeLengthMessage)]
        public string Nome { get; set; } = string.Empty;

        [Required(ErrorMessage = RequiredMessage)]
        [Display(Name = "Nome resumido")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = NomeLengthMessage)]
        public string NomeResumido { get; set; } = string.Empty;

        [Required(ErrorMessage = RequiredMessage)]
        [Display(Name = "Razão social")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = NomeLengthMessage)]
        public string RazaoSocial { get; set; } = string.Empty;

        [Required(ErrorMessage = RequiredMessage)]
        [Display(Name = "CNPJ")]
        [CnpjValid(ErrorMessage = "CNPJ inválido")]
        [StringLength(18, MinimumLength = 14, ErrorMessage = "CNPJ inválido")]
        public string Cnpj { get; set; } = string.Empty;

        public ICollection<Produtor> Produtores { get; set; } = new List<Produtor>();

        public ICollection<Propriedade> Propriedades { get; set; } = new List<Propriedade>();

        public ICollection<Solicitacao> Solicitacoes { get; set; } = new List<Solicitacao>();

        public ICollection<Cartografia> Cartografias { get; set; } = new List<Cartografia>();

        public ICollection<ApplicationUser> Usuarios { get; set; } = new List<ApplicationUser>();
    }
}