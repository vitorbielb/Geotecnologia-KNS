using NuGet.Protocol;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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
            get => UtmAsJson.FromJson<Vertice[]>(); 
            set => UtmAsJson = value.ToJson(); 
        }

        [Column("Utm")]
        public string UtmAsJson { get; set; } = "[]";
    }
}
