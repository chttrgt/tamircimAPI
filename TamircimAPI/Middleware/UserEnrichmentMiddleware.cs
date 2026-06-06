using System.Security.Claims;
using TamircimAPI.Services.Tenant;

namespace TamircimAPI.Middleware
{
    // Kimlik doğrulanmış isteklerde JWT claim'lerinden istek bağlamını zenginleştirir:
    // UserId/UserEmail (loglama) + tenant_id (tenant izolasyonu). tenant_id ITenantContext'e
    // yazılır; EF global query filter'ları ve RLS oturum değişkeni bundan beslenir.
    // UseAuthentication SONRASI çalışmalıdır.
    public class UserEnrichmentMiddleware
    {
        private readonly RequestDelegate _next;

        public UserEnrichmentMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        // ITenantContext scoped → InvokeAsync parametresi olarak çözülür.
        public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
                var userEmail = context.User.FindFirstValue(ClaimTypes.Email);

                if (!string.IsNullOrEmpty(userId))
                    context.Items["UserId"] = userId;

                if (!string.IsNullOrEmpty(userEmail))
                    context.Items["UserEmail"] = userEmail;

                // Tenant'ı yalnızca imzalı token'dan al. Geçersiz/eksikse set etme →
                // ITenantContext.TenantId null kalır → iş-verisi sorguları satır döndürmez.
                var tenantClaim = context.User.FindFirstValue("tenant_id");
                if (int.TryParse(tenantClaim, out var tenantId) && tenantId > 0)
                    tenantContext.SetTenant(tenantId);
            }

            await _next(context);
        }
    }
}
