using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace GeotecnologiaKNS.Models
{
    public class Geozone : ITenantInfo, IPrimaryKeyInfo<int>
    {
        /// <summary>
        /// O polígono é gravado pelo mapa (Google Maps) como <c>[{"lat":..,"lng":..}]</c>.
        /// O System.Text.Json é case-sensitive por padrão, o que faria os vértices
        /// serem lidos como (0,0); daí PropertyNameCaseInsensitive e a escrita em camelCase.
        /// </summary>
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        [Key]
        public int Id { get; set; }

        public int TenantId { get; set; }

        [NotMapped]
        public Vertice[] Utm
        {
            get => Deserialize(UtmAsJson);
            set => UtmAsJson = JsonSerializer.Serialize(value ?? Array.Empty<Vertice>(), SerializerOptions);
        }

        [Column("Utm")]
        [Required]
        public string UtmAsJson { get; set; } = "[]";

        private static Vertice[] Deserialize(string? utmAsJson)
        {
            if (string.IsNullOrWhiteSpace(utmAsJson))
            {
                return Array.Empty<Vertice>();
            }

            try
            {
                return JsonSerializer.Deserialize<Vertice[]>(utmAsJson, SerializerOptions) ?? Array.Empty<Vertice>();
            }
            catch (JsonException)
            {
                return Array.Empty<Vertice>();
            }
        }
    }
}
