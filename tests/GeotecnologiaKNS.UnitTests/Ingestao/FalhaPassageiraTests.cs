using FluentAssertions;
using GeotecnologiaKNS.Geo.Ingestao;

namespace GeotecnologiaKNS.UnitTests.Ingestao
{
    /// <summary>
    /// Quando insistir numa requisição que falhou, e quando desistir.
    /// </summary>
    /// <remarks>
    /// O PRODES do Cerrado são trinta e duas páginas. Na primeira tentativa a
    /// décima nona voltou 504 e a carga inteira foi embora — meia hora de
    /// trabalho perdida por um engasgo de um servidor que responde bem no
    /// minuto seguinte. Na tentativa com repetição, o mesmo servidor devolveu
    /// 502 e 504 em cinco momentos diferentes e a carga fechou com as
    /// 1.566.542 feições que a origem declara.
    ///
    /// Quanto maior a camada, mais requisições, e maior a chance de uma delas
    /// pegar o servidor num mau momento. As camadas que mais importam são
    /// justamente as que mais sofrem com isso.
    ///
    /// A decisão erra para os dois lados. Insistir em 4xx adia a mensagem que
    /// explica o que está errado e esconde o defeito atrás de três esperas.
    /// Não insistir em 5xx joga fora a carga por um problema que não é nosso.
    /// </remarks>
    public class FalhaPassageiraTests
    {
        /// <summary>O erro que o download levanta ao ver uma resposta de erro.</summary>
        private static Exception RespostaDaOrigem(int codigo, string razao) =>
            new InvalidOperationException($"A origem respondeu {codigo} {razao}.");

        [Theory]
        [InlineData(500, "Internal Server Error")]
        [InlineData(502, "Bad Gateway")]
        [InlineData(503, "Service Unavailable")]
        [InlineData(504, "Gateway Time-out")]
        public void ErroDoServidor_DeveInsistir(int codigo, string razao)
        {
            RecarregadorDeCamadas.EhPassageira(RespostaDaOrigem(codigo, razao))
                .Should().BeTrue();
        }

        [Fact]
        public void ExcessoDeRequisicoes_DeveInsistir()
        {
            // Pedir devagar é exatamente o que o servidor está mandando fazer.
            RecarregadorDeCamadas.EhPassageira(RespostaDaOrigem(429, "Too Many Requests"))
                .Should().BeTrue();
        }

        [Theory]
        [InlineData(400, "Bad Request")]
        [InlineData(401, "Unauthorized")]
        [InlineData(403, "Forbidden")]
        [InlineData(404, "Not Found")]
        public void ErroDoPedido_NaoDeveInsistir(int codigo, string razao)
        {
            // Pedido errado continua errado na terceira vez. O 403 é o caso
            // concreto: é assim que a FUNAI recusa, e insistir só atrasaria a
            // queda para a reserva do IBGE.
            RecarregadorDeCamadas.EhPassageira(RespostaDaOrigem(codigo, razao))
                .Should().BeFalse();
        }

        [Fact]
        public void FalhaDeRede_DeveInsistir()
        {
            RecarregadorDeCamadas.EhPassageira(new HttpRequestException("conexão perdida"))
                .Should().BeTrue();
        }

        [Fact]
        public void TempoEsgotado_DeveInsistir()
        {
            // O zip do PRODES leva minutos; um tempo esgotado aqui é a rede
            // engasgando, não a origem dizendo que o pedido está errado.
            RecarregadorDeCamadas.EhPassageira(new TaskCanceledException("tempo esgotado"))
                .Should().BeTrue();
        }

        [Fact]
        public void FalhaDeDisco_DeveInsistir()
        {
            RecarregadorDeCamadas.EhPassageira(new IOException("escrita interrompida"))
                .Should().BeTrue();
        }

        [Fact]
        public void ArquivoQueNaoEhZip_NaoDeveInsistir()
        {
            // A origem respondeu 200 com outra coisa no corpo — baixar de novo
            // traz a mesma outra coisa. É defeito de consulta, não de momento.
            RecarregadorDeCamadas.EhPassageira(
                new InvalidOperationException("O que a origem devolveu não é um zip."))
                .Should().BeFalse();
        }

        [Fact]
        public void CargaRecusadaPelaConferencia_NaoDeveInsistir()
        {
            // Repetir o download não muda o que a origem publicou. Insistir
            // aqui transformaria uma recusa explicada em três esperas e a mesma
            // recusa no fim.
            RecarregadorDeCamadas.EhPassageira(
                new InvalidOperationException(
                    "A origem de prodes-cerrado declara 1.566.542 feições e a carga leu 50.000."))
                .Should().BeFalse();
        }

        [Fact]
        public void AbrangenciaQueNaoPodeSerResolvida_NaoDeveInsistir()
        {
            // Falta carregar a camada de biomas. Nenhuma espera resolve isso.
            RecarregadorDeCamadas.EhPassageira(
                new InvalidOperationException(
                    "A camada prodes-cerrado declara cobrir Cerrado, mas os limites desses " +
                    "biomas não estão carregados."))
                .Should().BeFalse();
        }
    }
}
