using FluentAssertions;
using GeotecnologiaKNS.Analises;
using GeotecnologiaKNS.Data;
using GeotecnologiaKNS.Models;
using GeotecnologiaKNS.Utils;
using Microsoft.EntityFrameworkCore;

namespace GeotecnologiaKNS.UnitTests.Analises
{
    /// <summary>
    /// O que acontece com quem personalizou a política quando o protocolo ganha
    /// uma regra nova.
    /// </summary>
    /// <remarks>
    /// Descoberto na prática: a IND-001 entrou no protocolo e não chegou a
    /// nenhuma indústria, porque todas as que importavam já tinham política
    /// própria — copiada quando o protocolo tinha dez regras. Regra nova que não
    /// alcança a base de clientes mais antiga é regra que não existe.
    /// </remarks>
    public class ProtocoloEvoluiTests
    {
        private const int TenantA = 1;

        private sealed class ContextoFalso : IUserContext
        {
            public int? TenantId { get; set; }
            public bool IsApplicationAdmin => false;
            public bool IsTenantAdmin => true;
        }

        private static ApplicationDbContext Contexto(string banco, int? tenantId) =>
            new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(banco).Options,
                new ContextoFalso { TenantId = tenantId });

        /// <summary>Política antiga: o protocolo de hoje menos a regra mais nova.</summary>
        private static async Task PoliticaDesatualizadaAsync(ApplicationDbContext contexto)
        {
            var politica = new PoliticaTenant
            {
                TenantId = TenantA,
                Nome = "Protocolo da indústria",
                Regras = PoliticaAnalise.Padrao().Regras
                    .Where(r => r.Codigo != "IND-001")
                    .Select(r => new RegraTenant
                    {
                        Codigo = r.Codigo,
                        Descricao = r.Descricao,
                        Tipo = r.Tipo,
                        Restricao = r.Restricao,
                        CadeiaIndireta = r.CadeiaIndireta,
                        Severidade = (int)r.Severidade,
                        AreaMinimaHa = r.AreaMinimaHa,
                        PercentualMinimo = r.PercentualMinimo,
                        AnoMinimo = r.AnoMinimo,
                        Fundamento = r.Fundamento,
                        Ativa = true
                    })
                    .ToList()
            };

            contexto.Politicas.Add(politica);
            await contexto.SaveChangesAsync();
        }

        [Fact]
        public async Task RegraNovaDoProtocolo_DeveChegarNaAnalise()
        {
            var banco = Guid.NewGuid().ToString();

            using (var escrita = Contexto(banco, TenantA))
            {
                await PoliticaDesatualizadaAsync(escrita);
            }

            using var leitura = Contexto(banco, tenantId: null);
            var politica = await new PoliticaAnaliseRepository(leitura).ObterDoTenantAsync(TenantA);

            politica.Regras.Should().Contain(r => r.Codigo == "IND-001");
        }

        [Fact]
        public async Task RegraNovaDoProtocolo_DeveEntrarComoInformativa()
        {
            // Mesmo que o protocolo a traga mais severa. Mudar o veredito de
            // laudos por uma regra que ninguém da indústria leu seria pior que
            // não entregar a regra.
            var banco = Guid.NewGuid().ToString();

            using (var escrita = Contexto(banco, TenantA))
            {
                await PoliticaDesatualizadaAsync(escrita);
            }

            using var leitura = Contexto(banco, tenantId: null);
            var politica = await new PoliticaAnaliseRepository(leitura).ObterDoTenantAsync(TenantA);

            politica.Regras.Single(r => r.Codigo == "IND-001")
                .Severidade.Should().Be(Severidade.Informativo);
        }

        [Fact]
        public async Task Sincronizar_DevePersistirARegraParaAIndustriaPoderAjustar()
        {
            var banco = Guid.NewGuid().ToString();

            using (var escrita = Contexto(banco, TenantA))
            {
                await PoliticaDesatualizadaAsync(escrita);
            }

            using (var sincroniza = Contexto(banco, TenantA))
            {
                var novas = await new PoliticaAnaliseRepository(sincroniza)
                    .SincronizarComProtocoloAsync(TenantA);

                novas.Should().Be(1);
            }

            using var conferencia = Contexto(banco, TenantA);

            (await conferencia.Regras.AnyAsync(r => r.Codigo == "IND-001")).Should().BeTrue();
        }

        [Fact]
        public async Task Sincronizar_DuasVezes_NaoDeveDuplicar()
        {
            var banco = Guid.NewGuid().ToString();

            using (var escrita = Contexto(banco, TenantA))
            {
                await PoliticaDesatualizadaAsync(escrita);
            }

            using (var primeira = Contexto(banco, TenantA))
            {
                await new PoliticaAnaliseRepository(primeira).SincronizarComProtocoloAsync(TenantA);
            }

            using (var segunda = Contexto(banco, TenantA))
            {
                (await new PoliticaAnaliseRepository(segunda).SincronizarComProtocoloAsync(TenantA))
                    .Should().Be(0);
            }

            using var conferencia = Contexto(banco, TenantA);

            (await conferencia.Regras.CountAsync(r => r.Codigo == "IND-001")).Should().Be(1);
        }

        [Fact]
        public async Task RegraDesativada_NaoDeveVoltarComoNovidade()
        {
            // O caso que um teste existente pegou: comparar só com as regras
            // ativas fazia o protocolo ressuscitar o que a indústria tinha
            // desligado de propósito.
            var banco = Guid.NewGuid().ToString();

            using (var escrita = Contexto(banco, TenantA))
            {
                await PoliticaDesatualizadaAsync(escrita);

                var regra = await escrita.Regras.FirstAsync(r => r.Codigo == "EMB-001");
                regra.Ativa = false;
                await escrita.SaveChangesAsync();
            }

            using var leitura = Contexto(banco, tenantId: null);
            var politica = await new PoliticaAnaliseRepository(leitura).ObterDoTenantAsync(TenantA);

            politica.Regras.Should().NotContain(r => r.Codigo == "EMB-001");
        }
    }
}
