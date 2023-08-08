using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeotecnologiaKNS.Models
{
    public class Solicitacao : IIndustriaInfo, IPropriedadeInfo, ICartografiasInfo, IPrimaryKeyInfo<int>
    {
        private const string RequiredMessage = "Campo obrigatório";
        private const string TextoLongoMessage = "O campo {0} deve ter no máximo {1} caracteres.";

        public int Id { get; set; }

        [ForeignKey(nameof(Industria))]
        public int TenantId { get; set; }

        public Industria? Industria { get; set; }

        [Required(ErrorMessage = RequiredMessage)]
        [Display(Name = "Imóvel Rural")]
        [ForeignKey(nameof(Propriedade))]
        public int PropriedadeId { get; set; }

        public Propriedade? Propriedade { get; set; }

        [Display(Name = "Responsável pela Análise")]
        [StringLength(150, ErrorMessage = TextoLongoMessage)]
        public string? Analista { get; set; }

        [Required(ErrorMessage = RequiredMessage)]
        [Display(Name = "Responsável pela Solicitação")]
        [StringLength(150, ErrorMessage = TextoLongoMessage)]
        public string Solicitante { get; set; } = string.Empty;

        [Display(Name = "Data de Abertura")]
        public DateTime? DataSolicitacao { get; set; }

        [Display(Name = "Data da Avaliação")]
        public DateTime? DataAnalise { get; set; }

        public Cartografia? Cartografia { get; set; }

        [Display(Name = "Observações")]
        [StringLength(2000, ErrorMessage = TextoLongoMessage)]
        public string? Observacao { get; set; }

        [Display(Name = "Resultado da Análise")]
        [StringLength(2000, ErrorMessage = TextoLongoMessage)]
        public string? Parecer { get; set; }

        public List<AnaliseArquivo> Documentos { get; set; } = new();

        [Display(Name = "Situação da Solicitação")]
        public Status Status { get; set; } = Status.Solicitado;
    }

    public enum Status
    {
        Solicitado = 0,
        Liberado = 1,
        Alerta = 2,
        Bloqueado = 3
    }
}