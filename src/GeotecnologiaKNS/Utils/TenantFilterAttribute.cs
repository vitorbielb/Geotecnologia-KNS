using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GeotecnologiaKNS.Utils;

/// <summary>
/// Filtro que define o TenantId no modelo enviado para a action.
/// O valor é obtido a partir de <see cref="IUserContext"/>.
/// Caso o TenantId seja inválido, retorna 403 Forbidden.
/// </summary>
/// <remarks>
/// Este filtro deve ser usado em actions com um único parâmetro de modelo
/// que implemente <see cref="ITenantInfo"/>.
/// </remarks>
public sealed class TenantFilterAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var userContext = context.HttpContext.RequestServices.GetRequiredService<IUserContext>();
        var tenantId = userContext.TenantId;

        if (!tenantId.HasValue || tenantId <= 0)
        {
            context.Result = new ForbidResult();
            return;
        }

        var actionArgument = context.ActionArguments.Values.FirstOrDefault();

        if (actionArgument is not ITenantInfo tenantModel)
            return;

        tenantModel.TenantId = tenantId.Value;
    }
}