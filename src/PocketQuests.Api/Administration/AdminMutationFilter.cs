using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;

namespace PocketQuests.Api.Administration;

/// <summary>Cookie-authenticated mutations require a same-origin anti-forgery token.</summary>
public sealed class AdminMutationFilter : IEndpointFilter
{
    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        if ((await http.AuthenticateAsync(AdminWeb.Cookie)).Succeeded)
        {
            try
            {
                await http.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(http);
            }
            catch (AntiforgeryValidationException)
            {
                return Results.Problem("Reload the administrative page before making a change.", statusCode: 400);
            }
        }

        return await next(context);
    }
}
