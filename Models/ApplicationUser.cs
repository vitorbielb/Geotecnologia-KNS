using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Claims;

namespace GeotecnologiaKNS.Models
{
    public class ApplicationUser : IdentityUser, IIndustriaInfo, IPrimaryKeyInfo<string>
    {
        [PersonalData]
        [ForeignKey(nameof(Industria))]
        public int TenantId { get; set; }

        public Industria Industria { get; set; }
    }
}
