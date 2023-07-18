using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace GeotecnologiaKNS.Models
{
    public class Propriedade
    {
        [Display(Name = "ID")]
        public int Id { get; set; }
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
        [Display(Name = "Área")]
        public string Area { get; set; } 
        [Required(ErrorMessage = "Campo obrigatório")]
        [Display(Name = "Área útil")]
        public string AreaUtil { get; set; } 
        [Required(ErrorMessage = "Campo obrigatório")]
        [Display(Name = "Latitude")]
        public string Latitude { get; set; }
        [Required(ErrorMessage = "Campo obrigatório")]
        [Display(Name = "Longitude")]
        public string Longitude { get; set; }
        [Display(Name = "Origem coordenadas")]
        public string? OrigemCoordenadas { get; set; }
        [Required(ErrorMessage = "Campo obrigatório")]
        [Display(Name = "Bioma")]
        public string Bioma { get; set; }
        [Required(ErrorMessage = "Campo obrigatório")]
        [Display(Name = "Unidade Federativa")]
        public Estado UnidadeFederativa { get; set; }
        [Required(ErrorMessage = "Campo obrigatório")]
        [Display(Name = "Município")]
        public string Municipio { get; set; }
        [Required(ErrorMessage = "Campo obrigatório")]
        [Display(Name = "Indústria")]
        public string Industria { get; set; }
        [Required(ErrorMessage = "Campo obrigatório")]
        [Display(Name = "Tipo de propriedade")]
        public string TipoCadastroRural { get; set; }
        [Display(Name = "Matrícula")]
        public string? Matricula { get; set; }
        [Required(ErrorMessage = "Campo obrigatório")]
        [Display(Name = "Cadastro Ambiental Rural")]
        public string CadastroAmbientalRural { get; set; }
        [Display(Name = "Licença Ambiental")]
        public string? LicencaAmbiental { get; set; }
        [Display(Name = "CCIR")]
        public string? Ccir { get; set; }
        [Display(Name = "INCRA")]
        public string? Incra { get; set; }

        [Display(Name = "Outros")]
        public string? Outros { get; set; }
    }
}

