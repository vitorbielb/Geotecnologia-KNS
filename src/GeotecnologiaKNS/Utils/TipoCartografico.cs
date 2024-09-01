using System.Collections.ObjectModel;

namespace GeotecnologiaKNS.Utils
{
    public static class TipoCartografico
    {
        private static readonly IReadOnlyDictionary<string, string> Mapeamento =
            new ReadOnlyDictionary<string, string>(new Dictionary<string, string>
            {
                ["SUC"] = "Sobrep. Unidade de Conservação",
                ["STI"] = "Sobrep. Terra Indígena",
                ["SQ"] = "Sobrep. Quilombolas",
                ["SOP"] = "Sobrep. Outros Perímetros",
                ["SEI"] = "Sobrep. Embargo IBAMA",
                ["DAEI"] = "Distância Área Embargada IBAMA",
                ["DETER"] = "DETER",
                ["IP"] = "Incapacidade Produtiva",
                ["LB"] = "Localização Bioma",
                ["UPC"] = "Unidade Produtiva Contínua",
                ["PA"] = "Perímetro Adulterado",
                ["OE"] = "Outras Evidências",
                ["IP7"] = "Imagem PRODES 2007",
                ["IP8"] = "Imagem PRODES 2008",
                ["IP9"] = "Imagem PRODES 2009",
                ["IP10"] = "Imagem PRODES 2010",
                ["IP11"] = "Imagem PRODES 2011",
                ["IP12"] = "Imagem PRODES 2012",
                ["IP13"] = "Imagem PRODES 2013",
                ["IP14"] = "Imagem PRODES 2014",
                ["IP15"] = "Imagem PRODES 2015",
                ["IP16"] = "Imagem PRODES 2016",
                ["IP17"] = "Imagem PRODES 2017",
                ["IP18"] = "Imagem PRODES 2018",
                ["IP19"] = "Imagem PRODES 2019",
                ["IP20"] = "Imagem PRODES 2020",
                ["ZI"] = "Zona de influência",
                ["MF"] = "Mapa fundiário",
                ["UC"] = "Uso do solo e cobertura"
            });

        public static string GetDescricao(string? valor)
        {
            if (string.IsNullOrWhiteSpace(valor))
                return string.Empty;

            return Mapeamento.TryGetValue(valor, out var descricao)
                ? descricao
                : valor;
        }
    }
}