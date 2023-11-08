using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Http;

namespace GeotecnologiaKNS.Models
{
    public class Propriedade : IIndustriaInfo, IPrimaryKeyInfo<int>
    {
        public int Id { get; set; }

        [ForeignKey(nameof(Industria))]
        public int TenantId { get; set; }
        public Industria Industria { get; }

        [Required(ErrorMessage = "Campo obrigatório")]
        [Display(Name = "Nome propriedade")]
        [StringLength(150, MinimumLength = 6, ErrorMessage = "O nome deve ter no mínimo 6 e no máximo 150 caracteres")]
        public string NomePropriedade { get; set; }
        [Required(ErrorMessage = "Campo obrigatório")]
        [Display(Name = "Tipo de propriedade")]
        public string TipoPropriedade { get; set; }
        [Required(ErrorMessage = "Campo obrigatório")]
        [Display(Name = "Cliclo de produção")]
        public string CicloProducao { get; set; }
        [Required(ErrorMessage = "Campo obrigatório")]
        [Display(Name = "Área da propriedade (ha)")]
        public string Area { get; set; }
        [Required(ErrorMessage = "Campo obrigatório")]
        [Display(Name = "Área útil (ha)")]
        public string AreaUtil { get; set; }
        [Required(ErrorMessage = "Campo obrigatório")]
        [Display(Name = "Latitude (UTM)")]
        public string Latitude { get; set; }
        [Required(ErrorMessage = "Campo obrigatório")]
        [Display(Name = "Longitude (UTM)")]
        public string Longitude { get; set; }
        [Display(Name = "Origem coordenadas")]
        public string? OrigemCoordenadas { get; set; }
        [Required(ErrorMessage = "Campo obrigatório")]
        [Display(Name = "Bioma")]
        public string Bioma { get; set; }
        [Required(ErrorMessage = "Campo obrigatório")]
        [Display(Name = "Unidade Federativa")]
        public Estados UnidadeFederativa { get; set; }
        [Required(ErrorMessage = "Campo obrigatório")]
        [Display(Name = "Município")]
        public string Municipio { get; set; }
        [Required(ErrorMessage = "Campo obrigatório")]
        [Display(Name = "Tipo de cadastro rural")]
        public string TipoCadastroRural { get; set; }
        [Required(ErrorMessage = "Campo obrigatório")]
        [Display(Name = "Cadastro Ambiental Rural")]
        public string CadastroAmbientalRural { get; set; }

        [Required(ErrorMessage = "Campo obrigatório! Caso não tenha cadastrado um Produtor, cadastre-o na guia de 'Produtores'")]
        [Display(Name = "Produtor")]
        public int ProdutorId { get; set; }
        public Produtor? Produtor { get; set; }
        public List<PropriedadeArquivo>? Documentos { get; set; }
        public Validacao Validacao { get; set; } = Validacao.Inválido;
    }
   
}
public enum Validacao
{
    Inválido,
    Validado,

}