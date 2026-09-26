using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeotecnologiaKNS.Models
{
    /// <summary>
    /// Imóvel rural monitorado.
    /// </summary>
    /// <remarks>
    /// A partir da automação por CAR, só dois campos são digitados: o código do
    /// CAR e o produtor. Todo o resto é derivado da base pública e fica
    /// somente-leitura — dado digitado à mão era a principal fonte de erro que
    /// contaminava a análise.
    /// </remarks>
    public class Propriedade : IIndustriaInfo, IPrimaryKeyInfo<int>
    {
        private const string RequiredMessage = "Campo obrigatório";
        private const string NomeLengthMessage = "O nome deve ter no mínimo 2 e no máximo 100 caracteres";

        public int Id { get; set; }

        [ForeignKey(nameof(Industria))]
        public int TenantId { get; set; }

        public Industria? Industria { get; set; }

        #region Entrada do usuário

        /// <summary>Código do CAR normalizado (UF-IBGE-hash). É a chave de tudo.</summary>
        [Required(ErrorMessage = "Informe o número do CAR")]
        [Display(Name = "Número do CAR")]
        [StringLength(100)]
        public string CodigoCar { get; set; } = string.Empty;

        [Required(ErrorMessage = "Campo obrigatório! Caso não tenha cadastrado um Produtor, cadastre-o na guia de 'Produtores'")]
        [Display(Name = "Produtor")]
        [ForeignKey(nameof(Produtor))]
        public int ProdutorId { get; set; }

        public Produtor? Produtor { get; set; }

        /// <summary>Apelido interno. A base do CAR não tem nome de imóvel.</summary>
        [Display(Name = "Nome da propriedade")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = NomeLengthMessage)]
        public string NomePropriedade { get; set; } = string.Empty;

        #endregion

        #region Derivado da base do CAR

        /// <summary>Perímetro do imóvel em GeoJSON, copiado da base no momento do cadastro.</summary>
        [Display(Name = "Perímetro")]
        public string? PerimetroGeoJson { get; set; }

        /// <summary>Qual base e qual carga originaram o perímetro. Base da auditoria do laudo.</summary>
        [Display(Name = "Origem do perímetro")]
        [StringLength(200)]
        public string? PerimetroOrigem { get; set; }

        [Display(Name = "Perímetro atualizado em")]
        public DateTime? PerimetroAtualizadoEm { get; set; }

        [Display(Name = "Situação no CAR")]
        [StringLength(50)]
        public string? SituacaoCar { get; set; }

        [Display(Name = "Área da propriedade (ha)")]
        public string Area { get; set; } = string.Empty;

        [Display(Name = "Latitude")]
        public double Latitude { get; set; }

        [Display(Name = "Longitude")]
        public double Longitude { get; set; }

        [Display(Name = "Origem das coordenadas")]
        public string? OrigemCoordenadas { get; set; }

        [Display(Name = "Unidade Federativa")]
        public Estados UnidadeFederativa { get; set; }

        [Display(Name = "Município")]
        public string Municipio { get; set; } = string.Empty;

        [Display(Name = "Bioma")]
        public string Bioma { get; set; } = string.Empty;

        #endregion

        #region Operacional da indústria (opcional)

        [Display(Name = "Tipo de propriedade")]
        public string TipoPropriedade { get; set; } = string.Empty;

        [Display(Name = "Ciclo de produção")]
        public string CicloProducao { get; set; } = string.Empty;

        [Display(Name = "Área útil (ha)")]
        public string AreaUtil { get; set; } = string.Empty;

        [Display(Name = "Tipo de cadastro rural")]
        public string TipoCadastroRural { get; set; } = string.Empty;

        /// <summary>
        /// Campo legado, anterior ao <see cref="CodigoCar"/>. Mantido para não
        /// perder o que já foi digitado; não é mais usado para consulta.
        /// </summary>
        [Display(Name = "Cadastro Ambiental Rural (legado)")]
        public string CadastroAmbientalRural { get; set; } = string.Empty;

        #endregion

        public List<PropriedadeArquivo> Documentos { get; set; } = new();

        public Validacao Validacao { get; set; } = Validacao.Pendente;

        /// <summary>
        /// Polígono desenhado à mão. Descontinuado: o perímetro vem do CAR.
        /// A propriedade permanece mapeada apenas para não perder os dados
        /// existentes até a limpeza de schema.
        /// </summary>
        [Obsolete("O perímetro passou a vir da base do CAR. Use PerimetroGeoJson.")]
        public Geozone? Geozone { get; set; }

        public Cartografia? Cartografia { get; set; }

        /// <summary>Indica se o imóvel já teve o perímetro resolvido pela base do CAR.</summary>
        [NotMapped]
        public bool TemPerimetro => !string.IsNullOrWhiteSpace(PerimetroGeoJson);
    }

    public enum Validacao
    {
        Pendente = 0,
        Validado = 1
    }
}
