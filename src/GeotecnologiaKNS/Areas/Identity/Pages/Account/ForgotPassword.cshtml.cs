// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace GeotecnologiaKNS.Areas.Identity.Pages.Account
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;

        public ForgotPasswordModel(UserManager<ApplicationUser> userManager, IEmailSender emailSender)
        {
            _userManager = userManager;
            _emailSender = emailSender;
        }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        [BindProperty]
        public InputModel Input { get; set; }

        /// <summary>
        ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public class InputModel
        {
            /// <summary>
            ///     This API supports the ASP.NET Core Identity default UI infrastructure and is not intended to be used
            ///     directly from your code. This API may change or be removed in future releases.
            /// </summary>
            [Required]
            [EmailAddress]
            public string Email { get; set; }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByEmailAsync(Input.Email);
                if (user == null || !(await _userManager.IsEmailConfirmedAsync(user)))
                {
                    // Don't reveal that the user does not exist or is not confirmed
                    return RedirectToPage("./ForgotPasswordConfirmation");
                }

                // For more information on how to enable account confirmation and password reset please
                // visit https://go.microsoft.com/fwlink/?LinkID=532713
                var code = await _userManager.GeneratePasswordResetTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                var callbackUrl = Url.Page(
                    "/Account/ResetPassword",
                    pageHandler: null,
                    values: new { area = "Identity", code },
                    protocol: Request.Scheme);

                await _emailSender.SendEmailAsync(
                    Input.Email,
                    "Redefinição de senha — KNS Ambiental",
                    MensagemDeRedefinicao(HtmlEncoder.Default.Encode(callbackUrl)));

                return RedirectToPage("./ForgotPasswordConfirmation");
            }

            return Page();
        }

        /// <summary>
        /// Corpo da mensagem de redefinição.
        /// </summary>
        /// <remarks>
        /// HTML enxuto e em tabela porque cliente de e-mail corporativo ignora
        /// boa parte de CSS moderno — o que se ganha em capricho se perde em
        /// mensagem que chega quebrada. O link aparece também como texto: há
        /// clientes que não tornam o botão clicável.
        /// </remarks>
        private static string MensagemDeRedefinicao(string link) =>
            $@"<table role=""presentation"" width=""100%"" cellpadding=""0"" cellspacing=""0""
                      style=""font-family: Arial, Helvetica, sans-serif; color: #1F3242;"">
                 <tr><td style=""padding: 24px 0;"">
                   <p style=""font-size: 16px; margin: 0 0 16px;""><strong>Redefinição de senha</strong></p>
                   <p style=""font-size: 14px; line-height: 1.6; margin: 0 0 20px;"">
                     Recebemos um pedido para redefinir a senha da sua conta no painel de
                     análises da KNS Ambiental. Clique no botão abaixo para escolher uma nova senha.
                   </p>
                   <p style=""margin: 0 0 24px;"">
                     <a href=""{link}""
                        style=""background: #127CC3; color: #FFFFFF; text-decoration: none;
                               padding: 12px 22px; border-radius: 6px; display: inline-block;
                               font-size: 14px;"">Redefinir minha senha</a>
                   </p>
                   <p style=""font-size: 13px; line-height: 1.6; color: #5B7183; margin: 0 0 8px;"">
                     Se o botão não funcionar, copie e cole este endereço no navegador:
                   </p>
                   <p style=""font-size: 12px; word-break: break-all; color: #5B7183; margin: 0 0 24px;"">{link}</p>
                   <p style=""font-size: 13px; line-height: 1.6; color: #5B7183; margin: 0;"">
                     Se não foi você quem pediu, ignore esta mensagem — sua senha atual
                     continua valendo.
                   </p>
                 </td></tr>
               </table>";
    }
}
