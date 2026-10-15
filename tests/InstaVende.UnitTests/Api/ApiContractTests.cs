using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace InstaVende.UnitTests.Api;

/// <summary>Reglas que MVC solo comprueba en ejecución y que deben detectarse antes en las pruebas.</summary>
public class ApiContractTests
{
    private static readonly Assembly WebAssembly = typeof(InstaVende.Web.Controllers.TenantController).Assembly;

    /// <summary>
    /// En records posicionales los atributos de validación deben ir en el parámetro del constructor;
    /// con <c>[property: Required]</c> MVC lanza InvalidOperationException al validar (HTTP 500).
    /// </summary>
    [Fact]
    public void RecordsPosicionales_NoTienenValidacionEnPropiedades()
    {
        var offenders = WebAssembly.GetTypes()
            .Where(t => t.GetMethod("<Clone>$") != null) // records
            .SelectMany(t =>
            {
                var ctorParams = t.GetConstructors()
                    .SelectMany(c => c.GetParameters())
                    .Select(p => p.Name)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                return t.GetProperties()
                    .Where(p => ctorParams.Contains(p.Name) && p.GetCustomAttributes<ValidationAttribute>(inherit: true).Any())
                    .Select(p => $"{t.Name}.{p.Name}");
            })
            .ToList();

        Assert.Empty(offenders);
    }

    /// <summary>Todo endpoint del panel debe resolver el negocio (aislamiento multi-tenant).</summary>
    [Fact]
    public void ControladoresDelPanel_HeredanDeTenantController()
    {
        string[] publicControllers = ["AuthController", "WebhooksController", "InvitationsController"];
        var offenders = WebAssembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t))
            .Where(t => !typeof(InstaVende.Web.Controllers.TenantController).IsAssignableFrom(t) && !publicControllers.Contains(t.Name))
            .Select(t => t.FullName)
            .ToList();

        Assert.Empty(offenders);
    }
}
