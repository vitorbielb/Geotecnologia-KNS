using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeotecnologiaKNS.Models
{
    public enum SituacaoAnalise
    {
        Pendente = 0,
        Concluida = 1,
        Falhou = 2
    }

    /// <summary>
    /// Execução de uma análise automática sobre uma solicitação.
    /// </summary>
    /// <remarks>
    /// Guarda o resultado e as evidências congelados no momento da execução,
    /// junto com quais camadas foram verificadas. Recarregar as bases depois não
    /// pode mudar retroativamente um laudo já emitido: é isso que torna um
    /// bloqueio defensável quando o fornecedor contesta.
    /// </remarks>
    public class AnaliseAutomatica : IIndustriaInfo, IPrimaryKeyInfo<int>
    {
        public int Id { get; set; }

        [ForeignKey(nameof(Industria))]
        public int TenantId { get; set; }

        public Industria? Industria { get; set; }

        [ForeignKey(nameof(Solicitacao))]
        public int SolicitacaoId { get; set; }

        public Solicitacao? Solicitacao { get; set; }

        [Display(Name = "Número do CAR")]
        [StringLength(100)]
        public string CodigoCar { get; set; } = string.Empty;

        [Display(Name = "Política aplicada")]
        [StringLength(200)]
        public string Politica { get; set; } = string.Empty;

        [Display(Name = "Área do imóvel (ha)")]
        public double AreaImovelHa { get; set; }

        [Display(Name = "Situação da análise")]
        public SituacaoAnalise Situacao { get; set; } = SituacaoAnalise.Pendente;

        [Display(Name = "Resultado")]
        public Status Resultado { get; set; } = Status.Solicitado;

        [Display(Name = "Parecer")]
        public string? Parecer { get; set; }

        /// <summary>Camadas verificadas e suas versões, para auditoria do laudo.</summary>
        [Display(Name = "Camadas verificadas")]
        public string? CamadasVerificadas { get; set; }

        public string? Erro { get; set; }

        public DateTime IniciadaEm { get; set; } = DateTime.Now;

        public DateTime? ConcluidaEm { get; set; }

        public List<AnaliseOcorrencia> Ocorrencias { get; set; } = new();
    }

    /// <summary>
    /// Uma regra que disparou durante a análise, com a evidência.
    /// </summary>
    public class AnaliseOcorrencia : IPrimaryKeyInfo<int>
    {
        public int Id { get; set; }

        [ForeignKey(nameof(Analise))]
        public int AnaliseId { get; set; }

        public AnaliseAutomatica? Analise { get; set; }

        [StringLength(20)]
        public string CodigoRegra { get; set; } = string.Empty;

        [StringLength(300)]
        public string Descricao { get; set; } = string.Empty;

        /// <summary>0 informativo, 1 alerta, 2 bloqueio.</summary>
        public int Severidade { get; set; }

        [StringLength(200)]
        public string Camada { get; set; } = string.Empty;

        [StringLength(200)]
        public string Origem { get; set; } = string.Empty;

        [StringLength(300)]
        public string? Rotulo { get; set; }

        public double AreaSobrepostaHa { get; set; }

        public double PercentualDoImovel { get; set; }

        [StringLength(500)]
        public string? Fundamento { get; set; }
    }
}
