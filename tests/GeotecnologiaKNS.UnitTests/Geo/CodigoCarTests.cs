using FluentAssertions;
using GeotecnologiaKNS.Geo;

namespace GeotecnologiaKNS.UnitTests.Geo
{
    public class CodigoCarTests
    {
        // Metodo_Cenario_ResultadoEsperado

        private const string CodigoValido = "MT-5107925-A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6";

        [Fact]
        public void Normalizar_CodigoValido_DeveManterOCodigo()
        {
            CodigoCar.Normalizar(CodigoValido).Should().Be(CodigoValido);
        }

        [Theory]
        [InlineData("mt-5107925-a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6")]
        [InlineData("  MT-5107925-A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6  ")]
        [InlineData("MT-5107925-A1B2C3D4E5F6 A7B8C9D0E1F2A3B4C5D6")]
        public void Normalizar_VariacoesDeDigitacao_DeveConvergirParaOMesmoCodigo(string digitado)
        {
            CodigoCar.Normalizar(digitado).Should().Be(CodigoValido);
        }

        [Fact]
        public void Normalizar_FormatoDoReciboDoSicar_DeveSerAceito()
        {
            // É assim que o código aparece no recibo do SICAR e que o usuário
            // copia e cola: o hash vem separado em grupos de quatro por pontos.
            const string ComoAparece = "TO-1702000-6BC8.B06A.2D45.4929.BAD5.2CBE.5A2C.5475";
            const string Esperado = "TO-1702000-6BC8B06A2D454929BAD52CBE5A2C5475";

            CodigoCar.Normalizar(ComoAparece).Should().Be(Esperado);
            CodigoCar.EhValido(ComoAparece).Should().BeTrue();
            CodigoCar.ExtrairUf(ComoAparece).Should().Be("TO");
            CodigoCar.ExtrairCodigoIbge(ComoAparece).Should().Be("1702000");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("12345")]
        [InlineData("MT-510792-A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6")]  // IBGE com 6 dígitos
        [InlineData("M-5107925-A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6")]  // UF com 1 letra
        [InlineData("MT-5107925-XYZ")]                               // hash curto e fora de hexadecimal
        public void Normalizar_CodigoInvalido_DeveRetornarNulo(string? digitado)
        {
            CodigoCar.Normalizar(digitado).Should().BeNull();
            CodigoCar.EhValido(digitado).Should().BeFalse();
        }

        [Fact]
        public void ExtrairUf_CodigoValido_DeveRetornarASigla()
        {
            CodigoCar.ExtrairUf(CodigoValido).Should().Be("MT");
        }

        [Fact]
        public void ExtrairCodigoIbge_CodigoValido_DeveRetornarOCodigoDoMunicipio()
        {
            CodigoCar.ExtrairCodigoIbge(CodigoValido).Should().Be("5107925");
        }

        [Fact]
        public void ExtrairUf_CodigoInvalido_DeveRetornarNulo()
        {
            CodigoCar.ExtrairUf("qualquer coisa").Should().BeNull();
            CodigoCar.ExtrairCodigoIbge("qualquer coisa").Should().BeNull();
        }
    }
}
