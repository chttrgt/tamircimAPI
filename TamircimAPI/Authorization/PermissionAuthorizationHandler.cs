using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace TamircimAPI.Authorization
{
    public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public PermissionAuthorizationHandler(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
        {
            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);

            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out _))
                return Task.CompletedTask;

            // Şimdilik tüm kimlik doğrulanmış kullanıcılar tüm izinlere sahip
            // İleride rol bazlı yetki eklenebilir
            context.Succeed(requirement);

            return Task.CompletedTask;
        }
    }
}
