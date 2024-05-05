using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace GeotecnologiaKNS.Models
{
    using static Propriedade;

    public enum Estados
    {
        [Display(Name = "Acre")]
        AC = 1,

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
        TO
    }

    public static class UnidadesFederativasExtension
    {
        private const string SelecioneTexto = "Selecione...";

        public static IEnumerable<SelectListItem> GetUnidadesFederativas()
        {
            var items = Enum.GetValues(typeof(Estados))
                .Cast<Estados>()
                .Select(estado => new SelectListItem(
                    text: estado.GetDisplayName(),
                    value: estado.ToString()))
                .ToList();

            items.Insert(0, new SelectListItem(SelecioneTexto, string.Empty, true));

            return items;
        }

        public static IEnumerable<SelectListItem> GetCities(this Estados unidadeFederativa)
        {
            var items = Regionalizacao.Municipios[unidadeFederativa]
                .Select(municipio => new SelectListItem(
                    text: municipio,
                    value: municipio))
                .ToList();

            items.Insert(0, new SelectListItem(SelecioneTexto, string.Empty, true));

            return items;
        }

        public static string GetDisplayName(this Estados estado)
        {
            var memberInfo = typeof(Estados).GetMember(estado.ToString()).FirstOrDefault();
            return memberInfo?.GetCustomAttribute<DisplayAttribute>()?.Name ?? estado.ToString();
        }
    }
}