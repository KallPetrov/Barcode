using System.Security.Claims;

namespace CALAC.Api.Middleware;

public class PasswordChangeMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var mustChangeClaim = context.User.FindFirst("must_change_password")?.Value;
            if (mustChangeClaim == "true")
            {
                var path = context.Request.Path.Value?.ToLower();
                // Allow only auth-related endpoints and logout
                var allowedPaths = new[] { "/api/auth/change-password", "/api/auth/logout", "/api/auth/user" };

                if (allowedPaths.All(p => path != null && !path.StartsWith(p)))
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsJsonAsync(new { error = "Задължителна смяна на парола. Моля, сменете паролата си, за да продължите." });
                    return;
                }
            }
        }

        await next(context);
    }
}

public static class PasswordChangeMiddlewareExtensions
{
    public static IApplicationBuilder UsePasswordChangeEnforcement(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<PasswordChangeMiddleware>();
    }
}
