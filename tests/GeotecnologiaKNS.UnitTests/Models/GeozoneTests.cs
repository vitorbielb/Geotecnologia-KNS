using FluentAssertions;
using GeotecnologiaKNS.Models;
using NuGet.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeotecnologiaKNS.UnitTests.Models
{
    public class GeozoneTests
    {
        // Metodo_Cenario_ResultadoEsperado

        [Fact]
        public void GetUtm_ValidJson_ShouldReturnValidUtm()
        {
            // arrange
            var geozone = new Geozone();

            const string ValidJson =
            """
            [
                {"lat": -16.662726909086594, "lng": -49.340331005859376}, 
                {"lat": -16.69627241710094, "lng": -49.33724110107422},
                {"lat": -16.671607174992513, "lng": -49.31252186279297}
            ]
            """;

            geozone.Utm = ValidJson.FromJson<Vertice[]>();

            // act
            var utm = geozone.Utm;

            // assert
            utm.Should().NotBeNull();
            utm.Should().HaveCount(3);

            utm[0].Lat.Should().Be(-16.662726909086594);
            utm[0].Lng.Should().Be(-49.340331005859376);

            utm[1].Lat.Should().Be(-16.69627241710094);
            utm[1].Lng.Should().Be(-49.33724110107422);

            utm[2].Lat.Should().Be(-16.671607174992513);
            utm[2].Lng.Should().Be(-49.31252186279297);
        }

        [Fact]
        public void GetUtm_JsonDoGoogleMaps_ShouldReturnValidUtm()
        {
            // arrange
            // Formato exatamente como o mapa grava o polígono (chaves minúsculas).
            var geozone = new Geozone
            {
                UtmAsJson =
                """
                [
                    {"lat":-16.662726909086594,"lng":-49.340331005859376},
                    {"lat":-16.69627241710094,"lng":-49.33724110107422}
                ]
                """
            };

            // act
            var utm = geozone.Utm;

            // assert
            utm.Should().HaveCount(2);
            utm[0].Lat.Should().Be(-16.662726909086594);
            utm[0].Lng.Should().Be(-49.340331005859376);
            utm[1].Lat.Should().Be(-16.69627241710094);
            utm[1].Lng.Should().Be(-49.33724110107422);
        }

        [Fact]
        public void SetUtm_Vertices_ShouldSerializeInCamelCase()
        {
            // arrange
            var geozone = new Geozone();

            // act
            geozone.Utm = new[] { new Vertice { Lat = -16.5, Lng = -49.5 } };

            // assert
            geozone.UtmAsJson.Should().Contain("\"lat\"");
            geozone.UtmAsJson.Should().Contain("\"lng\"");
            geozone.Utm.Should().ContainSingle();
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("isso não é json")]
        public void GetUtm_JsonInvalido_ShouldReturnEmpty(string utmAsJson)
        {
            // arrange
            var geozone = new Geozone { UtmAsJson = utmAsJson };

            // act
            var utm = geozone.Utm;

            // assert
            utm.Should().BeEmpty();
        }
    }
}
