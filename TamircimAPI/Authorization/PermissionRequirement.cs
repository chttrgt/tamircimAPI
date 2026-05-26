using Microsoft.AspNetCore.Authorization;

namespace TamircimAPI.Authorization
{
    public class PermissionRequirement : IAuthorizationRequirement
    {
        public string Permission { get; }
        public IReadOnlyList<string> Permissions { get; }

        public PermissionRequirement(string permission)
        {
            Permission = permission;
            Permissions = new[] { permission };
        }
    }
}
