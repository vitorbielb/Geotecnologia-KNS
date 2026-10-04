using FluentAssertions;
using GeotecnologiaKNS.Geo.Ingestao;

namespace GeotecnologiaKNS.UnitTests.Ingestao
{
    /// <summary>
    /// O que uma carga pode afirmar ter coberto.
    /// </summary>
    /// <remarks>
    /// Cobertura é uma afirmação forte: "este município foi carregado por
    /// inteiro". Dela depende a lacuna — a consulta a um município não coberto
    /// vira pedido de carga, e o sistema se atende sozinho. Se a afirmação for
    /// falsa, a consulta deixa de virar pedido e o município fica faltando para
    /// sempre, sem erro nenhum aparecer.
    ///
    /// O caso real: uma carga de Goiás trouxe dois imóveis cujo código do CAR
    /// aponta para o Distrito Federal, porque no SICAR o cod_municipio_ibge e o
    /// município embutido no código do CAR às vezes discordam. Sem escopo, dois
    /// imóveis bastariam para dar um estado inteiro por carregado.
    /// </remarks>
    public class EscopoDeCoberturaTests
    {
        /// <summary>
        /// Reproduz a regra que a baixa por UF aplica, pelo mesmo caminho que
        /// ela usa: o estado sai do próprio código do município.
        /// </summary>
        private static Func<string, bool> EscopoDaUf(string uf) =>
            codigo => BaixadorBaseCar.UfDoMunicipio(codigo) == uf;

        [Fact]
        public void CargaPorUf_DeveCobrirOsMunicipiosDoEstado()
        {
            var escopo = EscopoDaUf("GO");

            escopo("5208707").Should().BeTrue();   // Goiânia
            escopo("5201405").Should().BeTrue();   // Anápolis
        }

        [Fact]
        public void CargaPorUf_NaoDeveCobrirMunicipioDeOutroEstado()
        {
            // Os dois imóveis do Distrito Federal que vieram na camada de Goiás:
            // gravados sim, cobertos não.
            EscopoDaUf("GO")("5300108").Should().BeFalse();
        }

        [Fact]
        public void CargaPorMunicipio_DeveCobrirSomenteOPedido()
        {
            Func<string, bool> escopo = codigo => codigo == "1702000";

            escopo("1702000").Should().BeTrue();
            escopo("1720259").Should().BeFalse();
        }

        [Fact]
        public void CodigoForaDoPadrao_NaoDeveEntrarEmEscopoAlgum()
        {
            // Código ilegível não pode cair no estado por acidente; ficar de
            // fora da cobertura faz o município continuar gerando lacuna, que é
            // o lado seguro do erro.
            var escopo = EscopoDaUf("GO");

            escopo("").Should().BeFalse();
            escopo("9999999").Should().BeFalse();
            escopo("520870").Should().BeFalse();
        }
    }
}
