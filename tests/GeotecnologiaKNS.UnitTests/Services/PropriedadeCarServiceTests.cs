using FluentAssertions;
using GeotecnologiaKNS.Geo.Services;
using GeotecnologiaKNS.Models;
using GeotecnologiaKNS.Services;

namespace GeotecnologiaKNS.UnitTests.Services
{
    public class PropriedadeCarServiceTests
    {
        // Metodo_Cenario_ResultadoEsperado

        private const string CodigoValido = "MT-5107925-A1B2C3D4E5F6A7B8C9D0E1F2A3B4C5D6";

        private static ImovelCarDto Imovel(
            double? areaHa = 1234.5,
            double? areaCalculadaHa = null,
            string? uf = "MT",
            string? municipio = "Sorriso") => new(
                CodigoCar: CodigoValido,
                PerimetroGeoJson: """{"type":"Polygon","coordinates":[]}""",
                CentroLat: -12.54,
                CentroLng: -55.71,
                AreaHa: areaHa,
                AreaCalculadaHa: areaCalculadaHa,
                Municipio: municipio,
                Uf: uf,
                Situacao: "AT",
                Tipo: "IRU",
                AtualizadoEmOrigem: null,
                Origem: "MapBiomas/CAR-Camada-Completa",
                BaseCarregadaEm: new DateTime(2026, 9, 1));

        private sealed class UserContextFalso : GeotecnologiaKNS.Utils.IUserContext
        {
            public int? TenantId { get; set; } = 1;
            public bool IsApplicationAdmin => false;
            public bool IsTenantAdmin => true;
        }

        private static PropriedadeCarService Service(ICarLookupService lookup) =>
            new(lookup, new UserContextFalso(),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<PropriedadeCarService>.Instance);

        [Fact]
        public async Task ConsultarAsync_CodigoInvalido_NaoDeveConsultarABase()
        {
            var lookup = new LookupFalso();
            var resultado = await Service(lookup).ConsultarAsync("codigo errado");

            resultado.Resultado.Should().Be(ResultadoConsultaCar.CodigoInvalido);
            resultado.Sucesso.Should().BeFalse();
            lookup.ConsultasFeitas.Should().Be(0, "não faz sentido consultar a base com um código malformado");
        }

        [Fact]
        public async Task ConsultarAsync_BaseNaoCarregada_DeveAvisarQueABaseEstaIndisponivel()
        {
            var lookup = new LookupFalso { BaseDisponivel = false };
            var resultado = await Service(lookup).ConsultarAsync(CodigoValido);

            resultado.Resultado.Should().Be(ResultadoConsultaCar.BaseIndisponivel);
            resultado.Sucesso.Should().BeFalse();
            resultado.Mensagem.Should().Contain("não foi importada");
        }

        [Fact]
        public async Task ConsultarAsync_SemBancoGeoespacial_DeveDistinguirDaBaseVazia()
        {
            // Falta de configuração e falta de carga têm remédios diferentes;
            // a mensagem precisa dizer qual dos dois é.
            var lookup = new LookupFalso { EstaConfigurado = false };
            var resultado = await Service(lookup).ConsultarAsync(CodigoValido);

            resultado.Resultado.Should().Be(ResultadoConsultaCar.NaoConfigurado);
            resultado.Mensagem.Should().Contain("não está configurado");
            lookup.ConsultasFeitas.Should().Be(0);
        }

        [Fact]
        public async Task ConsultarAsync_CarAusenteNaBase_NaoDeveAfirmarQueOCarEhInvalido()
        {
            var lookup = new LookupFalso { Imovel = null, MunicipioCoberto = true };
            var resultado = await Service(lookup).ConsultarAsync(CodigoValido);

            resultado.Resultado.Should().Be(ResultadoConsultaCar.NaoEncontrado);

            // Ausência na base não prova que o CAR é inválido: pode ser cadastro
            // recente, ainda não publicado pelo SICAR.
            resultado.Mensagem.Should().Contain("cadastro recente");
        }

        [Fact]
        public async Task ConsultarAsync_MunicipioNaoCarregado_DeveSerDistintoDeCarInexistente()
        {
            // Sem esta distinção, o suporte recebe como a mesma queixa uma
            // pendência de carga, que é nossa, e um número errado, que é do cliente.
            var lookup = new LookupFalso { Imovel = null, MunicipioCoberto = false };
            var resultado = await Service(lookup).ConsultarAsync(CodigoValido);

            resultado.Resultado.Should().Be(ResultadoConsultaCar.MunicipioNaoCoberto);
            resultado.Mensagem.Should().Contain("ainda não foi carregada");
            resultado.Mensagem.Should().Contain("5107925", "a mensagem precisa dizer qual município");
        }

        [Fact]
        public async Task ConsultarAsync_MunicipioNaoCarregado_DeveRegistrarADemanda()
        {
            var lookup = new LookupFalso { Imovel = null, MunicipioCoberto = false };
            await Service(lookup).ConsultarAsync(CodigoValido);

            lookup.LacunasRegistradas.Should().ContainSingle()
                  .Which.Should().Be("MT-5107925-t1", "a fila de carga é alimentada pelo pedido real do cliente");
        }

        [Fact]
        public async Task ConsultarAsync_MunicipioCarregadoMasCarAusente_NaoDeveCulparACobertura()
        {
            var lookup = new LookupFalso { Imovel = null, MunicipioCoberto = true };
            var resultado = await Service(lookup).ConsultarAsync(CodigoValido);

            resultado.Resultado.Should().Be(ResultadoConsultaCar.NaoEncontrado);
            resultado.Mensagem.Should().Contain("município está na base");
            lookup.LacunasRegistradas.Should().BeEmpty("não há lacuna de cobertura a registrar");
        }

        [Fact]
        public async Task ConsultarAsync_CodigoComEspacosEMinusculas_DeveNormalizarAntesDeConsultar()
        {
            var lookup = new LookupFalso { Imovel = Imovel() };
            var resultado = await Service(lookup).ConsultarAsync("  mt-5107925-a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6 ");

            resultado.Sucesso.Should().BeTrue();
            lookup.UltimoCodigoConsultado.Should().Be(CodigoValido);
        }

        [Fact]
        public void Aplicar_ImovelDaBase_DevePreencherOsCamposDerivados()
        {
            var propriedade = new Propriedade();

            Service(new LookupFalso()).Aplicar(propriedade, Imovel());

            propriedade.CodigoCar.Should().Be(CodigoValido);
            propriedade.Municipio.Should().Be("Sorriso");
            propriedade.UnidadeFederativa.Should().Be(Estados.MT);
            propriedade.Latitude.Should().Be(-12.54);
            propriedade.Longitude.Should().Be(-55.71);
            propriedade.Area.Should().Be("1234.5");
            propriedade.SituacaoCar.Should().Be("AT");
            propriedade.PerimetroOrigem.Should().Be("MapBiomas/CAR-Camada-Completa");
            propriedade.PerimetroAtualizadoEm.Should().Be(new DateTime(2026, 9, 1));
            propriedade.TemPerimetro.Should().BeTrue();
        }

        [Fact]
        public void Aplicar_ImovelComAreaCalculada_DevePreferirAAreaDaGeometria()
        {
            var propriedade = new Propriedade();

            Service(new LookupFalso()).Aplicar(propriedade, Imovel(areaHa: 1000, areaCalculadaHa: 987.65));

            propriedade.Area.Should().Be("987.65", "a área declarada é o que o produtor informou");
        }

        [Fact]
        public void Aplicar_AreaFracionada_DeveGravarComPontoDecimal()
        {
            var propriedade = new Propriedade();

            Service(new LookupFalso()).Aplicar(propriedade, Imovel(areaHa: 1234.56));

            propriedade.Area.Should().NotContain(",");
        }

        [Fact]
        public void Aplicar_PerimetroDaBaseOficial_DeveValidarOCadastro()
        {
            var propriedade = new Propriedade { Validacao = Validacao.Pendente };

            Service(new LookupFalso()).Aplicar(propriedade, Imovel());

            propriedade.Validacao.Should().Be(Validacao.Validado);
        }

        [Fact]
        public void Aplicar_SemNomeInformado_DeveGerarUmNomePadrao()
        {
            var propriedade = new Propriedade { NomePropriedade = string.Empty };

            Service(new LookupFalso()).Aplicar(propriedade, Imovel());

            propriedade.NomePropriedade.Should().Be("Sorriso/MT - B4C5D6");
        }

        [Fact]
        public void Aplicar_ComNomeInformado_DevePreservarOApelidoDoUsuario()
        {
            var propriedade = new Propriedade { NomePropriedade = "Fazenda Boa Vista" };

            Service(new LookupFalso()).Aplicar(propriedade, Imovel());

            propriedade.NomePropriedade.Should().Be("Fazenda Boa Vista");
        }

        [Fact]
        public void Aplicar_UfDesconhecida_NaoDeveQuebrar()
        {
            var propriedade = new Propriedade();

            var acao = () => Service(new LookupFalso()).Aplicar(propriedade, Imovel(uf: "ZZ"));

            acao.Should().NotThrow();
        }

        private sealed class LookupFalso : ICarLookupService
        {
            public ImovelCarDto? Imovel { get; set; }
            public bool EstaConfigurado { get; set; } = true;
            public bool BaseDisponivel { get; set; } = true;

            /// <summary>Por padrão o município está coberto, para os testes antigos seguirem valendo.</summary>
            public bool MunicipioCoberto { get; set; } = true;

            public List<string> LacunasRegistradas { get; } = new();

            public Task<bool> MunicipioCobertoAsync(string codigoIbge, CancellationToken cancellationToken = default)
                => Task.FromResult(MunicipioCoberto);

            public Task RegistrarLacunaAsync(string codigoIbge, string uf, string codigoCar, int tenantId, CancellationToken cancellationToken = default)
            {
                LacunasRegistradas.Add($"{uf}-{codigoIbge}-t{tenantId}");
                return Task.CompletedTask;
            }
            public int ConsultasFeitas { get; private set; }
            public string? UltimoCodigoConsultado { get; private set; }

            public Task<ImovelCarDto?> ObterPorCodigoAsync(string codigoCar, CancellationToken cancellationToken = default)
            {
                ConsultasFeitas++;
                UltimoCodigoConsultado = codigoCar;
                return Task.FromResult(Imovel);
            }

            public Task<bool> BaseDisponivelAsync(CancellationToken cancellationToken = default)
                => Task.FromResult(BaseDisponivel);
        }
    }
}
