using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace GeotecnologiaKNS.Models
{
    public class PermissionsModel
    {
        private readonly IEnumerable<Industria> _industrias = default!;

        public PermissionsModel()
        {
        }

        public PermissionsModel(IEnumerable<Industria> industrias)
        {
            _industrias = industrias;
        }

        public IEnumerable<SelectListItem> Industrias => _industrias.ToSelectListItems(
            text: x => x.Nome,
            value: x => x.TenantId,
            options => options.Placeholder = "Selecione Industria...");

        [Required]
        public int Industria { get; set; }
        [Required]
        public int User { get; set; }

        public List<Claim> FeatureClaims { get; set; } = default!;
    }
}
