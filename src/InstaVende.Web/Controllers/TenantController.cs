using InstaVende.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace InstaVende.Web.Controllers;

/// <summary>
/// Base de todos los endpoints del panel (<c>/api/...</c>).
/// <list type="bullet">
///   <item>Exige usuario autenticado con un negocio; si no lo tiene responde 403 con
///         <c>type = <see cref="ApiProblemTypes.NoBusiness"/></c> para que el SPA muestre la configuración inicial.</item>
///   <item>Expone <see cref="Tenant"/>/<see cref="BusinessId"/>: toda consulta debe filtrar por él.</item>
///   <item>Los errores se devuelven como ProblemDetails (RFC 9457) con <c>detail</c> legible para el usuario.</item>
/// </list>
/// </summary>
[ApiController]
[Authorize]
[Produces("application/json")]
[TypeFilter<RequireTenantFilter>]
public abstract class TenantController : ControllerBase
{
    /// <summary>Negocio activo del usuario. Siempre disponible dentro de una acción.</summary>
    protected TenantInfo Tenant => HttpContext.Features.Get<TenantInfo>()
        ?? throw new InvalidOperationException("Tenant no resuelto.");

    protected int BusinessId => Tenant.BusinessId;

    protected ObjectResult BadRequestProblem(string detail) => Problem(detail: detail, statusCode: StatusCodes.Status400BadRequest);
    protected ObjectResult NotFoundProblem(string detail = "El recurso no existe o fue eliminado.") => Problem(detail: detail, statusCode: StatusCodes.Status404NotFound);
    protected ObjectResult ConflictProblem(string detail) => Problem(detail: detail, statusCode: StatusCodes.Status409Conflict);
    protected ObjectResult ForbiddenProblem(string detail) => Problem(detail: detail, statusCode: StatusCodes.Status403Forbidden);
}

/// <summary>Identificadores <c>type</c> de ProblemDetails que el frontend interpreta.</summary>
public static class ApiProblemTypes
{
    public const string NoBusiness = "https://instavende.app/problems/no-business";
    public const string AdminRequired = "https://instavende.app/problems/admin-required";
}

/// <summary>Restringe una acción o controlador a administradores del negocio.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireBusinessAdminAttribute : Attribute;

/// <summary>Resuelve el negocio del usuario y lo publica como feature de la petición.</summary>
public sealed class RequireTenantFilter(CurrentUserService currentUser, ProblemDetailsFactory problems) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var tenant = await currentUser.GetTenantAsync();
        if (tenant is null)
        {
            context.Result = Problem(context, StatusCodes.Status403Forbidden, ApiProblemTypes.NoBusiness,
                "Tu usuario no tiene un negocio asociado.");
            return;
        }

        if (!tenant.IsAdmin && context.ActionDescriptor.EndpointMetadata.OfType<RequireBusinessAdminAttribute>().Any())
        {
            context.Result = Problem(context, StatusCodes.Status403Forbidden, ApiProblemTypes.AdminRequired,
                "Solo un administrador del negocio puede realizar esta acción.");
            return;
        }

        context.HttpContext.Features.Set(tenant);
        await next();
    }

    private ObjectResult Problem(ActionExecutingContext context, int status, string type, string detail)
    {
        var pd = problems.CreateProblemDetails(context.HttpContext, status, type: type, detail: detail);
        return new ObjectResult(pd) { StatusCode = status };
    }
}
