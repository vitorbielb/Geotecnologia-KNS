using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace GeotecnologiaKNS.Models
{
    public class Geozone : ITenantInfo, IPrimaryKeyInfo<int>
    {
        [Key]
        public int Id { get; set; }

        public int TenantId { get; set; }

        [NotMapped]
        public Vertice[] Utm
        {
            get => JsonSerializer.Deserialize<Vertice[]>(UtmAsJson) ?? Array.Empty<Vertice>();
            set => UtmAsJson = JsonSerializer.Serialize(value ?? Array.Empty<Vertice>());
        }

        [Column("Utm")]
        [Required]
        public string UtmAsJson { get; set; } = "[]";
    }
}