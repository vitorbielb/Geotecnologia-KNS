using System.Linq.Expressions;
using System.Security.Claims;

namespace GeotecnologiaKNS.Infra;

/// <summary>
/// Visitor class for processing permission expressions.
/// </summary>
public class PermissionExpressionVisitor : ExpressionVisitor
{
    private readonly List<string> _featuresNames = new List<string>();

    public List<Claim> Claims { get; private set; } = new List<Claim>();

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Method.Name == nameof(Features.Everything))
        {
            SetAllPermissionsClaimsEnabled();
        }
        else if (node.Method.Name == nameof(Features.EverythingExcept))
        {
            Visit(node.Arguments[0]);
            SetEveryPermissionClaimsExcept(_featuresNames);
        }
        else if (node.Method.Name == nameof(Features.OnlyAccess))
        {
            Visit(node.Arguments[0]);
            SetAccessOnlyForPermissionClaimsIn(_featuresNames);
        }

        return node;
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        string originalString = node.ToString();
        int firstDotIndex = originalString.IndexOf('.');
        string result = originalString[(firstDotIndex + 1)..];

        if (!_featuresNames.Any(val => val.StartsWith(result)))
        {
            _featuresNames.Add(result);
        }

        return base.VisitMember(node);
    }

    private void SetAllPermissionsClaimsEnabled()
    {
        Claims.AddRange(Features.GetAll(defaultValue: Enabled));
    }

    private void SetEveryPermissionClaimsExcept(List<string> featuresNames)
    {
        Claims.AddRange(Features.GetAll(defaultValue: Disabled).Where(c => featuresNames.Any(f => c.Type.StartsWith(f))));
        Claims.AddRange(Features.GetAll(defaultValue: Enabled).Where(c => !featuresNames.Any(f => c.Type.StartsWith(f))));
    }

    private void SetAccessOnlyForPermissionClaimsIn(List<string> featuresNames)
    {
        Claims.AddRange(Features.GetAll(defaultValue: Disabled).Where(c => !featuresNames.Any(f => c.Type.StartsWith(f))));
        Claims.AddRange(Features.GetAll(defaultValue: Enabled).Where(c => featuresNames.Any(f => c.Type.StartsWith(f))));
    }
}
