using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeotecnologiaKNS.Models
{
    public class Solicitacao : IIndustriaInfo, IPrimaryKeyInfo<int>
    {
        public int Id { get; set; }


        [ForeignKey(nameof(Industria))]
        public int TenantId { get; set; }
        public Industria Industria { get; }

        [Required(ErrorMessage = "Campo obrigatório")]
        [Display(Name = "Propriedade")]
        public int PropriedadeId { get; set; }
        public Propriedade? Propriedade { get; set; }

        [Display(Name = "Analista")]
        public string? Analista { get; set; }

        [Required(ErrorMessage = "Campo obrigatório")]
        [Display(Name = "Solicitante")]
        public string Solicitante { get; set; }

        [Required(ErrorMessage = "Campo obrigatório")]
        [Display(Name = "Data da solicitação")]
        public DateTime DataSolicitacao { get; set; }

        [Display(Name = "Data da análise")]
        public DateTime? DataAnalise { get; set; }

        
        [Display(Name = "Observação")]
        public string? Observacao { get; set; }

        [Display(Name = "Parecer")]
        public string? Parecer { get; set; }

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
