using Microsoft.AspNetCore.Authorization;

namespace TamircimAPI.Authorization
{
    public class HasPermissionAttribute : AuthorizeAttribute
    {
        public HasPermissionAttribute(string permission) : base(policy: permission)
        {
        }
    }
}
