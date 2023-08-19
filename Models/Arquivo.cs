using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace GeotecnologiaKNS.Models
{
    [ModelBinder(BinderType = typeof(ArquivoEntityBinder))]
    public class Arquivo
    {
        public int Id { get; set; }
        [Display(Name = "Domentos da propriedade")]
        public string Descricao { get; set; }
        public byte[] Dados { get; set; }
        public string ContentType { get; set; }
    }
}
