using System.Collections;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Security.Claims;

namespace GeotecnologiaKNS.Infra;

/// <summary>
/// Custom role claims class for defining claims associated with a role.
/// </summary>
[DebuggerDisplay("{RoleName} {Claims}")]
public class RoleClaims : IEnumerable<Claim>
{
    private ApplicationRole? _role;

    /// <param name="access">Expressão que descreve as permissões do papel.</param>
    /// <param name="roleName">
    /// Nome do papel. Preenchido automaticamente pelo compilador com o nome da propriedade
    /// que constrói o <see cref="RoleClaims"/> (ver <see cref="Roles"/>).
    /// </param>
    public RoleClaims(
        Expression<Func<Features, object>> access,
        [CallerMemberName] string roleName = "")
    {
        if (string.IsNullOrWhiteSpace(roleName))
        {
            throw new ArgumentException("O nome do papel não pôde ser determinado.", nameof(roleName));
        }

        RoleName = roleName;
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
        NormalizedName = RoleName.ToUpperInvariant()
    };

    private static List<Claim> Build(Expression<Func<Features, object>> featuresAccess)
    {
        var visitor = new PermissionExpressionVisitor();
        visitor.Visit(featuresAccess);
        return visitor.Claims;
    }
}
