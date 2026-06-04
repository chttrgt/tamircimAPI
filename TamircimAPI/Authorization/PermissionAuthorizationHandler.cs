using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using TamircimAPI.Models.Enums;

namespace TamircimAPI.Authorization
{
    public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
        {
            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out _))
                return Task.CompletedTask; // kimlik yok → izin verme

            // Sahip tüm izinlere örtük sahiptir.
            if (context.User.IsInRole(nameof(UserRole.Owner)))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            // Çalışan: gerekli izin token'daki "permission" claim'lerinde var mı?
            var hasPermission = context.User
                .FindAll("permission")
                .Any(c => c.Value == requirement.Permission);

            if (hasPermission)
                context.Succeed(requirement);

            return Task.CompletedTask;
        }
    }
}
