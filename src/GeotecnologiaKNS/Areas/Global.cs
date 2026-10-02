using Microsoft.AspNetCore.Mvc.Rendering;
using System.Reflection;
using System.Security.Claims;

namespace GeotecnologiaKNS.Utils;


public static class Global
{
    public const string Enabled = "enabled";
    public const string Disabled = "disabled";

    /// <summary>
    /// Claim que marca uma sessão cuja senha ainda é a provisória definida por
    /// quem cadastrou o usuário.
    /// </summary>
    public const string SenhaProvisoria = "senha_provisoria";
}
