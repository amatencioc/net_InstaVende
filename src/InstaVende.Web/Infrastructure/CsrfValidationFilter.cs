using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace InstaVende.Web.Infrastructure;

/// <summary>
/// Validación CSRF global para la API (POST/PUT/PATCH/DELETE): exige la cabecera X-XSRF-TOKEN
/// que el SPA copia de la cookie XSRF-TOKEN. Los endpoints con <see cref="IgnoreAntiforgeryTokenAttribute"/>
/// (webhooks firmados) quedan excluidos. Responde 400 ProblemDetails si el token falta o no es válido.
/// </summary>
public sealed class CsrfValidationFilter(IAntiforgery antiforgery, ProblemDetailsFactory problems, ILogger<CsrfValidationFilter> logger)
    : IAsyncAuthorizationFilter
{
    private static readonly HashSet<string> SafeMethods = new(StringComparer.OrdinalIgnoreCase) { "GET", "HEAD", "OPTIONS", "TRACE" };

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var http = context.HttpContext;
        if (SafeMethods.Contains(http.Request.Method)) return;

        // La política más cercana a la acción decide (permite [IgnoreAntiforgeryToken] por controlador o acción)
        var policy = context.Filters.OfType<IAntiforgeryPolicy>().LastOrDefault();
        if (policy is IgnoreAntiforgeryTokenAttribute) return;

        try
        {
            await antiforgery.ValidateRequestAsync(http);
        }
        catch (AntiforgeryValidationException ex)
        {
            logger.LogInformation("Token CSRF inválido en {Method} {Path}: {Reason}", http.Request.Method, http.Request.Path, ex.Message);
            var pd = problems.CreateProblemDetails(http, StatusCodes.Status400BadRequest,
                detail: "La sesión de seguridad expiró. Recarga la página e inténtalo de nuevo.");
            context.Result = new ObjectResult(pd) { StatusCode = StatusCodes.Status400BadRequest };
        }
    }
}
