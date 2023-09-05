using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GeotecnologiaKNS.Utils;

/// <summary>
/// An <see cref="ActionFilterAttribute"/> that sets the TenantId property on the model passed to the action method. 
/// It gets the tenantId from <see cref="ITenantProvider"/> service and checks if it's less or equal than zero,
/// it returns a 403 Forbidden status code. 
/// Otherwise, it gets the first argument passed to the action method, 
/// to set the value of the TenantId property to the tenantId obtained from <see cref="ITenantProvider"/> service
/// and update the action arguments with the updated model object.
/// </summary>
/// <remarks>
/// This filter should not be used on actions with multiple parameter arguments.
/// </remarks>
public class TenantFilterAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        int tenantId = context.HttpContext
                              .RequestServices
                              .GetRequiredService<ITenantProvider>()
                              .TenantId;

        if (tenantId <= 0)
        {
            context.Result = new ForbidResult();
            return;
        }

        var argumentName = context.ActionArguments.Keys.FirstOrDefault();

        if (argumentName == null)
        {
            return;
        }

        var model = context.ActionArguments[argumentName];

        var type = model?.GetType();
        var TenantIdProperty = type?.GetProperty(nameof(ITenantInfo.TenantId));
        TenantIdProperty?.SetValue(model, tenantId);

        context.ActionArguments[argumentName] = model;
    }
}
