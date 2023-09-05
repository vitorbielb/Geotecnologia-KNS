namespace GeotecnologiaKNS.Utils
{
    public interface ITenantProvider
    {
        int TenantId { get; }
    }

    public class TenantProvider : ITenantProvider
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public TenantProvider(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public int TenantId
        {
            get
            {
                if (_httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false)
                {
                    return _httpContextAccessor.HttpContext?.User?.Identity?.GetTenantId() ?? 0;
                }

                return 0;
            }
        }
    }
}
