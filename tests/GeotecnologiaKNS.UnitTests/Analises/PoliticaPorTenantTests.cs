using FluentAssertions;
using GeotecnologiaKNS.Analises;
using GeotecnologiaKNS.Data;
using GeotecnologiaKNS.Geo.Entities;
using GeotecnologiaKNS.Geo.Services;
using GeotecnologiaKNS.Models;
using GeotecnologiaKNS.Utils;
using Microsoft.EntityFrameworkCore;

namespace GeotecnologiaKNS.UnitTests.Analises
{
    /// <summary>
    /// Política de conformidade por indústria.
    /// </summary>
    /// <remarks>
    /// As regras eram constantes compiladas, iguais para todos. Vendendo a
    /// várias indústrias isso não se sustenta: o que uma trata como bloqueio a
    /// outra trata como alerta.
    /// </remarks>
    public class PoliticaPorTenantTests
    {
        private const int TenantA = 1;
        private const int TenantB = 2;

        private sealed class ContextoFalso : IUserContext
        {
            public int? TenantId { get; set; }
            public bool IsApplicationAdmin => false;
            public bool IsTenantAdmin => true;
        }

        private static ApplicationDbContext Contexto(string banco, int? tenantId) =>
            new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(banco).Options,
                new ContextoFalso { TenantId = tenantId });

        [Fact]
        public async Task SemPoliticaPropria_DeveUsarOProtocoloPadrao()
        {
            var banco = Guid.NewGuid().ToString();
            using var contexto = Contexto(banco, TenantA);

            var politica = await new PoliticaAnaliseRepository(contexto).ObterDoTenantAsync(TenantA);

            politica.Nome.Should().Be(PoliticaAnalise.Padrao().Nome);
            politica.Regras.Should().NotBeEmpty();
        }

        [Fact]
        public async Task AdotarOPadrao_DeveCopiarAsRegrasEmVezDeReferenciar()
        {
            // Copiar é deliberado: a partir daí a indústria ajusta as próprias
            // regras sem que mudanças no padrão a afetem de surpresa.
            var banco = Guid.NewGuid().ToString();
            using var contexto = Contexto(banco, TenantA);
            var repositorio = new PoliticaAnaliseRepository(contexto);

            var criada = await repositorio.CriarAPartirDoPadraoAsync(TenantA);

            criada.TenantId.Should().Be(TenantA);
            criada.Regras.Should().HaveCount(PoliticaAnalise.Padrao().Regras.Count);
            criada.Regras.Should().OnlyContain(r => r.Ativa);
        }

        [Fact]
        public async Task AdotarDuasVezes_NaoDeveDuplicarAPolitica()
        {
            var banco = Guid.NewGuid().ToString();
            using var contexto = Contexto(banco, TenantA);
            var repositorio = new PoliticaAnaliseRepository(contexto);

            await repositorio.CriarAPartirDoPadraoAsync(TenantA);
            await repositorio.CriarAPartirDoPadraoAsync(TenantA);

            (await contexto.Politicas.CountAsync()).Should().Be(1);
        }

        [Fact]
        public async Task IndustriasDiferentes_PodemTerCortesDiferentes()
        {
            var banco = Guid.NewGuid().ToString();

            using (var contexto = Contexto(banco, TenantA))
            {
                await new PoliticaAnaliseRepository(contexto).CriarAPartirDoPadraoAsync(TenantA);

                // A indústria A afrouxa o desmatamento consolidado para alerta.
                var regra = await contexto.Regras.FirstAsync(r => r.Codigo == "DES-001");
                regra.Severidade = (int)Severidade.Alerta;
                await contexto.SaveChangesAsync();
            }

            using (var contexto = Contexto(banco, TenantB))
            {
                await new PoliticaAnaliseRepository(contexto).CriarAPartirDoPadraoAsync(TenantB);
            }

            using var leitura = Contexto(banco, tenantId: null);
            var repositorio = new PoliticaAnaliseRepository(leitura);

            var daA = await repositorio.ObterDoTenantAsync(TenantA);
            var daB = await repositorio.ObterDoTenantAsync(TenantB);

            daA.Regras.First(r => r.Codigo == "DES-001").Severidade.Should().Be(Severidade.Alerta);
            daB.Regras.First(r => r.Codigo == "DES-001").Severidade.Should().Be(Severidade.Bloqueio);
        }

        [Fact]
        public async Task MesmaSobreposicao_DeveDarVereditosDiferentesConformeAPolitica()
        {
            var banco = Guid.NewGuid().ToString();

            using (var contexto = Contexto(banco, TenantA))
            {
                await new PoliticaAnaliseRepository(contexto).CriarAPartirDoPadraoAsync(TenantA);
                var regra = await contexto.Regras.FirstAsync(r => r.Codigo == "DES-001");
                regra.Severidade = (int)Severidade.Alerta;
                await contexto.SaveChangesAsync();
            }

            using (var contexto = Contexto(banco, TenantB))
            {
                await new PoliticaAnaliseRepository(contexto).CriarAPartirDoPadraoAsync(TenantB);
            }

            using var leitura = Contexto(banco, tenantId: null);
            var repositorio = new PoliticaAnaliseRepository(leitura);
            var motor = new MotorDeRegras();

            var sobreposicao = new Sobreposicao(
                "prodes", "PRODES 2024", TipoCamada.DesmatamentoConsolidado, "INPE",
                2024, "polígono", null, 100, 10);

            // Cobertura completa de propósito: o que este teste mede é a política
            // por inquilino, e uma lacuna de camada mudaria o veredito por outro
            // motivo, mascarando o que se quer provar.
            var todosOsTipos = PoliticaAnalise.Padrao().Regras.Select(r => r.Tipo).Distinct().ToList();

            var cruzamento = new ResultadoCruzamento(
                "MT-1-X", 1000, new[] { sobreposicao }, DateTime.UtcNow, todosOsTipos);

            var vereditoA = motor.Avaliar(cruzamento, await repositorio.ObterDoTenantAsync(TenantA));
            var vereditoB = motor.Avaliar(cruzamento, await repositorio.ObterDoTenantAsync(TenantB));

            vereditoA.Status.Should().Be(Status.Alerta);
            vereditoB.Status.Should().Be(Status.Bloqueado);
        }

        [Fact]
        public async Task RegraInativa_NaoDeveSerAvaliada()
        {
            var banco = Guid.NewGuid().ToString();

            using (var contexto = Contexto(banco, TenantA))
            {
                await new PoliticaAnaliseRepository(contexto).CriarAPartirDoPadraoAsync(TenantA);
                var regra = await contexto.Regras.FirstAsync(r => r.Codigo == "EMB-001");
                regra.Ativa = false;
                await contexto.SaveChangesAsync();
            }

            using var leitura = Contexto(banco, tenantId: null);
            var politica = await new PoliticaAnaliseRepository(leitura).ObterDoTenantAsync(TenantA);

            politica.Regras.Should().NotContain(r => r.Codigo == "EMB-001");
        }

        [Fact]
        public void RetratoDaPolitica_DeveRegistrarOsCortesVigentes()
        {
            // Sem o retrato, afrouxar uma regra tornaria indefensável um
            // bloqueio emitido antes da mudança.
            var retrato = RetratoDaPolitica.Serializar(PoliticaAnalise.Padrao());

            retrato.Should().Contain("DES-001");
            retrato.Should().Contain("Bloqueio");
            retrato.Should().Contain("2008", "o corte temporal precisa constar do retrato");
        }
    }
}
