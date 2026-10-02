using FluentAssertions;
using GeotecnologiaKNS.Data;
using GeotecnologiaKNS.Models;
using GeotecnologiaKNS.Utils;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GeotecnologiaKNS.UnitTests.Data
{
    /// <summary>
    /// Escopo de funções entre inquilinos.
    /// </summary>
    /// <remarks>
    /// Dois defeitos motivaram estes testes: os papéis internos ficavam
    /// invisíveis para o administrador do cliente, que então não conseguia
    /// atribuir Solicitante nem Analista; e o nome da função era chave global,
    /// impedindo duas indústrias de terem funções homônimas.
    /// </remarks>
    public class EscopoDeFuncoesTests
    {
        private const int TenantA = 1;
        private const int TenantB = 2;

        private sealed class ContextoFalso : IUserContext
        {
            public int? TenantId { get; set; }
            public bool IsApplicationAdmin { get; set; }
            public bool IsTenantAdmin { get; set; }
        }

        private static ApplicationDbContext Contexto(string banco, int? tenantId, bool adminDaAplicacao = false)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(banco)
                .Options;

            return new ApplicationDbContext(options, new ContextoFalso
            {
                TenantId = tenantId,
                IsApplicationAdmin = adminDaAplicacao,
                IsTenantAdmin = !adminDaAplicacao && tenantId.HasValue
            });
        }

        private static ApplicationRole Role(string id, string nome, int tenantId, bool custom) => new()
        {
            Id = id,
            Name = nome,
            NormalizedName = nome.ToUpperInvariant(),
            TenantId = tenantId,
            Custom = custom
        };

        private static string Semear()
        {
            var banco = Guid.NewGuid().ToString();
            using var contexto = Contexto(banco, tenantId: null);

            // Internos, compartilhados por todas as indústrias.
            contexto.Roles.AddRange(
                Role("Administrador", "Administrador", RoleGlobal.TenantId, custom: false),
                Role("ClienteAdmin", "ClienteAdmin", RoleGlobal.TenantId, custom: false),
                Role("Solicitante", "Solicitante", RoleGlobal.TenantId, custom: false),
                Role("Analista", "Analista", RoleGlobal.TenantId, custom: false));

            // Funções homônimas em indústrias diferentes — o que antes não era possível.
            contexto.Roles.AddRange(
                Role(Guid.NewGuid().ToString(), "Gerente", TenantA, custom: true),
                Role(Guid.NewGuid().ToString(), "Gerente", TenantB, custom: true));

            contexto.SaveChanges();
            return banco;
        }

        [Fact]
        public async Task AdministradorDoCliente_DeveEnxergarOsPapeisInternos()
        {
            var banco = Semear();
            using var contexto = Contexto(banco, TenantA);

            var nomes = await contexto.Roles.Select(x => x.Name).ToListAsync();

            nomes.Should().Contain("Solicitante", "sem isso o cliente não consegue atribuir a função");
            nomes.Should().Contain("Analista");
            nomes.Should().Contain("ClienteAdmin");
        }

        [Fact]
        public async Task AdministradorDoCliente_NaoDeveEnxergarOPapelDeAdministracaoDaAplicacao()
        {
            var banco = Semear();
            using var contexto = Contexto(banco, TenantA);

            (await contexto.Roles.AnyAsync(x => x.Name == "Administrador")).Should().BeFalse();
        }

        [Fact]
        public async Task FuncaoCustomizada_DeOutroInquilino_NaoDeveSerVisivel()
        {
            var banco = Semear();
            using var contexto = Contexto(banco, TenantA);

            var gerentes = await contexto.Roles.Where(x => x.Name == "Gerente").ToListAsync();

            gerentes.Should().ContainSingle();
            gerentes[0].TenantId.Should().Be(TenantA);
        }

        [Fact]
        public async Task DoisInquilinos_PodemTerFuncoesComOMesmoNome()
        {
            // O índice único era global em NormalizedName; a segunda indústria
            // recebia erro de chave duplicada por causa de um registro invisível.
            var banco = Semear();
            using var contexto = Contexto(banco, tenantId: null);

            var gerentes = await contexto.Roles.Where(x => x.NormalizedName == "GERENTE").ToListAsync();

            gerentes.Should().HaveCount(2);
            gerentes.Select(x => x.TenantId).Should().BeEquivalentTo(new[] { TenantA, TenantB });
        }

        [Fact]
        public async Task FuncaoCustomizada_NaoDeveUsarONomeComoChave()
        {
            var banco = Semear();
            using var contexto = Contexto(banco, tenantId: null);

            var gerente = await contexto.Roles.FirstAsync(x => x.NormalizedName == "GERENTE" && x.TenantId == TenantA);

            gerente.Id.Should().NotBe("Gerente", "usar o nome como Id recria a colisão entre inquilinos");
        }

        [Fact]
        public async Task AdministradorDaAplicacao_DeveEnxergarTudo()
        {
            var banco = Semear();
            using var contexto = Contexto(banco, TenantA, adminDaAplicacao: true);

            (await contexto.Roles.CountAsync()).Should().Be(6);
        }
    }
}
