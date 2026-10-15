using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using InstaVende.Core.Domain;
using InstaVende.Core.Entities;
using InstaVende.Core.Enums;
using InstaVende.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace InstaVende.Web.Infrastructure.DemoData;

/// <summary>
/// Genera datos de demostración realistas para el negocio de un usuario (solo entorno Development):
/// catálogo, clientes, conversaciones, pedidos en todas las etapas, recordatorios, configuración del
/// vendedor y equipo. Reejecutable: borra y vuelve a crear los datos de ESE negocio únicamente.
/// <para>Uso: <c>dotnet run --project src/InstaVende.Web -- --seed-demo demo@instavende.pe</c></para>
/// </summary>
public sealed class DemoDataSeeder(AppDbContext db, UserManager<ApplicationUser> users, IWebHostEnvironment env, ILogger<DemoDataSeeder> logger)
{
    private const string MemberEmail = "vendedor@instavende.pe";
    private const string DemoPassword = "Demo12345";

    private readonly Random _rnd = new(20261015); // determinista: misma demo en cada ejecución
    private readonly DateTime _now = DateTime.UtcNow;

    public static async Task RunAsync(IServiceProvider services, string email)
    {
        await using var scope = services.CreateAsyncScope();
        var seeder = ActivatorUtilities.CreateInstance<DemoDataSeeder>(scope.ServiceProvider);
        await seeder.SeedAsync(email);
    }

    public async Task SeedAsync(string email)
    {
        var owner = await users.FindByEmailAsync(email)
            ?? throw new InvalidOperationException($"No existe el usuario {email}. Regístralo primero en la app.");
        var business = await db.Businesses.FirstOrDefaultAsync(b => b.UserId == owner.Id)
            ?? throw new InvalidOperationException($"El usuario {email} no es propietario de ningún negocio.");

        logger.LogInformation("Generando datos de demostración para «{Business}» (id {Id})…", business.Name, business.Id);
        await WipeAsync(business.Id);

        ConfigureBusiness(business);
        ConfigureSeller(business.Id);
        var products = CreateCatalog(business.Id);
        CreateSellerData(business.Id);
        await db.SaveChangesAsync();

        var labels = CreateLabels(business.Id);
        var contacts = CreateContacts(business.Id);
        await db.SaveChangesAsync();

        var conversations = CreateConversations(business, contacts, labels, owner.Id, products);
        await db.SaveChangesAsync();

        CreateOrders(business.Id, contacts, conversations, products);
        CreateReminders(business.Id, contacts, conversations, owner.Id);
        CreateReminderTemplates(business.Id);
        await CreateTeamAsync(business.Id, email);
        await db.SaveChangesAsync();

        await UpdateContactStatsAsync(business.Id);
        logger.LogInformation("Datos de demostración listos: {Products} productos, {Contacts} clientes, {Conversations} conversaciones.",
            products.Count, contacts.Count, conversations.Count);
    }

    // ── Limpieza del negocio ─────────────────────────────────────────────────

    private async Task WipeAsync(int bid)
    {
        await db.Messages.Where(m => m.Conversation.BusinessId == bid).ExecuteDeleteAsync();
        await db.OrderItems.Where(i => i.Order.BusinessId == bid).ExecuteDeleteAsync();
        await db.Orders.Where(o => o.BusinessId == bid).ExecuteDeleteAsync();
        await db.Reminders.Where(r => r.BusinessId == bid).ExecuteDeleteAsync();
        await db.Conversations.Where(c => c.BusinessId == bid).ExecuteDeleteAsync();
        await db.ConversationLabels.Where(l => l.BusinessId == bid).ExecuteDeleteAsync();
        await db.Contacts.Where(c => c.BusinessId == bid).ExecuteDeleteAsync();
        await db.ProductImages.Where(i => i.Product.BusinessId == bid).ExecuteDeleteAsync();
        await db.ProductVariants.Where(v => v.Product.BusinessId == bid).ExecuteDeleteAsync();
        await db.Products.Where(p => p.BusinessId == bid).ExecuteDeleteAsync();
        await db.ProductCategories.Where(c => c.BusinessId == bid).ExecuteDeleteAsync();
        await db.KnowledgeEntries.Where(k => k.BusinessId == bid).ExecuteDeleteAsync();
        await db.DeliveryZones.Where(z => z.BusinessId == bid).ExecuteDeleteAsync();
        await db.PaymentMethods.Where(p => p.BusinessId == bid).ExecuteDeleteAsync();
        await db.PaymentImages.Where(p => p.BusinessId == bid).ExecuteDeleteAsync();
        await db.ReminderTemplates.Where(t => t.BusinessId == bid).ExecuteDeleteAsync();
        await db.NotificationEmails.Where(n => n.BusinessId == bid).ExecuteDeleteAsync();
        await db.UserInvitations.Where(i => i.BusinessId == bid).ExecuteDeleteAsync();
        await db.BusinessUsers.Where(u => u.BusinessId == bid).ExecuteDeleteAsync();
        await db.BotIntents.Where(i => i.BotConfig.BusinessId == bid).ExecuteDeleteAsync();
        await db.BotKnowledges.Where(k => k.BotConfig.BusinessId == bid).ExecuteDeleteAsync();
        await db.ConversationFlows.Where(f => f.BotConfig.BusinessId == bid).ExecuteDeleteAsync();
        await db.BotConfigs.Where(c => c.BusinessId == bid).ExecuteDeleteAsync();
        await db.VendorConfigs.Where(v => v.BusinessId == bid).ExecuteDeleteAsync();
        await db.OnboardingProgresses.Where(o => o.BusinessId == bid).ExecuteDeleteAsync();

        var demoImages = Path.Combine(UploadsRoot, bid.ToString(), "products");
        if (Directory.Exists(demoImages))
            foreach (var file in Directory.GetFiles(demoImages, "demo-*.svg")) File.Delete(file);
    }

    private string UploadsRoot => Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), "uploads");

    // ── Negocio y vendedor ───────────────────────────────────────────────────

    private static void ConfigureBusiness(Business b)
    {
        b.Description = "Tienda online de zapatillas urbanas y deportivas originales en Lima. Envíos a todo el Perú.";
        b.Sector = "Moda y calzado";
        b.Country = "Perú";
        b.Currency = "PEN - Sol Peruano";
        b.Phone = "+51 999 888 777";
        b.WhatsAppNumber = "51999888777";
        b.Email = "ventas@urbanstep.pe";
        b.WebsiteUrl = "https://urbanstep.pe";
        b.SocialMedia = "Instagram: @urbanstep.pe · TikTok: @urbanstep";
    }

    private void ConfigureSeller(int bid)
    {
        db.VendorConfigs.Add(new VendorConfig
        {
            BusinessId = bid,
            VendorName = "Favio",
            VendorGender = "Masculino",
            Country = "Perú",
            BusinessDescription = "Vendemos zapatillas urbanas, running y casuales 100 % originales. Tallas 36 a 44. Delivery en Lima en 24 h y envíos a provincia por Olva.",
            TargetAudience = "Jóvenes y adultos de 18 a 40 años que buscan estilo y comodidad.",
            Rules = "SIEMPRE confirma la talla antes de cerrar la venta.\nSIEMPRE ofrece el envío gratis a partir de S/ 250.\nNUNCA prometas entregas el mismo día.\nNUNCA ofrezcas descuentos sobre productos en oferta.",
            CommunicationStyle = "Cercano, alegre y directo. Tutea al cliente y responde con mensajes cortos.",
            SalesStyle = "Consultivo: pregunta por el uso que le dará y recomienda máximo 2 opciones con su beneficio principal.",
            ResponseLength = 3,
            UseEmojis = true,
            EmojiPalette = JsonSerializer.Serialize(new[] { "👟", "🔥", "✅", "🚚", "😊", "🙌" }),
            UseOpeningPunctuation = true,
            WordsToAvoid = "barato, imitación, réplica",
            WelcomeMessage = "¡Hola! 👋 Soy Favio de Urban Step. ¿Buscas zapatillas urbanas, para correr o algo casual?",
            PurchaseConfirmationMessage = "¡Gracias por tu compra! 🙌 Validamos tu pago y te avisamos cuando tu pedido salga a reparto.",
            HumanHandoffSituations = "Reclamos por talla o producto dañado\nCambios y devoluciones\nPedidos mayoristas (más de 6 pares)",
            AutoPauseOnHandoff = true,
            HandoffExampleMessage = "Te comunico con un asesor de Urban Step, en breve te atiende. 🙌",
        });

        var bot = new BotConfig
        {
            BusinessId = bid,
            BotName = "Favio",
            IsActive = true,
            FallbackMessage = "Disculpa, no te entendí bien. ¿Me cuentas qué zapatilla buscas o en qué te ayudo?",
            EnableHandoff = true,
            HandoffTriggerPhrase = "hablar con un asesor",
            BusinessHours = "Lunes a Sábado 09:00-20:00",
            ReturnPolicy = "Cambios por talla dentro de los 7 días, con la caja y sin uso. El primer cambio es gratis en Lima.",
            WarrantyPolicy = "30 días de garantía por defectos de fábrica.",
            ShippingTimes = "Lima: 24 h hábiles. Provincias: 3 a 5 días hábiles por Olva Courier.",
            ActivePromotions = "Envío gratis en compras desde S/ 250. 2.º par al 20 % en la línea Classic.",
            DiscountLevel1 = 5, DiscountLevel2 = 8, DiscountLevel3 = 10, MaxDiscountPercent = 12,
            LoyaltyDiscount = 3, BundleDiscount = 5, FreeShippingThreshold = 250,
        };
        bot.Intents.Add(new BotIntent
        {
            IntentName = "Horario de atención",
            TriggerPhrases = JsonSerializer.Serialize(new[] { "horario de atención", "a qué hora atienden", "hasta qué hora atienden" }),
            Response = "🕘 Atendemos de lunes a sábado de 9:00 a 20:00. ¡Pero puedes escribirme cuando quieras, respondo al toque!",
            Priority = 10,
        });
        bot.Intents.Add(new BotIntent
        {
            IntentName = "Ubicación",
            TriggerPhrases = JsonSerializer.Serialize(new[] { "dónde están ubicados", "tienen tienda física", "dirección de la tienda" }),
            Response = "📍 Somos tienda 100 % online. Hacemos delivery en Lima en 24 h y envíos a todo el Perú.",
            Priority = 5,
        });
        db.BotConfigs.Add(bot);

        db.OnboardingProgresses.Add(new OnboardingProgress
        {
            BusinessId = bid, ProductsAdded = true, PaymentMethodsAdded = true, BotConfigured = true, BotTested = true,
        });
    }

    // ── Catálogo ─────────────────────────────────────────────────────────────

    private sealed record DemoProduct(string Category, string Name, string Sku, decimal Price, decimal? Original, int Stock,
        string Description, string Color1, string Color2, bool Featured, string[] Sizes, string? ColorOptions = null);

    private static readonly DemoProduct[] CatalogData =
    [
        new("Urbanas", "Zapatilla Urban Blanca", "URB-BL", 189.90m, 229.90m, 18, "Urbana de cuero sintético con suela de goma antideslizante. Combina con todo.", "#f8fafc", "#cbd5e1", true, ["38", "39", "40", "41", "42", "43"]),
        new("Urbanas", "Zapatilla Urban Negra", "URB-NG", 189.90m, null, 9, "La versión total black de nuestra Urban. Resistente y fácil de limpiar.", "#334155", "#0f172a", false, ["39", "40", "41", "42"]),
        new("Urbanas", "Zapatilla Street High", "STR-HI", 219.90m, null, 4, "Caña alta con acolchado en el tobillo. Estilo skate.", "#7c3aed", "#4c1d95", true, ["39", "40", "41", "42", "43"], "Morado, Negro"),
        new("Running", "Zapatilla Runner Pro", "RUN-PRO", 249.90m, 299.90m, 14, "Ligera (230 g) y transpirable, con amortiguación reforzada para entrenar a diario.", "#22c55e", "#15803d", true, ["38", "39", "40", "41", "42", "43", "44"], "Verde, Azul"),
        new("Running", "Zapatilla Runner Lite", "RUN-LT", 199.90m, null, 11, "Para correr distancias cortas y gimnasio. Suela flexible.", "#38bdf8", "#0369a1", false, ["37", "38", "39", "40", "41", "42"]),
        new("Running", "Zapatilla Trail X", "TRL-X", 289.90m, null, 0, "Suela con tacos para trail running y montaña. Impermeable.", "#f97316", "#9a3412", false, ["40", "41", "42", "43"]),
        new("Casual", "Zapatilla Classic Lona", "CLS-LN", 129.90m, null, 25, "Clásica de lona, ligera y fresca. Ideal para el día a día.", "#ef4444", "#991b1b", false, ["36", "37", "38", "39", "40", "41", "42"], "Rojo, Blanco, Negro"),
        new("Casual", "Zapatilla Classic Slip-On", "CLS-SO", 119.90m, 139.90m, 3, "Sin pasadores, se pone en segundos. Plantilla acolchada.", "#a3e635", "#3f6212", false, ["37", "38", "39", "40", "41"]),
        new("Casual", "Mocasín Urban Suede", "MOC-SD", 179.90m, null, 7, "Gamuza sintética con costuras a mano. Para un look casual elegante.", "#a16207", "#713f12", false, ["39", "40", "41", "42"]),
        new("Accesorios", "Medias deportivas x3", "MED-3", 29.90m, null, 60, "Pack de 3 pares de medias técnicas con refuerzo en talón y punta.", "#e2e8f0", "#64748b", false, []),
        new("Accesorios", "Kit de limpieza para zapatillas", "KIT-LMP", 39.90m, 49.90m, 22, "Espuma limpiadora, cepillo y paño de microfibra.", "#14b8a6", "#0f766e", true, []),
        new("Accesorios", "Pasadores de colores", "PAS-COL", 14.90m, null, 2, "Par de pasadores planos de 120 cm. Varios colores.", "#f472b6", "#be185d", false, []),
    ];

    private List<Product> CreateCatalog(int bid)
    {
        var categories = CatalogData.Select(p => p.Category).Distinct()
            .ToDictionary(n => n, n => new ProductCategory { BusinessId = bid, Name = n, CreatedAt = _now.AddDays(-60) });
        db.ProductCategories.AddRange(categories.Values);

        var dir = Path.Combine(UploadsRoot, bid.ToString(), "products");
        Directory.CreateDirectory(dir);

        var products = new List<Product>();
        for (var i = 0; i < CatalogData.Length; i++)
        {
            var d = CatalogData[i];
            var fileName = $"demo-{d.Sku.ToLowerInvariant()}.svg";
            File.WriteAllText(Path.Combine(dir, fileName), ProductSvg(d), Encoding.UTF8);
            var url = $"/uploads/{bid}/products/{fileName}";

            var product = new Product
            {
                BusinessId = bid, Category = categories[d.Category], Name = d.Name, Sku = d.Sku, Description = d.Description,
                Price = d.Price, PriceOriginal = d.Original, Stock = d.Stock, IsActive = true, IsFeatured = d.Featured,
                SortOrder = i, ImageUrl = url, CreatedAt = _now.AddDays(-58 + i), UpdatedAt = _now.AddDays(-_rnd.Next(1, 20)),
            };
            product.Images.Add(new ProductImage { Url = url, IsMain = true, SortOrder = 0 });

            var colors = d.ColorOptions?.Split(", ") ?? [];
            foreach (var size in d.Sizes)
                product.Variants.Add(new ProductVariant
                {
                    AttributeName = "Talla", AttributeValue = size, PriceModifier = 0,
                    Stock = d.Stock == 0 ? 0 : _rnd.Next(0, 5), Sku = $"{d.Sku}-{size}", IsActive = true,
                });
            foreach (var color in colors.Skip(1))
                product.Variants.Add(new ProductVariant
                {
                    AttributeName = "Color", AttributeValue = color, PriceModifier = color == "Blanco" ? 10 : 0,
                    Stock = _rnd.Next(1, 6), Sku = $"{d.Sku}-{color[..3].ToUpperInvariant()}", IsActive = true,
                });
            products.Add(product);
        }
        db.Products.AddRange(products);
        return products;
    }

    /// <summary>Ilustración SVG de producto (degradado con zapatilla) para la demo.</summary>
    private static string ProductSvg(DemoProduct p)
    {
        var emoji = p.Category switch { "Accesorios" => p.Sku.StartsWith("MED") ? "🧦" : p.Sku.StartsWith("KIT") ? "🧽" : "🎀", "Running" => "👟", "Casual" => "👞", _ => "👟" };
        return $"""
            <svg xmlns="http://www.w3.org/2000/svg" width="600" height="600" viewBox="0 0 600 600">
              <defs><linearGradient id="g" x1="0" y1="0" x2="1" y2="1"><stop offset="0" stop-color="{p.Color1}"/><stop offset="1" stop-color="{p.Color2}"/></linearGradient></defs>
              <rect width="600" height="600" rx="48" fill="url(#g)"/>
              <circle cx="300" cy="270" r="170" fill="#ffffff" opacity="0.18"/>
              <text x="300" y="340" font-size="220" text-anchor="middle">{emoji}</text>
              <text x="300" y="530" font-family="Inter, Segoe UI, sans-serif" font-size="34" font-weight="700" fill="#ffffff" text-anchor="middle" opacity="0.95">{System.Security.SecurityElement.Escape(p.Name)}</text>
            </svg>
            """;
    }

    private void CreateSellerData(int bid)
    {
        db.PaymentMethods.AddRange(
            new PaymentMethod { BusinessId = bid, Type = PaymentMethodType.DigitalWallet, Name = "Yape / Plin", Instructions = "Envía el pago al 999 888 777 a nombre de *Urban Step SAC* y comparte la captura por este chat.", AccountNumber = "999888777", AccountAlias = "Urban Step SAC", SortOrder = 0 },
            new PaymentMethod { BusinessId = bid, Type = PaymentMethodType.BankTransfer, Name = "Transferencia BCP", Instructions = "Cuenta de ahorros en soles. Envía la constancia para validar tu pedido.", AccountNumber = "191-98765432-0-55", AccountAlias = "CCI 002-191-198765432055-12", SortOrder = 1 },
            new PaymentMethod { BusinessId = bid, Type = PaymentMethodType.Cash, Name = "Contraentrega", Instructions = "Pagas en efectivo o con POS al recibir tu pedido (solo Lima).", SortOrder = 2 },
            new PaymentMethod { BusinessId = bid, Type = PaymentMethodType.MercadoPago, Name = "Tarjeta (Mercado Pago)", Instructions = "Te enviamos un link de pago seguro.", PaymentLink = "https://mpago.la/urbanstep", IsActive = false, SortOrder = 3 });

        var zones = new (string Name, decimal? Cost, string Desc)[]
        {
            ("Miraflores", 10, "Entrega en 24 h"), ("San Isidro", 10, "Entrega en 24 h"), ("Surco", 12, "Entrega en 24 h"),
            ("San Borja", 12, "Entrega en 24 h"), ("Los Olivos", 15, "Entrega en 24-48 h"), ("Callao", 15, "Entrega en 24-48 h"),
            ("Provincias (Olva)", 20, "3 a 5 días hábiles"), ("Recojo en almacén (Surquillo)", 0, "Coordinando por chat"),
        };
        db.DeliveryZones.AddRange(zones.Select((z, i) => new DeliveryZone { BusinessId = bid, Name = z.Name, Cost = z.Cost, Description = z.Desc, SortOrder = i }));

        var kb = new (KnowledgeCategory Cat, string Title, string Content, bool Fav)[]
        {
            (KnowledgeCategory.Envios, "¿Hacen envíos a provincia?", "Sí, enviamos por Olva Courier a todo el Perú en 3 a 5 días hábiles. Costo fijo S/ 20.", true),
            (KnowledgeCategory.Envios, "¿Cuánto demora el delivery en Lima?", "24 horas hábiles. Repartimos de 10:00 a 19:00 y te avisamos cuando el motorizado esté cerca.", true),
            (KnowledgeCategory.Envios, "¿Hacen entregas el mismo día?", "No, el delivery es al día siguiente hábil.", false),
            (KnowledgeCategory.Pagos, "¿Aceptan pago contraentrega?", "Sí, solo en Lima. Puedes pagar en efectivo o con POS al recibir.", true),
            (KnowledgeCategory.Pagos, "¿Puedo pagar en cuotas?", "Sí, con tarjeta de crédito a través del link de Mercado Pago.", false),
            (KnowledgeCategory.Productos, "¿Las zapatillas son originales?", "Sí, todos nuestros productos son originales y vienen con caja y boleta.", true),
            (KnowledgeCategory.Productos, "¿Cómo sé mi talla?", "Mide tu pie de talón a punta: 25 cm = talla 39, 26 cm = 40, 26.5 cm = 41, 27.5 cm = 42.", false),
            (KnowledgeCategory.Devoluciones, "¿Puedo cambiar la talla?", "Sí, dentro de los 7 días con la caja y sin uso. El primer cambio es gratis en Lima.", true),
            (KnowledgeCategory.Horarios, "¿Atienden domingos?", "El chat responde todos los días; los despachos son de lunes a sábado.", false),
            (KnowledgeCategory.Otros, "¿Venden al por mayor?", "Sí, desde 6 pares hay precio especial. Te derivamos con un asesor.", false),
        };
        db.KnowledgeEntries.AddRange(kb.Select((k, i) => new KnowledgeEntry
        {
            BusinessId = bid, Category = k.Cat, Title = k.Title, Content = k.Content, IsFavorite = k.Fav,
            CreatedAt = _now.AddDays(-40 + i), UpdatedAt = _now.AddDays(-20 + i),
        }));

        db.NotificationEmails.AddRange(
            new NotificationEmail { BusinessId = bid, Email = "ventas@urbanstep.pe" },
            new NotificationEmail { BusinessId = bid, Email = "gerencia@urbanstep.pe", IsActive = false });
    }

    // ── Clientes y conversaciones ────────────────────────────────────────────

    private Dictionary<string, ConversationLabel> CreateLabels(int bid)
    {
        var labels = new Dictionary<string, ConversationLabel>
        {
            ["VIP"] = new() { BusinessId = bid, Name = "VIP", Color = "#7c3aed" },
            ["Mayorista"] = new() { BusinessId = bid, Name = "Mayorista", Color = "#0ea5e9" },
            ["Reclamo"] = new() { BusinessId = bid, Name = "Reclamo", Color = "#ef4444" },
            ["Seguimiento"] = new() { BusinessId = bid, Name = "Seguimiento", Color = "#f59e0b" },
        };
        db.ConversationLabels.AddRange(labels.Values);
        return labels;
    }

    private static readonly (string Name, ChannelType Channel)[] ContactData =
    [
        ("María Fernanda Quispe", ChannelType.WhatsApp), ("Jorge Luis Ramírez", ChannelType.WhatsApp), ("Valeria Chávez", ChannelType.Instagram),
        ("Diego Torres", ChannelType.WhatsApp), ("Camila Rojas", ChannelType.Instagram), ("Luis Alberto Mendoza", ChannelType.WhatsApp),
        ("Andrea Salazar", ChannelType.FacebookMessenger), ("Renzo Paredes", ChannelType.WhatsApp), ("Lucía Huamán", ChannelType.WhatsApp),
        ("Sebastián Vargas", ChannelType.Instagram), ("Daniela Flores", ChannelType.WhatsApp), ("Carlos Gutiérrez", ChannelType.WhatsApp),
        ("Ximena Castillo", ChannelType.Instagram), ("Miguel Ángel Rivas", ChannelType.FacebookMessenger), ("Alessandra Vega", ChannelType.WhatsApp),
        ("Fernando Cáceres", ChannelType.WhatsApp), ("Gabriela Ortiz", ChannelType.Instagram), ("José Manuel Pinto", ChannelType.WhatsApp),
        ("Rosa Elena Campos", ChannelType.WhatsApp), ("Bruno Delgado", ChannelType.FacebookMessenger), ("Natalia Herrera", ChannelType.WhatsApp),
        ("Kevin Llanos", ChannelType.WhatsApp), ("Fiorella Navarro", ChannelType.Instagram), ("Óscar Zapata", ChannelType.WhatsApp),
    ];

    private List<Contact> CreateContacts(int bid)
    {
        var contacts = ContactData.Select((c, i) =>
        {
            var phone = $"519{_rnd.Next(10_000_000, 99_999_999)}";
            var firstSeen = _now.AddDays(-_rnd.Next(5, 55)).AddHours(-_rnd.Next(0, 12));
            return new Contact
            {
                BusinessId = bid, ChannelType = c.Channel, Name = c.Name,
                ExternalId = c.Channel switch
                {
                    ChannelType.WhatsApp => phone,
                    ChannelType.Instagram => $"17841{_rnd.Next(100_000_000, 999_999_999)}{i}",
                    _ => $"{_rnd.Next(1_000_000, 9_999_999)}{_rnd.Next(10_000_000, 99_999_999)}",
                },
                Phone = c.Channel == ChannelType.WhatsApp ? phone : null,
                FirstSeenAt = firstSeen, LastSeenAt = firstSeen,
            };
        }).ToList();
        db.Contacts.AddRange(contacts);
        return contacts;
    }

    /// <summary>Guion de conversación: (entrante?, texto, enviado por bot?).</summary>
    private sealed record Line(bool In, string Text, bool Bot = true);

    private static Line[] Script(int kind, string first, string p1, string p2, string price1) => kind switch
    {
        0 => [ // compra completa
            new(true, $"Hola, ¿tienen la {p1} en talla 41?"),
            new(false, $"¡Hola {first}! 👋 Sí, la {p1} está disponible en talla 41 a {price1}. ¿La quieres para uso diario o para entrenar?"),
            new(true, "Para el día a día. ¿Cuánto sería con delivery a Miraflores?"),
            new(false, "El delivery a Miraflores cuesta S/ 10 y llega en 24 h 🚚. ¿Te la separo?"),
            new(true, "Sí, porfa. Pago con Yape"),
            new(false, "¡Perfecto! ✅ Envía el pago al 999 888 777 (Urban Step SAC) y comparte la captura. Registré tu pedido y lo validamos apenas llegue el pago."),
            new(true, "Listo, ya te envié la captura 🙌"),
            new(false, "¡Gracias por tu compra! 🙌 Validamos tu pago y te avisamos cuando tu pedido salga a reparto."),
        ],
        1 => [ // consulta de envío a provincia
            new(true, "Buenas, ¿hacen envíos a Arequipa?"),
            new(false, "¡Hola! Sí, enviamos a todo el Perú por Olva Courier en 3 a 5 días hábiles. El envío cuesta S/ 20. ¿Qué modelo te interesa? 👟"),
            new(true, $"La {p2}, talla 40"),
            new(false, $"¡Buena elección! 🔥 La {p2} en talla 40 está disponible. ¿Te paso los datos para el pago?"),
        ],
        2 => [ // negociación
            new(true, $"¿La {p1} tiene algún descuento?"),
            new(false, $"La {p1} está a {price1}. Si llevas dos pares tienes 5 % de descuento adicional 😊"),
            new(true, "Mmm está un poco caro, ¿no me la dejas a menos?"),
            new(false, "Te entiendo. Por ser tu primera compra te puedo dejar un 5 % de descuento y el delivery en Lima sale en 24 h. ¿Te la separo?"),
            new(true, "Ok, dale. La quiero"),
        ],
        3 => [ // reclamo → derivación
            new(true, "Hola, me llegó la zapatilla pero en talla 40 y yo pedí 41 😕"),
            new(false, "¡Lo siento mucho! 😔 Te comunico con un asesor de Urban Step, en breve te atiende. 🙌"),
            new(true, "Ok, quedo atento. Necesito el cambio antes del sábado"),
        ],
        4 => [ // atención humana
            new(true, "Quiero hablar con un asesor por favor"),
            new(false, "Te comunico con un asesor de Urban Step, en breve te atiende. 🙌"),
            new(false, $"Hola {first}, soy Carla del equipo de Urban Step. ¿En qué te puedo ayudar?", Bot: false),
            new(true, "Quiero comprar 8 pares para mi equipo de fútbol, ¿hay precio por mayor?"),
            new(false, "¡Claro! Desde 6 pares tienes 15 % de descuento. Te preparo una cotización con las tallas, ¿me las pasas?", Bot: false),
        ],
        5 => [ // catálogo por comando
            new(true, "#catalogo"),
            new(false, $"📋 *Catálogo de Urban Step*\n\n*1. {p1}* — {price1}\n*2. {p2}*\n\nEscribe *agregar N* para añadir un producto."),
            new(true, "agregar 1"),
            new(false, $"✅ {p1} agregado al carrito.\n\n🛒 *#carrito* para ver tu carrito\n✅ *#pedido* para confirmar"),
        ],
        _ => [ // agotado → alternativa
            new(true, "¿Tienen la Zapatilla Trail X talla 42?"),
            new(false, $"Uy, la Trail X se nos agotó 😅 pero la {p2} es muy parecida y está disponible en talla 42. ¿Te muestro?"),
            new(true, "Ya, mándame fotos"),
            new(false, $"¡Claro! Aquí la tienes 👟 La {p2} tiene suela reforzada y es súper cómoda."),
        ],
    };

    private List<Conversation> CreateConversations(Business business, List<Contact> contacts, Dictionary<string, ConversationLabel> labels,
        string agentId, List<Product> products)
    {
        var conversations = new List<Conversation>();
        var money = (decimal v) => BusinessLocale.FormatMoney(v, business.Currency);
        for (var i = 0; i < contacts.Count; i++)
        {
            var contact = contacts[i];
            // Mezcla de estados: 4 esperando asesor, 3 en atención humana, el resto bot o resueltas
            var (status, kind) = i switch
            {
                < 4 => (ConversationStatus.WaitingHuman, 3),
                < 7 => (ConversationStatus.HumanActive, 4),
                < 15 => (ConversationStatus.BotActive, new[] { 0, 1, 2, 5, 6 }[i % 5]),
                _ => (ConversationStatus.Resolved, new[] { 0, 1, 2, 6 }[i % 4]),
            };
            var p1 = products[_rnd.Next(0, 9)];
            var p2 = products[_rnd.Next(0, 9)];
            var lines = Script(kind, contact.Name!.Split(' ')[0], p1.Name, p2.Name, money(p1.Price));

            // Las conversaciones abiertas son recientes; las resueltas, de días anteriores
            var start = status == ConversationStatus.Resolved
                ? _now.AddDays(-_rnd.Next(3, 40)).AddHours(-_rnd.Next(0, 10))
                : _now.AddHours(-_rnd.Next(1, 70)).AddMinutes(-_rnd.Next(0, 59));
            if (start < contact.FirstSeenAt) contact.FirstSeenAt = start;

            var conv = new Conversation
            {
                BusinessId = business.Id, Contact = contact, ChannelType = contact.ChannelType, Status = status, CreatedAt = start,
                AssignedAgentId = status == ConversationStatus.HumanActive ? agentId : null,
                Label = i switch { 0 or 2 => labels["Reclamo"], 4 => labels["Mayorista"], 9 or 16 => labels["VIP"], 11 => labels["Seguimiento"], _ => null },
            };

            var t = start;
            foreach (var line in lines)
            {
                t = t.AddMinutes(line.In ? _rnd.Next(1, 6) : 0).AddSeconds(_rnd.Next(5, 50));
                conv.Messages.Add(new Message
                {
                    Direction = line.In ? MessageDirection.Inbound : MessageDirection.Outbound,
                    Content = line.Text, SentByBot = !line.In && line.Bot, SentAt = t,
                    IsRead = !(line.In && status == ConversationStatus.WaitingHuman && line == lines[^1]),
                    DeliveryStatus = MessageDeliveryStatus.Sent,
                    ExternalMessageId = $"demo.{business.Id}.{i}.{conv.Messages.Count}",
                });
            }

            var last = conv.Messages.Last();
            conv.UnreadCount = conv.Messages.Count(m => !m.IsRead);
            conv.LastMessageAt = last.SentAt;
            conv.UpdatedAt = last.SentAt;
            conv.LastMessagePreview = last.Content.Length > 120 ? last.Content[..117] + "..." : last.Content;
            if (status == ConversationStatus.Resolved) conv.ResolvedAt = last.SentAt.AddMinutes(30);
            if (contact.LastSeenAt < last.SentAt) contact.LastSeenAt = last.SentAt;

            conversations.Add(conv);
        }
        db.Conversations.AddRange(conversations);
        return conversations;
    }

    // ── Pedidos ──────────────────────────────────────────────────────────────

    private static readonly string[] Addresses =
    [
        "Av. Larco 345, Miraflores", "Calle Las Begonias 120, San Isidro", "Jr. Monte Rosa 255, Surco", "Av. Aviación 2810, San Borja",
        "Av. Universitaria 6540, Los Olivos", "Av. Sáenz Peña 512, Callao", "Calle Mercaderes 210, Arequipa", "Av. España 1430, Trujillo",
    ];

    private void CreateOrders(int bid, List<Contact> contacts, List<Conversation> conversations, List<Product> products)
    {
        string[] payments = ["Yape / Plin", "Yape / Plin", "Transferencia BCP", "Contraentrega"];
        var orders = new List<Order>();

        for (var n = 0; n < 46; n++)
        {
            var ageDays = n < 8 ? _rnd.NextDouble() * 2 : _rnd.NextDouble() * 45; // varios pedidos recientes
            var createdAt = _now.AddDays(-ageDays).AddHours(-_rnd.Next(0, 6));
            var contactIndex = _rnd.Next(0, contacts.Count);
            var contact = contacts[contactIndex];
            var conversation = conversations.FirstOrDefault(c => c.Contact == contact);

            var order = new Order
            {
                BusinessId = bid, Contact = contact, Conversation = n % 3 == 0 ? null : conversation, ChannelType = contact.ChannelType,
                OrderNumber = OrderWorkflow.NewOrderNumber(), PaymentMethodName = payments[_rnd.Next(payments.Length)],
                ShippingAddress = Addresses[_rnd.Next(Addresses.Length)], CreatedAt = createdAt,
                Status = OrderStatus.Pending, SubStatus = OrderSubStatus.EnValidacion,
            };

            var itemCount = _rnd.Next(1, 4);
            foreach (var product in products.Where(p => p.Stock > 0).OrderBy(_ => _rnd.Next()).Take(itemCount))
            {
                var size = product.Variants.Where(v => v.AttributeName == "Talla").OrderBy(_ => _rnd.Next()).FirstOrDefault();
                var qty = product.Category!.Name == "Accesorios" ? _rnd.Next(1, 3) : 1;
                order.Items.Add(new OrderItem
                {
                    Product = product, ProductName = product.Name, VariantDescription = size == null ? null : $"Talla: {size.AttributeValue}",
                    Quantity = qty, UnitPrice = product.Price, Subtotal = product.Price * qty,
                });
            }

            order.Subtotal = order.Items.Sum(i => i.Subtotal);
            order.ShippingCost = order.Subtotal >= 250 ? 0 : order.ShippingAddress!.Contains("Arequipa") || order.ShippingAddress.Contains("Trujillo") ? 20 : 10 + _rnd.Next(0, 3) * 2.5m;
            order.Discount = _rnd.Next(0, 5) == 0 ? Math.Round(order.Subtotal * 0.05m, 2) : 0;
            order.Total = order.Subtotal - order.Discount + order.ShippingCost;
            if (_rnd.Next(0, 6) == 0) order.Notes = "Tocar el timbre del depto. 302";

            // Etapa según antigüedad: los antiguos ya se entregaron; algunos se cancelaron
            var target = ageDays switch
            {
                < 0.6 => OrderSubStatus.EnValidacion,
                < 1.2 => _rnd.Next(0, 2) == 0 ? OrderSubStatus.EnValidacion : OrderSubStatus.EnPreparacion,
                < 2.5 => new[] { OrderSubStatus.EnPreparacion, OrderSubStatus.ListoParaEnvio, OrderSubStatus.Enviado }[_rnd.Next(3)],
                < 5 => _rnd.Next(0, 3) == 0 ? OrderSubStatus.Enviado : OrderSubStatus.Entregado,
                _ => _rnd.Next(0, 10) switch { 0 => OrderSubStatus.Cancelado, 1 => OrderSubStatus.Rechazado, < 6 => OrderSubStatus.Finalizado, _ => OrderSubStatus.Entregado },
            };
            if (target != OrderSubStatus.EnValidacion) OrderWorkflow.MoveTo(order, target);
            order.UpdatedAt = target == OrderSubStatus.EnValidacion ? createdAt : createdAt.AddHours(_rnd.Next(2, 48)) is var u && u > _now ? _now : u;
            orders.Add(order);
        }

        // Dos carritos abiertos del bot (no aparecen como pedidos)
        foreach (var conv in conversations.Where(c => c.Status == ConversationStatus.BotActive).Take(2))
        {
            var product = products[0];
            orders.Add(new Order
            {
                BusinessId = bid, Contact = conv.Contact, Conversation = conv, ChannelType = conv.ChannelType,
                OrderNumber = OrderWorkflow.NewOrderNumber(), Status = OrderStatus.Cart, SubStatus = OrderSubStatus.EnValidacion,
                Subtotal = product.Price, Total = product.Price, CreatedAt = _now.AddHours(-3), UpdatedAt = _now.AddHours(-3),
                Items = { new OrderItem { Product = product, ProductName = product.Name, Quantity = 1, UnitPrice = product.Price, Subtotal = product.Price } },
            });
        }
        db.Orders.AddRange(orders);
    }

    private async Task UpdateContactStatsAsync(int bid)
    {
        var revenue = OrderWorkflow.RevenueStatuses;
        var stats = await db.Orders.AsNoTracking()
            .Where(o => o.BusinessId == bid && revenue.Contains(o.Status))
            .GroupBy(o => o.ContactId)
            .Select(g => new { ContactId = g.Key, Count = g.Count(), Total = g.Sum(o => o.Total), Last = g.Max(o => o.CreatedAt) })
            .ToListAsync();
        foreach (var s in stats)
            await db.Contacts.Where(c => c.Id == s.ContactId).ExecuteUpdateAsync(u => u
                .SetProperty(c => c.TotalPurchases, s.Count)
                .SetProperty(c => c.TotalSpent, s.Total)
                .SetProperty(c => c.LastPurchaseAt, s.Last));
    }

    // ── Recordatorios ────────────────────────────────────────────────────────

    private void CreateReminders(int bid, List<Contact> contacts, List<Conversation> conversations, string agentId)
    {
        Reminder R(int contactIndex, string message, double hoursFromNow, ReminderStatus status) => new()
        {
            BusinessId = bid, Contact = contacts[contactIndex], ChannelType = contacts[contactIndex].ChannelType,
            ConversationId = conversations.FirstOrDefault(c => c.Contact == contacts[contactIndex])?.Id, Message = message, Status = status,
            ScheduledAt = _now.AddHours(hoursFromNow), SentAt = status == ReminderStatus.Sent ? _now.AddHours(hoursFromNow) : null,
            CreatedByAgentId = agentId, CreatedAt = _now.AddHours(Math.Min(hoursFromNow, 0) - 24),
        };
        db.Reminders.AddRange(
            R(8, "¡Hola! 👋 Tu Zapatilla Urban Blanca sigue separada. ¿Te paso los datos para el pago?", 3, ReminderStatus.Pending),
            R(9, "Hola, te escribo de Urban Step. Llegó stock nuevo de la Runner Pro en tu talla 🔥", 20, ReminderStatus.Pending),
            R(10, "¿Pudiste ver los modelos que te envié? Si quieres te ayudo a elegir la talla 😊", 48, ReminderStatus.Pending),
            R(11, "¡Gracias por tu compra! ¿Qué tal te quedaron las zapatillas? Tu opinión nos ayuda mucho 🙌", -30, ReminderStatus.Sent),
            R(12, "Hola, te recordamos que tu pedido está listo para envío. ¿Confirmas la dirección?", -52, ReminderStatus.Sent),
            R(13, "Tu carrito te espera 🛒 ¿Finalizamos tu compra?", -8, ReminderStatus.Failed),
            R(14, "Promoción del fin de semana: 2.º par al 20 % 🔥", -70, ReminderStatus.Cancelled));
    }

    private void CreateReminderTemplates(int bid)
    {
        db.ReminderTemplates.AddRange(
            new ReminderTemplate { BusinessId = bid, Segment = CustomerSegment.Frio, Order = 1, TimeWindow = "2-3h", Message = "¡Hola! 👋 Gracias por escribirnos. ¿Te puedo ayudar a encontrar tu zapatilla ideal?" },
            new ReminderTemplate { BusinessId = bid, Segment = CustomerSegment.Tibio, Order = 1, TimeWindow = "2-3h", Message = "¿Pudiste revisar los modelos que te envié? 👟 Si quieres te ayudo con la talla." },
            new ReminderTemplate { BusinessId = bid, Segment = CustomerSegment.Tibio, Order = 2, TimeWindow = "22-23h", Message = "¡Hola de nuevo! Ayer conversamos sobre unas zapatillas. ¿Te animas? Hoy el delivery en Lima llega en 24 h 🚚", IsActive = false },
            new ReminderTemplate { BusinessId = bid, Segment = CustomerSegment.Caliente, Order = 1, TimeWindow = "2-3h", Message = "¡Estás a un paso! 🔥 Tu pedido está separado. ¿Te paso los datos de Yape para cerrarlo?" },
            new ReminderTemplate { BusinessId = bid, Segment = CustomerSegment.Caliente, Order = 2, TimeWindow = "22-23h", Message = "Solo para recordarte que tu par sigue separado hasta hoy 🙌 ¿Lo confirmamos?" });
    }

    // ── Equipo ───────────────────────────────────────────────────────────────

    private async Task CreateTeamAsync(int bid, string ownerEmail)
    {
        var member = await users.FindByEmailAsync(MemberEmail);
        if (member == null)
        {
            member = new ApplicationUser { UserName = MemberEmail, Email = MemberEmail, FirstName = "Carla", LastName = "Benavides", EmailConfirmed = true };
            var result = await users.CreateAsync(member, DemoPassword);
            if (!result.Succeeded) throw new InvalidOperationException(string.Join(" ", result.Errors.Select(e => e.Description)));
            await users.AddToRoleAsync(member, AppRoles.Member);
        }

        // Si el miembro pertenece a otro negocio (o es dueño de uno) no se vincula
        var free = !await db.Businesses.AnyAsync(b => b.UserId == member.Id) && !await db.BusinessUsers.AnyAsync(u => u.UserId == member.Id);
        if (free) db.BusinessUsers.Add(new BusinessUser { BusinessId = bid, UserId = member.Id, Role = UserRole.Member, JoinedAt = _now.AddDays(-12) });

        var token = Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)))));
        db.UserInvitations.Add(new UserInvitation
        {
            BusinessId = bid, Email = "socio@urbanstep.pe", Role = UserRole.Admin, Token = token,
            Status = InvitationStatus.Pending, CreatedAt = _now.AddDays(-1), ExpiresAt = _now.AddDays(6),
        });
        logger.LogInformation("Miembro del equipo: {Email} / {Password} (propietario: {Owner})", MemberEmail, DemoPassword, ownerEmail);
    }
}
