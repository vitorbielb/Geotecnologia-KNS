using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeotecnologiaKNS.Models
{
    public class Produtor : IIndustriaInfo, IPrimaryKeyInfo<int>
    {
        public int Id { get; set; }

        [ForeignKey(nameof(Industria))]
        public int TenantId { get; set; }
        public Industria Industria { get; }

        [Required(ErrorMessage = "Campo obrigatório")]
        [Display(Name = "Nome")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "O nome deve ter no mínimo 6 e no máximo 100 caracteres")]
        public string Nome { get; set; }

        [Required(ErrorMessage = "Campo obrigatório")]
        [Display(Name = "CPF/CNPJ")]
        [StringLength(14, MinimumLength = 14, ErrorMessage = "Insira o CPF no seguinte modelo: 123.456.789-10")]
        public string Cpf { get; set; }

        [Required(ErrorMessage = "Campo obrigatório")]
        [Display(Name = "Indústria")]
        [StringLength(100, ErrorMessage = "O nome da indústria deve ter no máximo 100 caracteres")]

        public List<ProdutorArquivo>? Documentos { get; set; }
        public List<Propriedade>? Propriedades { get; set; }

    }
}
