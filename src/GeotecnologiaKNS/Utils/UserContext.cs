using System.Security.Claims;
using System.Security.Principal;

namespace GeotecnologiaKNS.Utils
{
    public interface IUserContext
    {
        int? TenantId { get; }
        bool IsApplicationAdmin { get; }
        bool IsTenantAdmin { get; }
    }

    public sealed class UserContext : IUserContext
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserContext(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;
        private IIdentity? Identity => User?.Identity;

        public int? TenantId => Identity?.GetTenantId();

        public bool IsApplicationAdmin => Identity?.IsApplicationAdmin() ?? false;

        public bool IsTenantAdmin => Identity?.IsTenantAdmin() ?? false;
    }
}