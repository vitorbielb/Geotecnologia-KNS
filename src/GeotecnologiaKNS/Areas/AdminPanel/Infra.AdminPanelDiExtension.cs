
using Microsoft.AspNetCore.Mvc.Razor;

namespace GeotecnologiaKNS.Infra;

/// <summary>
/// Extension methods for configuring AdminPanel.
/// </summary>
public static class AdminPanelDiExtension
{
    /// <summary>
    /// Adds configuration for AdminPanel views.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The updated service collection.</returns>
    public static IServiceCollection AddAdminPanel(this IServiceCollection services)
    {
        services.Configure<RazorViewEngineOptions>(options =>
        {
            options.ViewLocationFormats.Add("/Areas/AdminPanel/Views/{1}/{0}.cshtml");
        });

        return services;
    }
}