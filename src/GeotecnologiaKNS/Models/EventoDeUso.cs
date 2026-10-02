using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeotecnologiaKNS.Models
{
    /// <summary>
    /// O que é contado para efeito de cobrança.
    /// </summary>
    public enum TipoDeUso
    {
        /// <summary>Uma análise automática rodou até o fim.</summary>
        AnaliseExecutada = 1,

        /// <summary>Uma solicitação foi aberta.</summary>
        SolicitacaoAberta = 2,

        /// <summary>Um imóvel entrou na carteira monitorada.</summary>
        ImovelCadastrado = 3
    }

    /// <summary>
    /// Um fato cobrável, registrado no instante em que aconteceu.
    /// </summary>
    /// <remarks>
    /// Contar pelas tabelas de negócio não serve para faturar: solicitação é
    /// apagada, imóvel é removido da carteira, e a contagem do mês passado
    /// mudaria depois de fechada. Um evento imutável preserva o que foi
    /// consumido, independente do que aconteça com o registro que o originou.
    ///
    /// O que se cobra por cada evento é decisão comercial e não está aqui —
    /// esta tabela só mede.
    /// </remarks>
    public class EventoDeUso : IIndustriaInfo, IPrimaryKeyInfo<long>
    {
        public long Id { get; set; }

        [ForeignKey(nameof(Industria))]
        public int TenantId { get; set; }

        public Industria? Industria { get; set; }

        [Display(Name = "Tipo")]
        public TipoDeUso Tipo { get; set; }

        /// <summary>
        /// Id do registro que originou o evento, para conferência quando a
        /// fatura for contestada.
        /// </summary>
        [Display(Name = "Referência")]
        public int ReferenciaId { get; set; }

        /// <summary>
        /// Identificação legível do que foi consumido — o CAR do imóvel, por
        /// exemplo. Guardado junto porque o registro de origem pode sumir.
        /// </summary>
        [Display(Name = "Descrição")]
        [StringLength(200)]
        public string? Descricao { get; set; }

        [Display(Name = "Ocorrido em")]
        public DateTime OcorridoEm { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Competência no formato aaaamm, gravada junto para agrupar por mês
        /// sem depender de função de data no banco nem de fuso.
        /// </summary>
        [Display(Name = "Competência")]
        public int Competencia { get; set; }

        public static int CompetenciaDe(DateTime momento) => momento.Year * 100 + momento.Month;
    }
}
