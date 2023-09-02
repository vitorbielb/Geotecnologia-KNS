using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace GeotecnologiaKNS.Models
{
    using static Propriedade;

    public enum Estados

    {
        [Display(Name = "Acre")]
        AC,

        [Display(Name = "Alagoas")]
        AL,

        [Display(Name = "Amapá")]
        AP,

        [Display(Name = "Amazonas")]
        AM,

        [Display(Name = "Bahia")]
        BA,

        [Display(Name = "Ceará")]
        CE,

        [Display(Name = "Distrito Federal")]
        DF,

        [Display(Name = "Espírito Santo")]
        ES,

        [Display(Name = "Goiás")]
        GO,

        [Display(Name = "Maranhão")]
        MA,

        [Display(Name = "Mato Grosso")]
        MT,

        [Display(Name = "Mato Grosso do Sul")]
        MS,

        [Display(Name = "Minas Gerais")]
        MG,

        [Display(Name = "Pará")]
        PA,

        [Display(Name = "Paraíba")]
        PB,

        [Display(Name = "Paraná")]
        PR,

        [Display(Name = "Pernambuco")]
        PE,

        [Display(Name = "Piauí")]
        PI,

        [Display(Name = "Rio de Janeiro")]
        RJ,

        [Display(Name = "Rio Grande do Norte")]
        RN,

        [Display(Name = "Rio Grande do Sul")]
        RS,

        [Display(Name = "Rondônia")]
        RO,

        [Display(Name = "Roraima")]
        RR,

        [Display(Name = "Santa Catarina")]
        SC,

        [Display(Name = "São Paulo")]
        SP,

        [Display(Name = "Sergipe")]
        SE,

        [Display(Name = "Tocantins")]
        TO,
    }

    public static class UnidadesFederativasExtension
    {
        public static IEnumerable<SelectListItem> GetUnidadesFederativas()
        {
            List<SelectListItem> selectListItems = new List<SelectListItem> { new SelectListItem("Selecione...", null, true) };

            selectListItems
                .AddRange(typeof(Estados)
                .GetFields()
                .Select(GetUnidadeFederativaSelectListItem)
                .Where(x => !string.IsNullOrWhiteSpace(x.Text) && !string.IsNullOrWhiteSpace(x.Value)));

            return selectListItems;
        }

        public static IEnumerable<SelectListItem> GetCities(this Estados unidadesFederativa)
        {
            return Regionalizacao.Municipios[unidadesFederativa]
                .Select(GetMunicipioSelectListItem)
                .Append(new SelectListItem("Selecione...", null, true));
        }

        private static SelectListItem GetUnidadeFederativaSelectListItem(FieldInfo field)
        {
            return new SelectListItem(field.GetCustomAttribute<DisplayAttribute>()?.Name, field.Name);
        }

        private static SelectListItem GetMunicipioSelectListItem(string municipio)
        {
            return new SelectListItem(municipio, municipio);
        }
    }

}
