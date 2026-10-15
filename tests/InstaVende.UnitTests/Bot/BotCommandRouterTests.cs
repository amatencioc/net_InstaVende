using InstaVende.Web.Services.Bot;

namespace InstaVende.UnitTests.Bot;

public class BotCommandRouterTests
{
    [Theory]
    [InlineData("#catalogo", BotCommand.Catalog)]
    [InlineData("#Catálogo", BotCommand.Catalog)]
    [InlineData("#carrito", BotCommand.Cart)]
    [InlineData("#pedido", BotCommand.Order)]
    [InlineData("#limpiar", BotCommand.ClearCart)]
    [InlineData("#ayuda", BotCommand.Help)]
    [InlineData("#estado", BotCommand.Status)]
    [InlineData("Ver catálogo", BotCommand.Catalog)]
    [InlineData("  mi carrito!! ", BotCommand.Cart)]
    [InlineData("¿Dónde está mi pedido?", BotCommand.Status)]
    public void Detect_ReconoceComandos(string message, BotCommand expected)
        => Assert.Equal(expected, BotCommandRouter.Detect(message).Command);

    [Theory]
    [InlineData("necesito ayuda con mi talla")]
    [InlineData("¿qué hay de envíos a Arequipa?")]
    [InlineData("quiero cancelar mi pedido")]
    [InlineData("confirmar mi pedido para mañana")]
    [InlineData("hola, qué tienen en zapatillas")]
    [InlineData("#desconocido")]
    [InlineData("")]
    public void Detect_NoSecuestraFrasesNaturales(string message)
        => Assert.Equal(BotCommand.None, BotCommandRouter.Detect(message).Command);

    [Theory]
    [InlineData("agregar 3", 3, 1)]
    [InlineData("Añadir 12", 12, 1)]
    [InlineData("quiero el 2", 2, 1)]
    [InlineData("agregar 4 x 3", 4, 3)]
    public void Detect_AgregarAlCarrito(string message, int number, int quantity)
    {
        var (command, n, qty) = BotCommandRouter.Detect(message);

        Assert.Equal(BotCommand.AddToCart, command);
        Assert.Equal(number, n);
        Assert.Equal(quantity, qty);
    }

    [Theory]
    [InlineData("quiero hablar con un asesor por favor", "hablar con un asesor", true)]
    [InlineData("Hablar con un ASESOR", "hablar con un asesor", true)]
    [InlineData("asesoramiento", "asesor", false)]
    [InlineData("hola", "", false)]
    public void ContainsPhrase_CoincidePorPalabraCompleta(string message, string phrase, bool expected)
        => Assert.Equal(expected, BotCommandRouter.ContainsPhrase(message, phrase));
}
