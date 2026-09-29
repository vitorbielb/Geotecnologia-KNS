using GeotecnologiaKNS.Data;
using GeotecnologiaKNS.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GeotecnologiaKNS.FuncionalTests
{
    /// <summary>
    /// Sobe a aplicação inteira contra um banco descartável e a semeia com dois
    /// inquilinos.
    /// </summary>
    /// <remarks>
    /// Cada instância cria o próprio banco, com nome sorteado, e o apaga no fim.
    /// A versão anterior desta fábrica chamava EnsureDeleted sobre a connection
    /// string do appsettings — ou seja, apagaria o banco de desenvolvimento de
    /// quem rodasse os testes. É provavelmente por isso que nenhum teste
    /// funcional chegou a ser escrito.
    ///
    /// Sem PostGIS: as telas cobertas aqui não cruzam camada nenhuma, e exigir
    /// um segundo servidor tornaria a suíte impossível de rodar em CI.
    /// </remarks>
    public class AplicacaoDeTeste : WebApplicationFactory<Program>, IAsyncLifetime
    {
        private readonly string _banco = "KNS_Testes_" + Guid.NewGuid().ToString("N")[..12];

        public const string SenhaPadrao = "Senha@Teste1";

        /// <summary>Atribuidos pelo banco: TenantId e coluna identity.</summary>
        public int TenantA { get; private set; }
        public int TenantB { get; private set; }

        public const string EmailA = "admin.a@teste.local";
        public const string EmailB = "admin.b@teste.local";
        public const string EmailProvisorio = "novato.a@teste.local";

        /// <summary>Documento do inquilino B, para provar que A não o alcança.</summary>
        public int DocumentoDoTenantB { get; private set; }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.UseEnvironment("Development");

            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:GeotecnologiaKNS"] =
                        $@"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog={_banco};Integrated Security=True;",

                    // Vazia de propósito: o projeto Geo degrada para "base não
                    // configurada" em vez de falhar ao subir.
                    ["ConnectionStrings:Geo"] = string.Empty
                });
            });
        }

        public async Task InitializeAsync()
        {
            using var escopo = Services.CreateScope();
            var contexto = escopo.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var hasher = escopo.ServiceProvider.GetRequiredService<IPasswordHasher<ApplicationUser>>();

            var industriaA = new Industria
            {
                Nome = "Frigorifico A Ltda",
                NomeResumido = "Frig A",
                RazaoSocial = "Frigorifico A Industria Ltda",
                Cnpj = "11222333000181"
            };

            var industriaB = new Industria
            {
                Nome = "Frigorifico B Ltda",
                NomeResumido = "Frig B",
                RazaoSocial = "Frigorifico B Industria Ltda",
                Cnpj = "11444777000161"
            };

            contexto.Industrias.AddRange(industriaA, industriaB);
            await contexto.SaveChangesAsync();

            TenantA = industriaA.TenantId;
            TenantB = industriaB.TenantId;

            contexto.Users.AddRange(
                Usuario(hasher, EmailA, TenantA, provisoria: false),
                Usuario(hasher, EmailB, TenantB, provisoria: false),
                Usuario(hasher, EmailProvisorio, TenantA, provisoria: true));

            var documentoDeB = new PropriedadeArquivo
            {
                TenantId = TenantB,
                VinculoId = 1,
                Descricao = "contrato-do-B.pdf",
                ContentType = "application/pdf",
                Dados = new byte[] { 9, 9, 9 }
            };

            contexto.PropriedadesArquivos.Add(documentoDeB);

            await contexto.SaveChangesAsync();

            DocumentoDoTenantB = documentoDeB.Id;
        }

        private static ApplicationUser Usuario(
            IPasswordHasher<ApplicationUser> hasher, string email, int tenantId, bool provisoria)
        {
            var usuario = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = email,
                NormalizedUserName = email.ToUpperInvariant(),
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                EmailConfirmed = true,
                TenantId = tenantId,
                SecurityStamp = Guid.NewGuid().ToString(),
                SenhaProvisoria = provisoria
            };

            usuario.PasswordHash = hasher.HashPassword(usuario, SenhaPadrao);
            return usuario;
        }

        async Task IAsyncLifetime.DisposeAsync()
        {
            using var escopo = Services.CreateScope();
            var contexto = escopo.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await contexto.Database.EnsureDeletedAsync();
        }
    }
}
