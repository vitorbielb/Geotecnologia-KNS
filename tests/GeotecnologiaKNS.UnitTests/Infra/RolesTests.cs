using FluentAssertions;
using GeotecnologiaKNS.Infra;

namespace GeotecnologiaKNS.UnitTests.Infra
{
    public class RolesTests
    {
        // Metodo_Cenario_ResultadoEsperado

        [Fact]
        public void RoleName_PapelDeclarado_DeveUsarONomeDaPropriedade()
        {
            // arrange & act
            var administrador = Roles.Administrador;
            var analista = Roles.Analista;

            // assert
            administrador.RoleName.Should().Be(nameof(Roles.Administrador));
            analista.RoleName.Should().Be(nameof(Roles.Analista));
        }

        [Fact]
        public void Role_PapelDeclarado_DeveNormalizarONomeEmMaiusculas()
        {
            // act
            var role = Roles.ClienteAdmin.Role;

            // assert
            role.Id.Should().Be(nameof(Roles.ClienteAdmin));
            role.Name.Should().Be(nameof(Roles.ClienteAdmin));
            role.NormalizedName.Should().Be(nameof(Roles.ClienteAdmin).ToUpperInvariant());
        }

        [Fact]
        public void GetRoleClaims_TodosOsPapeis_DevemTerNomeEClaims()
        {
            // act
            var roles = Roles.GetRoleClaims().ToList();

            // assert
            roles.Should().NotBeEmpty();
            roles.Should().OnlyContain(r => !string.IsNullOrWhiteSpace(r.RoleName));
            roles.Should().OnlyContain(r => r.Claims.Count > 0);
        }

        [Fact]
        public void Claims_Administrador_DeveHabilitarTodasAsOperacoes()
        {
            // act
            var claims = Roles.Administrador.Claims;

            // assert
            claims.Should().OnlyContain(c => c.Value == "enabled");
            claims.Should().Contain(c => c.Type == "Solicitacao.Update");
        }

        [Fact]
        public void Claims_Analista_DeveHabilitarSomenteOAcessoDeclarado()
        {
            // act
            var claims = Roles.Analista.Claims;

            // assert
            claims.Should().Contain(c => c.Type == "Solicitacao.Update" && c.Value == "enabled");
            claims.Should().Contain(c => c.Type == "Cartografia.Create" && c.Value == "enabled");
            claims.Should().Contain(c => c.Type == "Tenant.Create" && c.Value == "disabled");
        }
    }
}
