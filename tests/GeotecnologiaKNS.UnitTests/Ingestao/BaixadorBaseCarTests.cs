using FluentAssertions;
using GeotecnologiaKNS.Geo.Ingestao;

namespace GeotecnologiaKNS.UnitTests.Ingestao
{
    /// <summary>
    /// A conversão de código de município em unidade da federação.
    /// </summary>
    /// <remarks>
    /// Parece detalhe e não é: a base do CAR é publicada pelo SICAR em uma
    /// camada por estado, e quem pede um município informa só o código do IBGE.
    /// Errar o estado faz a consulta ir à camada errada e voltar vazia — o
    /// município ficaria eternamente sem cobertura, sem erro nenhum aparecer.
    /// </remarks>
    public class BaixadorBaseCarTests
    {
        [Theory]
        [InlineData("5107925", "MT")]  // Sorriso
        [InlineData("1506807", "PA")]  // Paragominas
        [InlineData("1702000", "TO")]  // Araguaçu
        [InlineData("5002704", "MS")]  // Campo Grande
        [InlineData("1100205", "RO")]  // Porto Velho
        [InlineData("5208707", "GO")]  // Goiânia
        [InlineData("3550308", "SP")]  // São Paulo
        [InlineData("5300108", "DF")]  // Brasília
        public void UfDoMunicipio_DeveAcertarOEstado(string codigoIbge, string esperado)
        {
            BaixadorBaseCar.UfDoMunicipio(codigoIbge).Should().Be(esperado);
        }

        [Theory]
        [InlineData("510792")]     // curto demais
        [InlineData("51079255")]   // longo demais
        [InlineData("9999999")]    // prefixo que não é estado
        [InlineData("")]
        [InlineData(null)]
        public void UfDoMunicipio_CodigoInvalido_DeveSerRecusado(string? codigoIbge)
        {
            // Recusar é melhor que chutar: um código inválido que virasse uma UF
            // qualquer faria a baixa consultar a camada errada e devolver zero
            // imóveis como se o município não tivesse cadastro algum.
            BaixadorBaseCar.UfDoMunicipio(codigoIbge).Should().BeNull();
        }

        [Fact]
        public void Ufs_DeveCobrirOPaisInteiro()
        {
            // Vinte e seis estados mais o Distrito Federal. Faltar um significa
            // uma região do país que o sistema não consegue carregar.
            BaixadorBaseCar.Ufs.Should().HaveCount(27);
            BaixadorBaseCar.Ufs.Should().OnlyHaveUniqueItems();
        }

        [Theory]
        [InlineData("MT")]
        [InlineData("mt")]
        [InlineData("PA")]
        public void UfConhecida_DeveAceitarMaiusculaEMinuscula(string uf)
        {
            BaixadorBaseCar.UfConhecida(uf).Should().BeTrue();
        }

        [Theory]
        [InlineData("XX")]
        [InlineData("BRASIL")]
        [InlineData("")]
        [InlineData(null)]
        public void UfConhecida_ValorInvalido_DeveSerRecusado(string? uf)
        {
            BaixadorBaseCar.UfConhecida(uf).Should().BeFalse();
        }

        [Fact]
        public async Task BaixarUfAsync_UfInvalida_DeveFalharSemChamarAOrigem()
        {
            // Erro de digitação não pode virar requisição ao SICAR: o GeoServer
            // responderia com uma exceção XML que chegaria aqui como "zip
            // corrompido", escondendo que o problema era o estado.
            var resultado = await new BaixadorBaseCar(null!, null!).BaixarUfAsync("XX");

            resultado.Sucesso.Should().BeFalse();
            resultado.Erro.Should().Contain("XX");
            resultado.Paginas.Should().Be(0);
        }

        [Fact]
        public async Task BaixarMunicipioAsync_CodigoInvalido_DeveFalharSemChamarAOrigem()
        {
            var resultado = await new BaixadorBaseCar(null!, null!).BaixarMunicipioAsync("123");

            resultado.Sucesso.Should().BeFalse();
            resultado.Erro.Should().Contain("7 dígitos");
            resultado.Paginas.Should().Be(0);
        }
    }
}
