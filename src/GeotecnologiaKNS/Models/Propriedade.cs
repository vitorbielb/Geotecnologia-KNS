using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GeotecnologiaKNS.Models
{
    public class Propriedade : IIndustriaInfo, IPrimaryKeyInfo<int>
    {
        private const string RequiredMessage = "Campo obrigatório";
        private const string NomeLengthMessage = "O nome deve ter no mínimo 2 e no máximo 100 caracteres";

        public int Id { get; set; }

        [ForeignKey(nameof(Industria))]
        public int TenantId { get; set; }

        public Industria? Industria { get; set; }

        [Required(ErrorMessage = RequiredMessage)]
        [Display(Name = "Nome da propriedade")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = NomeLengthMessage)]
        public string NomePropriedade { get; set; } = string.Empty;

        [Required(ErrorMessage = RequiredMessage)]
        [Display(Name = "Tipo de propriedade")]
        public string TipoPropriedade { get; set; } = string.Empty;

        [Required(ErrorMessage = RequiredMessage)]
        [Display(Name = "Ciclo de produção")]
        public string CicloProducao { get; set; } = string.Empty;

        [Required(ErrorMessage = RequiredMessage)]
        [Display(Name = "Área da propriedade (ha)")]
        public string Area { get; set; } = string.Empty;

        [Required(ErrorMessage = RequiredMessage)]
        [Display(Name = "Área útil (ha)")]
        public string AreaUtil { get; set; } = string.Empty;

        [Required(ErrorMessage = RequiredMessage)]
        [Display(Name = "Latitude (UTM)")]
        public double Latitude { get; set; }

        [Required(ErrorMessage = RequiredMessage)]
        [Display(Name = "Longitude (UTM)")]
        public double Longitude { get; set; }

        [Display(Name = "Origem das coordenadas")]
        public string? OrigemCoordenadas { get; set; }

        [Required(ErrorMessage = RequiredMessage)]
        [Display(Name = "Bioma")]
        public string Bioma { get; set; } = string.Empty;

        [Required(ErrorMessage = RequiredMessage)]
        [Display(Name = "Unidade Federativa")]
        public Estados UnidadeFederativa { get; set; }

        [Required(ErrorMessage = RequiredMessage)]
        [Display(Name = "Município")]
        public string Municipio { get; set; } = string.Empty;

        [Required(ErrorMessage = RequiredMessage)]
        [Display(Name = "Tipo de cadastro rural")]
        public string TipoCadastroRural { get; set; } = string.Empty;

        [Required(ErrorMessage = RequiredMessage)]
        [Display(Name = "Cadastro Ambiental Rural")]
        public string CadastroAmbientalRural { get; set; } = string.Empty;

        [Required(ErrorMessage = "Campo obrigatório! Caso não tenha cadastrado um Produtor, cadastre-o na guia de 'Produtores'")]
        [Display(Name = "Produtor")]
        [ForeignKey(nameof(Produtor))]
        public int ProdutorId { get; set; }

        public Produtor? Produtor { get; set; }

        public List<PropriedadeArquivo> Documentos { get; set; } = new();

        public Validacao Validacao { get; set; } = Validacao.Pendente;

        public Geozone? Geozone { get; set; }

        public Cartografia? Cartografia { get; set; }
    }

    public enum Validacao
    {
        Pendente = 0,
        Validado = 1
    }
}