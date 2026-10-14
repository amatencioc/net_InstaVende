using Microsoft.AspNetCore.Antiforgery;

namespace InstaVende.Web.Infrastructure;

/// <summary>
/// Patrón "double submit" para el SPA: el token de petición se publica en la cookie legible
/// XSRF-TOKEN y el cliente lo devuelve en la cabecera X-XSRF-TOKEN en cada POST/PUT/PATCH/DELETE.
/// </summary>
public sealed class CsrfTokenIssuer(IAntiforgery antiforgery)
{
    public const string CookieName = "XSRF-TOKEN";
    public const string HeaderName = "X-XSRF-TOKEN";

    public void Issue(HttpContext context)
    {
        var tokens = antiforgery.GetAndStoreTokens(context);
        context.Response.Cookies.Append(CookieName, tokens.RequestToken!, new CookieOptions
        {
            HttpOnly = false,          // el SPA debe poder leerla
            Secure   = context.Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Path     = "/",
        });
    }
}
