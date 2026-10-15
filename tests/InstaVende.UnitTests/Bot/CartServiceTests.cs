using InstaVende.Core.Entities;
using InstaVende.Core.Enums;
using InstaVende.Infrastructure.Data;
using InstaVende.Web.Services.Bot;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InstaVende.UnitTests.Bot;

public sealed class CartServiceTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private AppDbContext _db = null!;
    private int _businessId, _otherBusinessId, _conversationId, _contactId, _productId, _foreignProductId;

    public async ValueTask InitializeAsync()
    {
        await _connection.OpenAsync();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        await _db.Database.EnsureCreatedAsync();

        var owner = new ApplicationUser { Id = "u1", UserName = "a@a.com", Email = "a@a.com" };
        var otherOwner = new ApplicationUser { Id = "u2", UserName = "b@b.com", Email = "b@b.com" };
        var business = new Business { Name = "Tienda", User = owner };
        var other = new Business { Name = "Otra", User = otherOwner };
        var contact = new Contact { Business = business, ChannelType = ChannelType.WhatsApp, ExternalId = "519" };
        var conversation = new Conversation { Business = business, Contact = contact, ChannelType = ChannelType.WhatsApp };
        var product = new Product { Business = business, Name = "Zapatilla", Price = 100m, Stock = 3 };
        var foreign = new Product { Business = other, Name = "Ajeno", Price = 1m, Stock = 50 };
        _db.AddRange(business, other, contact, conversation, product, foreign);
        await _db.SaveChangesAsync();

        (_businessId, _otherBusinessId, _conversationId, _contactId, _productId, _foreignProductId) =
            (business.Id, other.Id, conversation.Id, contact.Id, product.Id, foreign.Id);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private BotContext Context(decimal freeShippingThreshold = 0, params DeliveryZoneInfo[] zones) => new()
    {
        BusinessId = _businessId, BusinessName = "Tienda", IsActive = true, BotName = "Bot", FallbackMessage = "?",
        EnableHandoff = true, HandoffMessage = "asesor", AutoPauseOnHandoff = true, Country = "Perú", Currency = "PEN",
        Negotiation = new NegotiationRules(5, 10, 15, 15, 3, 5, freeShippingThreshold),
        ActivePromotions = "", DeliveryZones = zones, Intents = [], QuickAnswers = [],
        PurchaseConfirmationMessage = "Gracias", StaticPlaceholders = new Dictionary<string, string>(),
    };

    [Fact]
    public async Task AddItem_UsaElPrecioDelCatalogoYAcumulaCantidades()
    {
        var cart = new CartService(_db);

        await cart.AddItemAsync(Context(), _conversationId, _productId, 1, ct: TestContext.Current.CancellationToken);
        var result = await cart.AddItemAsync(Context(), _conversationId, _productId, 2, ct: TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        var item = Assert.Single(result.Order!.Items);
        Assert.Equal(3, item.Quantity);
        Assert.Equal(100m, item.UnitPrice);
        Assert.Equal(300m, result.Order.Subtotal);
        Assert.Equal(OrderStatus.Cart, result.Order.Status);
    }

    [Fact]
    public async Task AddItem_RechazaStockInsuficiente()
    {
        var result = await new CartService(_db).AddItemAsync(Context(), _conversationId, _productId, 4, ct: TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.Contains("3", result.Message);
    }

    [Fact]
    public async Task AddItem_NoPermiteProductosDeOtroNegocio()
    {
        var result = await new CartService(_db).AddItemAsync(Context(), _conversationId, _foreignProductId, 1, ct: TestContext.Current.CancellationToken);

        Assert.False(result.Success);
        Assert.False(await _db.Orders.AnyAsync(o => o.BusinessId == _otherBusinessId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Confirm_CreaPedidoEnValidacionConEnvioYActualizaElCliente()
    {
        var ct = TestContext.Current.CancellationToken;
        var cart = new CartService(_db);
        var ctx = Context(zones: new DeliveryZoneInfo("Miraflores", 10m, null));
        await cart.AddItemAsync(ctx, _conversationId, _productId, 2, ct: ct);

        var result = await cart.ConfirmAsync(ctx, _conversationId, "Av. Larco 123", "Yape", "miraflores", null, ct);

        Assert.True(result.Success, result.Message);
        var order = result.Order!;
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(OrderSubStatus.EnValidacion, order.SubStatus);
        Assert.Equal(10m, order.ShippingCost);
        Assert.Equal(210m, order.Total);
        Assert.StartsWith("IV-", order.OrderNumber);

        _db.ChangeTracker.Clear();
        var contact = await _db.Contacts.SingleAsync(c => c.Id == _contactId, ct);
        Assert.Equal(1, contact.TotalPurchases);
        Assert.Equal(210m, contact.TotalSpent);
        Assert.NotNull(contact.LastPurchaseAt);
    }

    [Fact]
    public async Task Confirm_AplicaEnvioGratisSobreElUmbral()
    {
        var ct = TestContext.Current.CancellationToken;
        var cart = new CartService(_db);
        var ctx = Context(freeShippingThreshold: 150, new DeliveryZoneInfo("Lima", 12m, null));
        await cart.AddItemAsync(ctx, _conversationId, _productId, 2, ct: ct);

        var result = await cart.ConfirmAsync(ctx, _conversationId, null, "Efectivo", "Lima", null, ct);

        Assert.Equal(0m, result.Order!.ShippingCost);
        Assert.Equal(200m, result.Order.Total);
    }

    [Fact]
    public async Task Confirm_ConCarritoVacio_Falla()
    {
        var result = await new CartService(_db).ConfirmAsync(Context(), _conversationId, null, null, null, null, TestContext.Current.CancellationToken);

        Assert.False(result.Success);
    }
}
