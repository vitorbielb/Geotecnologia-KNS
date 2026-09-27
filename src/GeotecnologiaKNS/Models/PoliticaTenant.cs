using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using GeotecnologiaKNS.Geo.Entities;

namespace GeotecnologiaKNS.Models
{
    /// <summary>
    /// Política de conformidade de uma indústria.
    /// </summary>
    /// <remarks>
    /// Até aqui as regras eram constantes compiladas, iguais para todos. Num
    /// produto vendido a várias indústrias isso não se sustenta: cada uma tem
    /// compromisso de cadeia diferente, e o que uma trata como bloqueio a outra
    /// trata como alerta.
    ///
    /// Indústria sem política cadastrada usa o protocolo padrão. Não há
    /// semeadura automática de propósito — assim fica registrado quem
    /// deliberadamente definiu regras próprias e quem está no padrão.
    /// </remarks>
    public class PoliticaTenant : IIndustriaInfo, IPrimaryKeyInfo<int>
    {
        public int Id { get; set; }

        [ForeignKey(nameof(Industria))]
        public int TenantId { get; set; }

        public Industria? Industria { get; set; }

        [Required(ErrorMessage = "Campo obrigatório")]
        [Display(Name = "Nome da política")]
        [StringLength(200)]
        public string Nome { get; set; } = string.Empty;

        [Display(Name = "Atualizada em")]
        public DateTime AtualizadaEm { get; set; } = DateTime.Now;

        public List<RegraTenant> Regras { get; set; } = new();
    }

    /// <summary>
    /// Uma regra da política de uma indústria.
    /// </summary>
    public class RegraTenant : IPrimaryKeyInfo<int>
    {
        public int Id { get; set; }

        [ForeignKey(nameof(Politica))]
        public int PoliticaId { get; set; }

        public PoliticaTenant? Politica { get; set; }

        [Required]
        [Display(Name = "Código")]
        [StringLength(20)]
        public string Codigo { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Descrição")]
        [StringLength(300)]
        public string Descricao { get; set; } = string.Empty;

        [Display(Name = "Tipo de camada")]
        public TipoCamada Tipo { get; set; }

        /// <summary>0 informativo, 1 alerta, 2 bloqueio.</summary>
        [Display(Name = "Severidade")]
        public int Severidade { get; set; }

        [Display(Name = "Área mínima (ha)")]
        public double AreaMinimaHa { get; set; }

        [Display(Name = "Percentual mínimo do imóvel")]
        public double PercentualMinimo { get; set; }

        [Display(Name = "Ano mínimo")]
        public int? AnoMinimo { get; set; }

        [Display(Name = "Fundamento")]
        [StringLength(500)]
        public string? Fundamento { get; set; }

        /// <summary>Regra inativa não é avaliada, mas fica registrada.</summary>
        [Display(Name = "Ativa")]
        public bool Ativa { get; set; } = true;
    }
}
