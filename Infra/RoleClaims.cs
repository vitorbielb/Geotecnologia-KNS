using System.Collections;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Security.Claims;

namespace GeotecnologiaKNS.Infra;

[DebuggerDisplay("{Name} {Claims}")]
public class RoleClaims : IEnumerable<Claim>
{
    private IdentityRole? _role;

    public RoleClaims(string name, Expression<Func<Features, object>> access)
    {
        Name = name;
        Claims = Build(access);
    }

    public string Name { get; set; } = default!;

    public List<Claim> Claims { get; set; } = new List<Claim>();

    public IEnumerator<Claim> GetEnumerator() => Claims.AsEnumerable().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public IdentityRole IdentityRole => _role ??= new()
    {
        Id = Name,
        Name = Name,
        NormalizedName = Name
    };

    private List<Claim> Build(Expression<Func<Features, object>> featuresAccess)
    {
        var visitor = new PermissionExpressionVisitor();
        visitor.Visit(featuresAccess);
        return visitor.Claims;
    }
}
