using System.Text;
using FluentAssertions;
using GeotecnologiaKNS.Geo.Ingestao;

namespace GeotecnologiaKNS.UnitTests.Ingestao
{
    /// <summary>
    /// Em que codificação os textos de um shapefile são lidos.
    /// </summary>
    /// <remarks>
    /// A biblioteca honra o arquivo <c>.cpg</c>/<c>.cst</c> ao lado do
    /// shapefile, e assume UTF-8 quando ele não existe. Órgão brasileiro
    /// publica em Latin1, então a suposição sai errada justamente onde não há
    /// declaração.
    ///
    /// Apareceu no laudo: o assentamento "PA PROVÍNCIA" do INCRA saía como
    /// "PA PROVNCIA" com um caractere de substituição no meio. Pequeno perto dos
    /// outros defeitos desta base, mas impresso num documento que a indústria
    /// entrega a auditoria.
    /// </remarks>
    public class CodificacaoDeShapefileTests : IDisposable
    {
        private readonly string _pasta = Path.Combine(
            Path.GetTempPath(), $"codificacao-{Guid.NewGuid():N}");

        public CodificacaoDeShapefileTests() => Directory.CreateDirectory(_pasta);

        public void Dispose()
        {
            try
            {
                Directory.Delete(_pasta, recursive: true);
            }
            catch (IOException)
            {
                // Temporário preso não é motivo para o teste falhar.
            }
        }

        /// <summary>Monta um shapefile de mentira com o cabeçalho DBF pedido.</summary>
        private string Montar(string nome, byte? identificador = null, string? declaracao = null)
        {
            var shp = Path.Combine(_pasta, $"{nome}.shp");
            File.WriteAllBytes(shp, new byte[100]);

            if (identificador is { } id)
            {
                // Byte 29 do cabeçalho do DBF é o "language driver id".
                var dbf = new byte[32];
                dbf[29] = id;
                File.WriteAllBytes(Path.ChangeExtension(shp, ".dbf"), dbf);
            }

            if (declaracao is not null)
            {
                File.WriteAllText(Path.Combine(_pasta, $"{nome}.cpg"), declaracao);
            }

            return shp;
        }

        [Fact]
        public void ArquivoQueDeclaraACodificacao_DeveFicarPorContaDaBiblioteca()
        {
            // O SICAR e os GeoServer do IBGE e do INPE mandam .cst dizendo
            // ISO-8859-1, e a biblioteca já acerta sozinha. Devolver nulo aqui é
            // o que preserva esse caminho.
            CodificacaoDeShapefile.Detectar(Montar("declarado", declaracao: "ISO-8859-1"))
                .Should().BeNull();
        }

        [Theory]
        [InlineData((byte)0x57)]  // ANSI — é o que o INCRA declara
        [InlineData((byte)0x03)]
        [InlineData((byte)0x58)]
        [InlineData((byte)0x59)]
        public void SemDeclaracao_ComCabecalhoAnsi_DeveSerLatin1(byte identificador)
        {
            CodificacaoDeShapefile.Detectar(Montar($"ansi{identificador}", identificador))
                .Should().Be(Encoding.Latin1);
        }

        [Fact]
        public void SemDeclaracaoESemCabecalho_DeveCairEmLatin1()
        {
            // 0x00 é "não declarado", o valor mais comum. Latin1 é o palpite
            // certo para origem brasileira — e é o palpite que a biblioteca não
            // dá, porque ela assume UTF-8.
            CodificacaoDeShapefile.Detectar(Montar("mudo", identificador: 0x00))
                .Should().Be(Encoding.Latin1);
        }

        [Fact]
        public void SemDbfNenhum_DeveCairEmLatin1()
        {
            CodificacaoDeShapefile.Detectar(Montar("sodbf"))
                .Should().Be(Encoding.Latin1);
        }

        [Fact]
        public void CabecalhoDesconhecido_DeveCairEmLatin1()
        {
            // Página de código do MS-DOS, que ficou de fora do mapeamento por
            // exigir pacote extra. Cair em Latin1 erra menos que cair em UTF-8:
            // texto acentuado em português vira caractere errado, e não
            // caractere de substituição.
            CodificacaoDeShapefile.Detectar(Montar("dos", identificador: 0x02))
                .Should().Be(Encoding.Latin1);
        }

        [Fact]
        public void ArquivoInexistente_NaoDeveExplodir()
        {
            CodificacaoDeShapefile.Detectar(Path.Combine(_pasta, "nao-existe.shp"))
                .Should().Be(Encoding.Latin1);
        }
    }
}
