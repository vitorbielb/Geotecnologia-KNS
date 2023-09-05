namespace GeotecnologiaKNS.Utils
{
    public interface IUserContext
    {
        int? TenantId { get; }
        bool? IsApplicationAdmin { get; }
        bool? IsTenantAdmin { get; }
    }

    public class UserContext : IUserContext
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        public UserContext(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;
        public int? TenantId => _httpContextAccessor.HttpContext?.User?.Identity?.GetTenantId();
        public bool? IsApplicationAdmin => _httpContextAccessor.HttpContext?.User?.Identity?.IsApplicationAdmin();
        public bool? IsTenantAdmin => _httpContextAccessor.HttpContext?.User?.Identity?.IsTenantAdmin();
    }
}
