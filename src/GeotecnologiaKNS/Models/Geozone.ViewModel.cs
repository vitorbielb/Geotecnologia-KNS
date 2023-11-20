using NuGet.Protocol;

namespace GeotecnologiaKNS.Models
{
    public class GeozoneViewModel
    {
        private string utmAsJson = "[]";

        public double CenterLat { get; set; }
        public double CenterLong { get; set; }

        public Vertice[] Utm
        {
            get => UtmAsJson.FromJson<Vertice[]>();
            set => UtmAsJson = value.ToJson();
        }

        public string UtmAsJson { get => utmAsJson.ToLower(); set => utmAsJson = value; }
    }
}