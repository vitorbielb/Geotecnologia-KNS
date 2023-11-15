using System.ComponentModel.DataAnnotations;

namespace GeotecnologiaKNS.Models
{
    public class ApplicationRole : IdentityRole
    {
        [Required]
        public int TenantId { get; set; }

        public bool Custom { get; set; }

        public List<IdentityRoleClaim<string>> Claims { get; set; } = default!;
    }
}
