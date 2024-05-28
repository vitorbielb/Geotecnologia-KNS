using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeotecnologiaKNS.Models
{
    public class Produtor : IIndustriaInfo, IPrimaryKeyInfo<int>
    {
        private const string RequiredMessage = "Campo obrigatório";

        [Key]
        public int Id { get; set; }

        [ForeignKey(nameof(Industria))]
        public int TenantId { get; set; }

        public Industria? Industria { get; set; }

        [Required(ErrorMessage = RequiredMessage)]
        [Display(Name = "Produtor Rural")]
        [StringLength(100, MinimumLength = 4, ErrorMessage = "O nome deve ter no mínimo 4 e no máximo 100 caracteres")]
        public string Nome { get; set; } = string.Empty;

        [Required(ErrorMessage = RequiredMessage)]
        [Display(Name = "CPF/CNPJ")]
        [StringLength(18, MinimumLength = 11, ErrorMessage = "O CPF/CNPJ deve ter entre 11 e 18 caracteres")]
        public string Cpf { get; set; } = string.Empty;

        public List<ProdutorArquivo> Documentos { get; set; } = new();

        public List<Propriedade> Propriedades { get; set; } = new();

        public List<Solicitacao> Solicitacoes { get; set; } = new();
    }
}