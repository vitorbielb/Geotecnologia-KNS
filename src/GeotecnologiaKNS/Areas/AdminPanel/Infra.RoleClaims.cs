using System.Collections;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace GeotecnologiaKNS.Infra;

/// <summary>
/// Custom role claims class for defining claims associated with a role.
/// </summary>
[DebuggerDisplay("{RoleName} {Claims}")]
public class RoleClaims : IEnumerable<Claim>
{
    private const string Pattern = @"(?<=get_)[^()\n]*";
    private const int StartIndexToSearchPropertyName = 40;
    private ApplicationRole? _role;

    public RoleClaims(Expression<Func<Features, object>> access)
    {
        var match = Regex.Match(Environment.StackTrace[StartIndexToSearchPropertyName..], Pattern);
        RoleName = match.Value;
        Claims = Build(access);
    }

    public string RoleName { get; set; } = default!;

    public List<Claim> Claims { get; set; } = new List<Claim>();

    public IEnumerator<Claim> GetEnumerator() => Claims.AsEnumerable().GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public ApplicationRole Role => _role ??= new()
    {
        Id = RoleName,
        Name = RoleName,
        NormalizedName = RoleName
    };

    private static List<Claim> Build(Expression<Func<Features, object>> featuresAccess)
    {
        var visitor = new PermissionExpressionVisitor();
        visitor.Visit(featuresAccess);
        return visitor.Claims;
    }
}
