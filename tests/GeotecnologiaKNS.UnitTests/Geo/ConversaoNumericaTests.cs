using System.Globalization;
using FluentAssertions;
using GeotecnologiaKNS.Geo.Ingestao;

namespace GeotecnologiaKNS.UnitTests.Geo
{
    public class ConversaoNumericaTests
    {
        // Metodo_Cenario_ResultadoEsperado

        [Fact]
        public void ConverterParaDouble_ValorNumericoDoDbf_NaoDevePassarPelaCulturaCorrente()
        {
            // arrange
            // Campo numérico do .dbf chega boxed. Se for convertido com ToString()
            // sob pt-BR vira "1234,5" e o parser invariante devolveria null.
            var culturaOriginal = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");

            try
            {
                // act
                var resultado = SicarShapefileImporter.ConverterParaDouble(1234.5d);

                // assert
                resultado.Should().Be(1234.5d);
            }
            finally
            {
                CultureInfo.CurrentCulture = culturaOriginal;
            }
        }

        [Theory]
        [InlineData(1234.5d, 1234.5)]
        [InlineData(1234.5f, 1234.5)]
        [InlineData(42, 42.0)]
        [InlineData(42L, 42.0)]
        public void ConverterParaDouble_TiposNumericos_DeveConverterDireto(object bruto, double esperado)
        {
            SicarShapefileImporter.ConverterParaDouble(bruto).Should().BeApproximately(esperado, 0.0001);
        }

        [Theory]
        [InlineData("1234.5", 1234.5)]        // ponto decimal
        [InlineData("1234,5", 1234.5)]        // vírgula decimal — NumberStyles.Any leria 12345
        [InlineData("  880  ", 880.0)]
        [InlineData("1.234,56", 1234.56)]     // pt-BR: milhar com ponto, decimal com vírgula
        [InlineData("1,234.56", 1234.56)]     // en-US: milhar com vírgula, decimal com ponto
        [InlineData("1.234.567", 1234567.0)]  // só milhar
        [InlineData("-12,5", -12.5)]
        public void ConverterParaDouble_Texto_DeveResolverOSeparadorDecimal(string bruto, double esperado)
        {
            SicarShapefileImporter.ConverterParaDouble(bruto).Should().BeApproximately(esperado, 0.0001);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("sem área")]
        public void ConverterParaDouble_ValorInvalido_DeveRetornarNulo(object? bruto)
        {
            SicarShapefileImporter.ConverterParaDouble(bruto).Should().BeNull();
        }

        [Fact]
        public void ConverterParaDouble_Decimal_DeveConverter()
        {
            SicarShapefileImporter.ConverterParaDouble(1234.5m).Should().Be(1234.5d);
        }
    }
}
