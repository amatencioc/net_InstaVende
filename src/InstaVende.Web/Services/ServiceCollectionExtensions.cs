using Microsoft.Extensions.Options;
using Microsoft.Extensions.Options;

namespace InstaVende.Web.Services;

/// <summary>
/// Extensiones de <see cref="IServiceCollection"/> para registrar y validar
/// todas las opciones tipadas de la aplicación en un único punto,
/// y para centralizar los clientes HTTP nombrados.
/// </summary>
internal static class ServiceCollectionExtensions
{
    /// <summary>
    /// Enlaza, valida y registra todas las secciones de configuración tipadas.
    /// Si algún valor obligatorio falta en producción, la aplicación lanzará
    /// una excepción descriptiva antes de aceptar tráfico.
    /// </summary>
    public static IServiceCollection AddValidatedOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ?? BotCache ?????????????????????????????????????????????????????????
        // Sin validador propio: todos sus valores tienen defaults razonables.
        services.Configure<BotCacheOptions>(
            configuration.GetSection(BotCacheOptions.SectionName));

        // ?? OpenAI ???????????????????????????????????????????????????????????
        // Exige ApiKey fuera de Development para evitar errores en tiempo de ejecución.
        services
            .AddOptions<OpenAiOptions>()
            .Bind(configuration.GetSection(OpenAiOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<OpenAiOptions>, OpenAiOptionsValidator>();

        // ?? WhatsAppClient ???????????????????????????????????????????????????
        // Exige BaseUrl válida y ClientPath no vacío fuera de Development.
        services
            .AddOptions<WhatsAppClientOptions>()
            .Bind(configuration.GetSection(WhatsAppClientOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<WhatsAppClientOptions>, WhatsAppClientOptionsValidator>();

        // ?? HttpClients ??????????????????????????????????????????????????????
        // TimeoutSeconds > 0 en todos los entornos sin excepción.
        foreach (var clientName in new[] { "wa-health", "wa-send" })
        {
            var name = clientName; // captura para el closure
            services
                .AddOptions<HttpClientOptions>(name)
                .Bind(configuration.GetSection($"{HttpClientOptions.SectionName}:{name}"))
                .ValidateOnStart();
            services.AddSingleton<IValidateOptions<HttpClientOptions>>(
                new HttpClientOptionsValidator(name));
        }

        return services;
    }

    /// <summary>
    /// Registra los clientes HTTP nombrados que usa la aplicación.
    /// Los timeouts se leen de la sección <c>HttpClients:{name}:TimeoutSeconds</c>
    /// en <c>appsettings.json</c>; si la clave no existe se aplica el valor por defecto indicado.
    /// <list type="bullet">
    ///   <item><c>wa-health</c> — sondeo rápido de disponibilidad del proceso Node (default 2 s).</item>
    ///   <item><c>wa-send</c>   — envío de mensajes WhatsApp (default 10 s).</item>
    /// </list>
    /// </summary>
    public static IServiceCollection AddHttpClients(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Cliente genérico sin configuración especial (usado por servicios que no son WhatsApp)
        services.AddHttpClient();

        // Sondeo de salud al arranque: falla rápido si Node no responde
        services.AddNamedHttpClient(configuration, "wa-health", defaultTimeoutSeconds: 2);

        // Envío de mensajes: margen mayor para la respuesta del servidor Node
        services.AddNamedHttpClient(configuration, "wa-send", defaultTimeoutSeconds: 10);

        return services;
    }

    /// <summary>
    /// Registra un <see cref="HttpClient"/> nombrado cuyo timeout se obtiene de
    /// <c>HttpClients:{name}:TimeoutSeconds</c> en la configuración.
    /// Si la clave no existe se usa <paramref name="defaultTimeoutSeconds"/>.
    /// </summary>
    /// <param name="services">Colección de servicios.</param>
    /// <param name="configuration">Configuración de la aplicación.</param>
    /// <param name="name">Nombre lógico del cliente HTTP.</param>
    /// <param name="defaultTimeoutSeconds">Timeout en segundos usado cuando la clave de configuración no existe.</param>
    private static IServiceCollection AddNamedHttpClient(
        this IServiceCollection services,
        IConfiguration configuration,
        string name,
        int defaultTimeoutSeconds)
    {
        var timeoutSeconds = configuration.GetValue<int>(
            $"HttpClients:{name}:TimeoutSeconds",
            defaultTimeoutSeconds);

        services.AddHttpClient(name, (sp, c) =>
        {
            // Preferir el valor validado de IOptions si está disponible;
            // si la sección no existía en config se mantiene el default.
            var monitor = sp.GetService<IOptionsMonitor<HttpClientOptions>>();
            var configured = monitor?.Get(name)?.TimeoutSeconds ?? 0;
            c.Timeout = TimeSpan.FromSeconds(configured > 0 ? configured : timeoutSeconds);
        });

        return services;
    }
}
