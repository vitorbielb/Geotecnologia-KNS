using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeotecnologiaKNS.Models
{
    public class Solicitacao : IIndustriaInfo, IPropriedadeInfo, ICartografiasInfo, IPrimaryKeyInfo<int>
    {
        public int Id { get; set; }

        [ForeignKey(nameof(Industria))]
        public int TenantId { get; set; }
        public Industria? Industria { get; }

        [Required(ErrorMessage = "Campo obrigatório")]
        [Display(Name = "Imóvel Rural")]
        public int PropriedadeId { get; set; }
        public Propriedade? Propriedade { get; set; }

        [Display(Name = "Responsável pela Análise")]
        public string? Analista { get; set; }

        [Required(ErrorMessage = "Campo obrigatório")]
        [Display(Name = "Responsável pela Solicitação")]
        public string Solicitante { get; set; }

        [Display(Name = "Data de Abertura")]
        public DateTime? DataSolicitacao { get; set; }

        [Display(Name = "Data da Avaliação")]
        public DateTime? DataAnalise { get; set; }

        public Cartografia? Cartografia { get; set; }

        [Display(Name = "Observações")]
        public string? Observacao { get; set; }

        [Display(Name = "Resultado da Análise")]
        public string? Parecer { get; set; }

        public List<AnaliseArquivo>? Documentos { get; set; }

        [Display(Name = "Situação da Solicitação")]
        public Status Status { get; set; } = Status.Solicitado;
    }

    public enum Status
    {
        Solicitado,
        Liberado,
        Alerta,
        Bloqueado
    }
}