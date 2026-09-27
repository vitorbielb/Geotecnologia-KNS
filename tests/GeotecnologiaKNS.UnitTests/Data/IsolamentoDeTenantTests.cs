using FluentAssertions;
using GeotecnologiaKNS.Data;
using GeotecnologiaKNS.Models;
using GeotecnologiaKNS.Utils;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GeotecnologiaKNS.UnitTests.Data
{
    /// <summary>
    /// Garante que uma indústria não alcança dados de outra.
    /// </summary>
    /// <remarks>
    /// Os documentos eram o furo: os endpoints de download buscam pelo Id
    /// inteiro, e sem filtro global bastava iterar o Id para ler ou apagar
    /// arquivo de outro tenant. Num produto vendido a concorrentes diretos,
    /// isso precisa de teste, não de revisão de código.
    /// </remarks>
    public class IsolamentoDeTenantTests
    {
        private const int TenantA = 1;
        private const int TenantB = 2;

        private sealed class ContextoFalso : IUserContext
        {
            public int? TenantId { get; set; }
            public bool IsApplicationAdmin { get; set; }
            public bool IsTenantAdmin { get; set; }
        }

        /// <summary>
        /// Um contexto por chamada, todos sobre o mesmo banco em memória, para
        /// que a troca de tenant seja observada como uma sessão diferente.
        /// </summary>
        private static ApplicationDbContext Contexto(string banco, int? tenantId)
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(banco)
                .Options;

            return new ApplicationDbContext(options, new ContextoFalso { TenantId = tenantId });
        }

        private static string SemearDoisTenants()
        {
            var banco = Guid.NewGuid().ToString();

            // Semeia sem tenant no contexto: os filtros ficam inativos e ambos
            // os conjuntos de dados podem ser inseridos de uma vez.
            using var contexto = Contexto(banco, tenantId: null);

            contexto.ProdutoresArquivos.AddRange(
                new ProdutorArquivo { Id = 1, TenantId = TenantA, VinculoId = 1, Descricao = "cpf-do-A.pdf", Dados = new byte[] { 1 }, ContentType = "application/pdf" },
                new ProdutorArquivo { Id = 2, TenantId = TenantB, VinculoId = 2, Descricao = "cpf-do-B.pdf", Dados = new byte[] { 2 }, ContentType = "application/pdf" });

            contexto.PropriedadesArquivos.AddRange(
                new PropriedadeArquivo { Id = 1, TenantId = TenantA, VinculoId = 1, Descricao = "car-do-A.pdf", Dados = new byte[] { 1 }, ContentType = "application/pdf" },
                new PropriedadeArquivo { Id = 2, TenantId = TenantB, VinculoId = 2, Descricao = "car-do-B.pdf", Dados = new byte[] { 2 }, ContentType = "application/pdf" });

            contexto.AnalisesArquivos.AddRange(
                new AnaliseArquivo { Id = 1, TenantId = TenantA, VinculoId = 1, Descricao = "laudo-do-A.pdf", Dados = new byte[] { 1 }, ContentType = "application/pdf" },
                new AnaliseArquivo { Id = 2, TenantId = TenantB, VinculoId = 2, Descricao = "laudo-do-B.pdf", Dados = new byte[] { 2 }, ContentType = "application/pdf" });

            contexto.CartografiasArquivos.AddRange(
                new CartografiaArquivo { Id = 1, TenantId = TenantA, VinculoId = 1, Descricao = "mapa-do-A.pdf", Dados = new byte[] { 1 }, ContentType = "application/pdf", Tipo = "SUC" },
                new CartografiaArquivo { Id = 2, TenantId = TenantB, VinculoId = 2, Descricao = "mapa-do-B.pdf", Dados = new byte[] { 2 }, ContentType = "application/pdf", Tipo = "SUC" });

            contexto.SaveChanges();
            return banco;
        }

        [Fact]
        public async Task DocumentoDeProdutor_DeOutroTenant_NaoDeveSerAlcancavelPeloId()
        {
            var banco = SemearDoisTenants();
            using var contexto = Contexto(banco, TenantA);

            // Id 2 é do tenant B. É exatamente o que um usuário faria na URL.
            var alheio = await contexto.ProdutoresArquivos.FirstOrDefaultAsync(x => x.Id == 2);
            var proprio = await contexto.ProdutoresArquivos.FirstOrDefaultAsync(x => x.Id == 1);

            alheio.Should().BeNull("documento de outra indústria não pode ser alcançado pelo Id");
            proprio.Should().NotBeNull();
        }

        [Fact]
        public async Task DocumentoDeImovel_DeOutroTenant_NaoDeveSerAlcancavelPeloId()
        {
            var banco = SemearDoisTenants();
            using var contexto = Contexto(banco, TenantA);

            (await contexto.PropriedadesArquivos.FirstOrDefaultAsync(x => x.Id == 2)).Should().BeNull();
        }

        [Fact]
        public async Task LaudoDeAnalise_DeOutroTenant_NaoDeveSerAlcancavelPeloId()
        {
            var banco = SemearDoisTenants();
            using var contexto = Contexto(banco, TenantA);

            (await contexto.AnalisesArquivos.FirstOrDefaultAsync(x => x.Id == 2)).Should().BeNull();
        }

        [Fact]
        public async Task Cartografia_DeOutroTenant_NaoDeveSerAlcancavelPeloId()
        {
            var banco = SemearDoisTenants();
            using var contexto = Contexto(banco, TenantA);

            (await contexto.CartografiasArquivos.FirstOrDefaultAsync(x => x.Id == 2)).Should().BeNull();
        }

        [Fact]
        public async Task Listagem_DeveConterApenasDocumentosDoProprioTenant()
        {
            var banco = SemearDoisTenants();
            using var contexto = Contexto(banco, TenantB);

            var documentos = await contexto.ProdutoresArquivos.ToListAsync();

            documentos.Should().ContainSingle();
            documentos[0].Descricao.Should().Be("cpf-do-B.pdf");
        }

        [Fact]
        public async Task SemTenantNaSessao_NaoDeveFiltrar()
        {
            // Rotinas de manutenção e o seeder rodam sem HttpContext; nesse caso
            // o filtro é inativo de propósito.
            var banco = SemearDoisTenants();
            using var contexto = Contexto(banco, tenantId: null);

            (await contexto.ProdutoresArquivos.CountAsync()).Should().Be(2);
        }
    }
}
