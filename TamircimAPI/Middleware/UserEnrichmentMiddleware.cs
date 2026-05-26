using System.Security.Claims;

namespace TamircimAPI.Middleware
{
    public class UserEnrichmentMiddleware
    {
        private readonly RequestDelegate _next;

        public UserEnrichmentMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
                var userEmail = context.User.FindFirstValue(ClaimTypes.Email);

                if (!string.IsNullOrEmpty(userId))
                    context.Items["UserId"] = userId;

                if (!string.IsNullOrEmpty(userEmail))
                    context.Items["UserEmail"] = userEmail;
            }

            await _next(context);
        }
    }
}
