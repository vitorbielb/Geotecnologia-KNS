namespace GeotecnologiaKNS.Models
{
    /// <summary>
    /// Dados para o partial que desenha um perímetro vindo da base do CAR.
    /// </summary>
    public class PerimetroMapaViewModel
    {
        /// <summary>Perímetro em GeoJSON. Vazio desenha só o marcador do centro.</summary>
        public string? GeoJson { get; set; }

        public double CentroLat { get; set; }

        public double CentroLng { get; set; }

        public string? Titulo { get; set; }

        /// <summary>Id do elemento no DOM. Precisa ser único quando há mais de um mapa na página.</summary>
        public string ElementoId { get; set; } = "map";

        public int AlturaEmPixels { get; set; } = 380;

        public static PerimetroMapaViewModel DaPropriedade(Propriedade propriedade, string elementoId = "map")
        {
            return new PerimetroMapaViewModel
            {
                GeoJson = propriedade.PerimetroGeoJson,
                CentroLat = propriedade.Latitude,
                CentroLng = propriedade.Longitude,
                Titulo = propriedade.NomePropriedade,
                ElementoId = elementoId
            };
        }
    }
}
