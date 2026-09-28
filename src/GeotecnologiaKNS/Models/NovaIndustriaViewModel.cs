using System.ComponentModel.DataAnnotations;
using GeotecnologiaKNS.Validators;

namespace GeotecnologiaKNS.Models
{
    /// <summary>
    /// Cadastro de uma indústria junto com o usuário que vai administrá-la.
    /// </summary>
    /// <remarks>
    /// Os dois eram cadastrados em telas separadas, e nada impedia criar a
    /// indústria e esquecer o usuário — ela ficava órfã, sem ninguém que
    /// conseguisse entrar, e o problema só aparecia quando o cliente tentava
    /// acessar. Aqui os dois nascem na mesma transação: ou existem ambos, ou
    /// nenhum.
    /// </remarks>
    public class NovaIndustriaViewModel
    {
        private const string RequiredMessage = "Campo obrigatório";

        #region Indústria

        [Required(ErrorMessage = RequiredMessage)]
        [Display(Name = "Nome")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "O nome deve ter no mínimo 6 e no máximo 100 caracteres")]
        public string Nome { get; set; } = string.Empty;

        [Required(ErrorMessage = RequiredMessage)]
        [Display(Name = "Nome resumido")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "O nome deve ter no mínimo 3 e no máximo 100 caracteres")]
        public string NomeResumido { get; set; } = string.Empty;

        [Required(ErrorMessage = RequiredMessage)]
        [Display(Name = "Razão social")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "A razão social deve ter no mínimo 6 e no máximo 100 caracteres")]
        public string RazaoSocial { get; set; } = string.Empty;

        [Required(ErrorMessage = RequiredMessage)]
        [Display(Name = "CNPJ")]
        [CnpjValid(ErrorMessage = "CNPJ inválido")]
        public string Cnpj { get; set; } = string.Empty;

        [Display(Name = "Logotipo")]
        public IFormFile? Logotipo { get; set; }

        #endregion

        #region Administrador da indústria

        [Required(ErrorMessage = RequiredMessage)]
        [Display(Name = "Nome do administrador")]
        [StringLength(100)]
        public string AdminUserName { get; set; } = string.Empty;

        [Required(ErrorMessage = RequiredMessage)]
        [EmailAddress(ErrorMessage = "Email inválido")]
        [Display(Name = "E-mail de acesso")]
        public string AdminEmail { get; set; } = string.Empty;

        [Display(Name = "Celular")]
        public string? AdminPhoneNumber { get; set; }

        [Required(ErrorMessage = RequiredMessage)]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "A senha deve ter no mínimo 6 e no máximo 100 caracteres")]
        [DataType(DataType.Password)]
        [Display(Name = "Senha inicial")]
        public string AdminPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = RequiredMessage)]
        [DataType(DataType.Password)]
        [Display(Name = "Confirme a senha")]
        [Compare(nameof(AdminPassword), ErrorMessage = "A senha e a confirmação não coincidem")]
        public string AdminConfirmPassword { get; set; } = string.Empty;

        #endregion

        public Industria ParaIndustria() => new()
        {
            Nome = Nome.Trim(),
            NomeResumido = NomeResumido.Trim(),
            RazaoSocial = RazaoSocial.Trim(),
            Cnpj = Cnpj.Trim()
        };
    }
}
