using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeotecnologiaKNS.Models
{
    public enum SituacaoAnalise
    {
        /// <summary>Na fila, ainda não processada.</summary>
        Pendente = 0,

        Concluida = 1,

        /// <summary>Esgotou as tentativas; exige tratamento humano.</summary>
        Falhou = 2,

        /// <summary>
        /// Em processamento por um trabalhador.
        /// </summary>
        /// <remarks>
        /// Existe para que a tomada do item seja atômica: a transição de
        /// Pendente para Processando é um UPDATE condicional, e quem não
        /// conseguir mudar a linha sabe que outro já pegou. Sem ela, dois
        /// processos cruzariam o mesmo imóvel e gravariam laudos duplicados.
        /// </remarks>
        Processando = 3
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

        /// <summary>
        /// Retrato das regras vigentes quando a análise rodou, em JSON.
        /// </summary>
        /// <remarks>
        /// Guardar só o nome não basta: a política pode mudar depois, e um
        /// bloqueio contestado meses adiante precisa ser explicável pelas regras
        /// que de fato o produziram.
        /// </remarks>
        [Display(Name = "Regras aplicadas")]
        public string? PoliticaAplicada { get; set; }

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

        /// <summary>
        /// Todas as regras da política puderam ser aplicadas nesta execução.
        /// </summary>
        /// <remarks>
        /// Guardado como coluna, e não só no texto do parecer, para que seja
        /// possível perguntar ao banco quais laudos foram emitidos com cobertura
        /// parcial — e refazê-los quando a camada que faltava for carregada.
        /// </remarks>
        [Display(Name = "Cobertura completa")]
        public bool CoberturaCompleta { get; set; }

        /// <summary>Regras que ficaram sem base para consulta, uma por linha.</summary>
        [Display(Name = "Regras não avaliadas")]
        public string? RegrasNaoAvaliadas { get; set; }

        public string? Erro { get; set; }

        /// <summary>Quantas vezes o processamento já foi tentado.</summary>
        /// <remarks>
        /// Falha de rede ou banco geoespacial fora do ar é transitória e merece
        /// nova tentativa; CAR ausente da base não melhora repetindo. Como não
        /// dá para distinguir com segurança, o limite de tentativas é o que
        /// impede uma análise impossível de ocupar a fila para sempre.
        /// </remarks>
        public int Tentativas { get; set; }

        /// <summary>Quando esta análise volta a ser elegível para processamento.</summary>
        public DateTime? ProximaTentativaEm { get; set; }

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
