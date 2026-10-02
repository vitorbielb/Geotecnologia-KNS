using GeotecnologiaKNS.Analises;
using GeotecnologiaKNS.Data;
using GeotecnologiaKNS.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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

        /// <summary>Imóvel do inquilino A, para abrir solicitação.</summary>
        public int ImovelDoTenantA { get; private set; }

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

            // ConfigureTestServices, e não ConfigureServices: só ele roda depois
            // das registrações do Program, que é onde o processador entra. Com o
            // hook errado a remoção não tem efeito e o serviço continua de pé.
            builder.ConfigureTestServices(services =>
            {
                // Nenhum trabalho de plano de fundo durante os testes. O
                // processador de análises tomaria a análise enfileirada em
                // milissegundos e o teste que verifica o enfileiramento viraria
                // uma corrida — o que o processador faz depois tem verificação
                // própria. Remove-se por contrato (IHostedService) e não por
                // tipo concreto, para não depender de como o registro foi feito
                // nem quebrar em silêncio se outro serviço entrar amanhã.
                foreach (var background in services
                             .Where(d => d.ServiceType == typeof(IHostedService))
                             .ToList())
                {
                    services.Remove(background);
                }
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

            var produtor = new Produtor
            {
                TenantId = TenantA,
                Nome = "Produtor de Teste",
                Cpf = "11144477735"
            };

            contexto.Produtores.Add(produtor);
            await contexto.SaveChangesAsync();

            var imovel = new Propriedade
            {
                TenantId = TenantA,
                ProdutorId = produtor.Id,
                CodigoCar = "MT-5107925-AAAA1111BBBB2222CCCC3333DDDD4444",
                NomePropriedade = "Fazenda de Teste",
                Municipio = "Sorriso",
                Bioma = "Cerrado",
                Area = "1000"
            };

            contexto.Propriedades.Add(imovel);

            await contexto.SaveChangesAsync();

            DocumentoDoTenantB = documentoDeB.Id;
            ImovelDoTenantA = imovel.Id;
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
