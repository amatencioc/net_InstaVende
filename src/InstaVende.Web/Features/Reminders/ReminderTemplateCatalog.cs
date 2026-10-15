using InstaVende.Core.Enums;

namespace InstaVende.Web.Features.Reminders;

/// <summary>
/// Reglas y textos predefinidos de los recordatorios automáticos por segmento:
/// cuántos recordatorios admite cada segmento, su ventana de envío, los mensajes
/// por defecto y las sugerencias que el panel ofrece en "Plantillas".
/// </summary>
public static class ReminderTemplateCatalog
{
    public const int MaxMessageLength = 200;

    public const string FirstWindow = "2-3h";
    public const string SecondWindow = "22-23h";

    public static readonly IReadOnlyList<CustomerSegment> Segments =
        [CustomerSegment.Frio, CustomerSegment.Tibio, CustomerSegment.Caliente];

    /// <summary>Frío solo admite un recordatorio (no conviene insistir); Tibio y Caliente, dos.</summary>
    public static int MaxReminders(CustomerSegment segment) => segment == CustomerSegment.Frio ? 1 : 2;

    /// <summary>Ventana de envío de cada posición: 1 → 2-3h, 2 → 22-23h.</summary>
    public static string WindowFor(int order) => order == 1 ? FirstWindow : SecondWindow;

    /// <summary>Mensajes por defecto de un negocio que aún no configuró el segmento.</summary>
    public static IReadOnlyList<ReminderTemplateItemDto> Defaults(CustomerSegment segment) => segment switch
    {
        CustomerSegment.Frio =>
        [
            new(1, FirstWindow, "Hola 👋, gracias por contactarnos. ¿En qué puedo ayudarte hoy?", true),
        ],
        CustomerSegment.Tibio =>
        [
            new(1, FirstWindow, "Gracias por escribirnos 😊 ¿Tienes alguna duda sobre nuestros productos? Estoy aquí para ayudarte.", true),
            new(2, SecondWindow, "¡Hola! Ayer hablamos un poquito. ¿Te animas a revisar de nuevo? Puedo ayudarte a elegir.", true),
        ],
        _ =>
        [
            new(1, FirstWindow, "Gracias por tu interés 🛒 Estás a un paso de tener tu pedido listo. ¿Te ayudo a completarlo?", true),
            new(2, SecondWindow, "¡Hola! Solo queríamos recordarte que tu pedido sigue apartado 📦 ¿Lo confirmamos hoy?", true),
        ],
    };

    /// <summary>Sugerencias de mensajes por segmento (español neutro, máximo 200 caracteres).</summary>
    public static IReadOnlyList<ReminderTemplateSuggestionDto> Suggestions(CustomerSegment segment) => segment switch
    {
        CustomerSegment.Frio =>
        [
            new("frio-bienvenida", "Bienvenida cercana",
                "Hola 👋, gracias por contactarnos. ¿En qué puedo ayudarte hoy?", 1),
            new("frio-catalogo", "Ofrecer el catálogo",
                "¡Hola! Vi que nos escribiste. ¿Quieres que te comparta nuestro catálogo con precios? Así eliges con calma 😊", 1),
            new("frio-pregunta-abierta", "Pregunta abierta",
                "Hola, ¿qué estás buscando? Cuéntame y te recomiendo la mejor opción para ti.", 1),
            new("frio-sin-presion", "Sin presión",
                "¡Hola! Quedo atento por si te surge alguna duda. Cuando quieras, aquí estoy para ayudarte 🙌", 1),
            new("frio-novedades", "Novedades del día",
                "Hola 👋 Hoy tenemos novedades que te pueden gustar. ¿Te cuento cuáles son?", 1),
        ],
        CustomerSegment.Tibio =>
        [
            new("tibio-dudas", "Resolver dudas",
                "Gracias por escribirnos 😊 ¿Tienes alguna duda sobre nuestros productos? Estoy aquí para ayudarte.", 1),
            new("tibio-recomendacion", "Recomendación personalizada",
                "¡Hola! Según lo que me contaste, tengo un par de opciones que te pueden encantar. ¿Te las muestro?", 1),
            new("tibio-envio", "Destacar el envío",
                "Hola 👋 Te cuento que hacemos envíos rápidos y seguros. ¿A qué zona te lo enviaríamos?", 1),
            new("tibio-retomar", "Retomar la conversación",
                "¡Hola! Ayer hablamos un poquito. ¿Te animas a revisar de nuevo? Puedo ayudarte a elegir.", 2),
            new("tibio-stock", "Stock limitado",
                "Hola 😊 Te escribo porque el producto que viste tiene pocas unidades disponibles. ¿Quieres que te separe uno?", 2),
            new("tibio-opiniones", "Prueba social",
                "¡Hola! Muchos clientes ya lo probaron y quedaron felices ⭐ ¿Te gustaría ver algunas opiniones?", 2),
        ],
        _ =>
        [
            new("caliente-un-paso", "A un paso",
                "Gracias por tu interés 🛒 Estás a un paso de tener tu pedido listo. ¿Te ayudo a completarlo?", 1),
            new("caliente-pago", "Facilitar el pago",
                "¡Hola! Para confirmar tu pedido solo falta el pago. Te comparto los métodos disponibles, ¿cuál prefieres?", 1),
            new("caliente-datos", "Pedir datos de entrega",
                "Hola 👋 ¿Me confirmas tu dirección de entrega? Así programamos tu pedido hoy mismo 📦", 1),
            new("caliente-apartado", "Pedido apartado",
                "¡Hola! Solo queríamos recordarte que tu pedido sigue apartado 📦 ¿Lo confirmamos hoy?", 2),
            new("caliente-ultimo-aviso", "Último aviso amable",
                "Hola 😊 Tu pedido sigue reservado, pero solo podemos guardarlo hasta hoy. ¿Lo dejamos listo?", 2),
            new("caliente-ayuda", "Ofrecer ayuda",
                "¡Hola! ¿Hubo algo que te hizo dudar? Cuéntame y lo resolvemos juntos para que recibas tu pedido pronto 🙌", 2),
        ],
    };
}
