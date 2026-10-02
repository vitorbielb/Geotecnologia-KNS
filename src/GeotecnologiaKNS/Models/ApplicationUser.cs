using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Claims;

namespace GeotecnologiaKNS.Models
{
    public class ApplicationUser : IdentityUser, IIndustriaInfo, IPrimaryKeyInfo<string>
    {
        [PersonalData]
        [ForeignKey(nameof(Industria))]
        public int TenantId { get; set; }

        public Industria Industria { get; set; }

        /// <summary>
        /// A senha atual foi definida por outra pessoa e precisa ser trocada no
        /// primeiro acesso.
        /// </summary>
        /// <remarks>
        /// Quem cadastra um usuário escolhe a senha inicial e a combina por fora
        /// — por mensagem, telefone, papel. Sem esta marca, essa senha vira a
        /// senha definitiva, conhecida por duas pessoas, e o log de acesso deixa
        /// de identificar quem de fato entrou.
        /// </remarks>
        public bool SenhaProvisoria { get; set; }

        public List<IdentityUserClaim<string>> Claims { get; set; }
    }
}
