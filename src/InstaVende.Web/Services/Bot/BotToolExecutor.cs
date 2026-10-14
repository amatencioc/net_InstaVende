using System.Text.Encodings.Web;
using System.Text.Json;
using InstaVende.Core.Domain;
using InstaVende.Core.Enums;
using InstaVende.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenAI.Chat;

namespace InstaVende.Web.Services.Bot;

/// <summary>Estado de una ejecución del bot compartido entre herramientas.</summary>
public sealed class ToolContext
{
    public required BotContext Bot { get; init; }
    public required int ConversationId { get; init; }
    public int? ContactId { get; init; }
    public bool IsPreview => ConversationId <= 0;

    public bool HandoffTriggered { get; set; }
    public string? HandoffSummary { get; set; }
}

/// <summary>
/// Implementación de las funciones declaradas en BotPrompts/MasterBot_Functions.json.
/// Todas filtran por el negocio y el cliente de la conversación: el modelo nunca
/// puede leer ni modificar datos de otro negocio u otro cliente.
/// </summary>
public class BotToolExecutor
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // evita ó → menos tokens
        PropertyNamingPolicy = null,
    };

    private readonly AppDbContext _db;
    private readonly CartService _cart;
    private readonly AppOptions _app;
    private readonly ILogger<BotToolExecutor> _logger;
    private readonly Dictionary<string, Func<JsonElement, ToolContext, CancellationToken, Task<object>>> _handlers;

    public BotToolExecutor(AppDbContext db, CartService cart, IOptions<AppOptions> app, ILogger<BotToolExecutor> logger)
    {
        _db = db;
        _cart = cart;
        _app = app.Value;
        _logger = logger;
        _handlers = new(StringComparer.Ordinal)
        {
            ["buscar_productos"]          = BuscarProductosAsync,
            ["obtener_detalle_producto"]  = ObtenerDetalleProductoAsync,
            ["buscar_alternativas"]       = BuscarAlternativasAsync,
            ["verificar_stock"]           = VerificarStockAsync,
            ["calcular_descuento"]        = CalcularDescuentoAsync,
            ["calcular_envio"]            = (a, c, _) => Task.FromResult(CalcularEnvio(a, c)),
            ["crear_pedido"]              = CrearPedidoAsync,
            ["rastrear_pedido"]           = RastrearPedidoAsync,
            ["obtener_historial_cliente"] = ObtenerHistorialClienteAsync,
            ["obtener_promociones"]       = (_, c, _) => Task.FromResult<object>(new { promociones = c.Bot.ActivePromotions }),
            ["transferir_a_humano"]       = (a, c, _) => Task.FromResult(TransferirAHumano(a, c)),
        };
    }

    public IReadOnlyCollection<string> ToolNames => _handlers.Keys;

    public async Task<string> ExecuteAsync(ChatToolCall call, ToolContext ctx, CancellationToken ct)
    {
        if (!_handlers.TryGetValue(call.FunctionName, out var handler))
            return Serialize(new { error = $"La función '{call.FunctionName}' no existe." });
        try
        {
            using var doc = JsonDocument.Parse(call.FunctionArguments.ToString() is { Length: > 0 } s ? s : "{}");
            var result = await handler(doc.RootElement, ctx, ct);
            return Serialize(result);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ejecutando la herramienta {Tool} (negocio {BusinessId})", call.FunctionName, ctx.Bot.BusinessId);
            return Serialize(new { error = "No se pudo completar la consulta. Intenta de nuevo o deriva a un asesor." });
        }
    }

    // ── Catálogo ─────────────────────────────────────────────────────────────

    private async Task<object> BuscarProductosAsync(JsonElement args, ToolContext ctx, CancellationToken ct)
    {
        var bid = ctx.Bot.BusinessId;
        var q = _db.Products.AsNoTracking().Where(p => p.BusinessId == bid && p.IsActive);

        var categoria = GetString(args, "categoria");
        if (categoria != null) q = q.Where(p => p.Category != null && p.Category.Name.Contains(categoria));
        if (GetDecimal(args, "precio_min") is decimal min) q = q.Where(p => p.Price >= min);
        if (GetDecimal(args, "precio_max") is decimal max) q = q.Where(p => p.Price <= max);
        if (GetBool(args, "solo_en_stock") == true) q = q.Where(p => p.Stock > 0);

        var limit = Math.Clamp(GetInt(args, "limite") ?? 5, 1, 10);
        var terms = Tokenize(GetString(args, "query"));

        // Todas las palabras deben aparecer; si no hay resultados, basta con alguna.
        var strict = terms.Aggregate(q, (acc, t) => acc.Where(p =>
            p.Name.Contains(t) || (p.Description != null && p.Description.Contains(t)) || (p.Category != null && p.Category.Name.Contains(t))));
        var results = await ProjectSearch(Order(strict, GetString(args, "orden")), limit, ctx, ct);

        if (results.Count == 0 && terms.Count > 1)
        {
            // Búsqueda flexible por el término más específico (el más largo)
            var key = terms.OrderByDescending(t => t.Length).First();
            var loose = q.Where(p => p.Name.Contains(key) || (p.Description != null && p.Description.Contains(key)));
            results = await ProjectSearch(Order(loose, GetString(args, "orden")), limit, ctx, ct);
        }

        return results.Count == 0
            ? new { resultados = Array.Empty<object>(), mensaje = "No se encontraron productos con esos criterios." }
            : new { resultados = results, total = results.Count };
    }

    private static IQueryable<Core.Entities.Product> Order(IQueryable<Core.Entities.Product> q, string? orden) => orden switch
    {
        "precio_asc"  => q.OrderBy(p => p.Price),
        "precio_desc" => q.OrderByDescending(p => p.Price),
        "recientes"   => q.OrderByDescending(p => p.CreatedAt),
        _             => q.OrderByDescending(p => p.IsFeatured).ThenBy(p => p.SortOrder).ThenBy(p => p.Name),
    };

    private async Task<List<object>> ProjectSearch(IQueryable<Core.Entities.Product> q, int limit, ToolContext ctx, CancellationToken ct)
    {
        var rows = await q.Take(limit).Select(p => new
        {
            p.Id, p.Name, p.Description, p.Price, p.PriceOriginal, p.Stock, p.ImageUrl, p.IsFeatured,
            Category = p.Category != null ? p.Category.Name : null,
            Variants = p.Variants.Count(v => v.IsActive),
        }).ToListAsync(ct);

        return rows.Select(p => (object)new
        {
            product_id      = p.Id,
            nombre          = p.Name,
            precio          = ctx.Bot.Money(p.Price),
            precio_anterior = p.PriceOriginal > p.Price ? ctx.Bot.Money(p.PriceOriginal.Value) : null,
            stock           = p.Stock,
            disponible      = p.Stock > 0,
            categoria       = p.Category,
            destacado       = p.IsFeatured,
            tiene_variantes = p.Variants > 0,
            descripcion     = Shorten(p.Description, 240),
            imagen          = _app.Absolute(p.ImageUrl),
        }).ToList();
    }

    private async Task<object> ObtenerDetalleProductoAsync(JsonElement args, ToolContext ctx, CancellationToken ct)
    {
        if (GetInt(args, "product_id") is not int id) return new { error = "product_id es obligatorio." };
        var p = await _db.Products.AsNoTracking()
            .Where(x => x.Id == id && x.BusinessId == ctx.Bot.BusinessId && x.IsActive)
            .Select(x => new
            {
                x.Id, x.Name, x.Description, x.Price, x.PriceOriginal, x.Stock, x.Sku, x.ImageUrl,
                Category = x.Category != null ? x.Category.Name : null,
                Variants = x.Variants.Where(v => v.IsActive).Select(v => new { v.Id, v.AttributeName, v.AttributeValue, v.PriceModifier, v.Stock }).ToList(),
                Images   = x.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).ToList(),
            })
            .FirstOrDefaultAsync(ct);
        if (p == null) return new { error = "Producto no encontrado." };

        return new
        {
            product_id      = p.Id,
            nombre          = p.Name,
            descripcion     = p.Description,
            precio          = ctx.Bot.Money(p.Price),
            precio_anterior = p.PriceOriginal > p.Price ? ctx.Bot.Money(p.PriceOriginal.Value) : null,
            stock           = p.Stock,
            disponible      = p.Stock > 0,
            sku             = p.Sku,
            categoria       = p.Category,
            variantes       = p.Variants.Select(v => new
            {
                variante_id = v.Id, atributo = v.AttributeName, valor = v.AttributeValue,
                precio = ctx.Bot.Money(p.Price + v.PriceModifier), stock = v.Stock, disponible = v.Stock > 0,
            }),
            imagenes = new[] { p.ImageUrl }.Concat(p.Images).Select(_app.Absolute).Where(u => u != null).Distinct(),
        };
    }

    private async Task<object> BuscarAlternativasAsync(JsonElement args, ToolContext ctx, CancellationToken ct)
    {
        if (GetInt(args, "product_id") is not int id) return new { error = "product_id es obligatorio." };
        var bid = ctx.Bot.BusinessId;
        var original = await _db.Products.AsNoTracking()
            .Where(p => p.Id == id && p.BusinessId == bid)
            .Select(p => new { p.CategoryId, p.Price })
            .FirstOrDefaultAsync(ct);
        if (original == null) return new { error = "Producto no encontrado." };

        var max = Math.Clamp(GetInt(args, "max_resultados") ?? 3, 1, 5);
        var q = _db.Products.AsNoTracking()
            .Where(p => p.BusinessId == bid && p.IsActive && p.Id != id && p.Stock > 0);
        if (original.CategoryId != null) q = q.Where(p => p.CategoryId == original.CategoryId);

        var alternatives = await q
            .OrderBy(p => Math.Abs(p.Price - original.Price))
            .ThenByDescending(p => p.IsFeatured)
            .Take(max)
            .Select(p => new { p.Id, p.Name, p.Price, p.Stock, p.ImageUrl })
            .ToListAsync(ct);

        return new
        {
            alternativas = alternatives.Select(a => new
            {
                product_id = a.Id, nombre = a.Name, precio = ctx.Bot.Money(a.Price), stock = a.Stock, imagen = _app.Absolute(a.ImageUrl)
            }),
            total = alternatives.Count,
        };
    }

    private async Task<object> VerificarStockAsync(JsonElement args, ToolContext ctx, CancellationToken ct)
    {
        if (GetInt(args, "product_id") is not int id) return new { error = "product_id es obligatorio." };
        var p = await _db.Products.AsNoTracking()
            .Where(x => x.Id == id && x.BusinessId == ctx.Bot.BusinessId && x.IsActive)
            .Select(x => new { x.Name, x.Stock, Variants = x.Variants.Where(v => v.IsActive).Select(v => new { v.Id, v.AttributeName, v.AttributeValue, v.Stock }).ToList() })
            .FirstOrDefaultAsync(ct);
        if (p == null) return new { error = "Producto no encontrado." };

        if (GetInt(args, "variante_id") is int vid)
        {
            var v = p.Variants.FirstOrDefault(x => x.Id == vid);
            if (v == null) return new { error = "Variante no encontrada." };
            return new { nombre = p.Name, variante = $"{v.AttributeName}: {v.AttributeValue}", stock = v.Stock, disponible = v.Stock > 0, pocas_unidades = v.Stock is > 0 and < 5 };
        }

        return new
        {
            nombre = p.Name, stock = p.Stock, disponible = p.Stock > 0, pocas_unidades = p.Stock is > 0 and < 5,
            variantes = p.Variants.Select(v => new { variante_id = v.Id, descripcion = $"{v.AttributeName}: {v.AttributeValue}", stock = v.Stock }),
        };
    }

    // ── Negociación y envío ──────────────────────────────────────────────────

    private async Task<object> CalcularDescuentoAsync(JsonElement args, ToolContext ctx, CancellationToken ct)
    {
        var rules = ctx.Bot.Negotiation;
        var precio = GetDecimal(args, "precio_base");
        if (precio is null or <= 0) return new { error = "precio_base debe ser mayor que 0." };

        var nivel = Math.Clamp(GetInt(args, "nivel_negociacion") ?? 1, 1, 3);
        var pct = nivel switch { 1 => rules.Level1, 2 => rules.Level2, _ => rules.Level3 };

        // La fidelidad la decide el servidor, no el modelo
        var esRecurrente = ctx.ContactId is int cid &&
            await _db.Contacts.AsNoTracking().AnyAsync(c => c.Id == cid && c.TotalPurchases > 0, ct);
        if (esRecurrente) pct += rules.LoyaltyDiscount;
        if ((GetInt(args, "cantidad_productos") ?? 1) >= 2) pct += rules.BundleDiscount;

        pct = Math.Clamp(pct, 0, rules.MaxDiscountPercent);
        var final = Math.Round(precio.Value * (1 - pct / 100m), 2);
        return new
        {
            descuento_porcentaje = pct,
            precio_final         = ctx.Bot.Money(final),
            ahorro               = ctx.Bot.Money(precio.Value - final),
            es_maximo            = pct >= rules.MaxDiscountPercent,
        };
    }

    private static object CalcularEnvio(JsonElement args, ToolContext ctx)
    {
        var zonaTexto = GetString(args, "zona");
        if (zonaTexto == null) return new { error = "Indica la zona o distrito de entrega." };
        if (ctx.Bot.DeliveryZones.Count == 0)
            return new { disponible = false, mensaje = "No hay zonas de entrega configuradas; un asesor confirmará el envío." };

        var norm = BotCommandRouter.Normalize(zonaTexto);
        var zona = ctx.Bot.DeliveryZones.FirstOrDefault(z => BotCommandRouter.Normalize(z.Name) == norm)
                ?? ctx.Bot.DeliveryZones.FirstOrDefault(z => BotCommandRouter.Normalize(z.Name).Contains(norm) || norm.Contains(BotCommandRouter.Normalize(z.Name)));
        if (zona == null)
            return new { disponible = false, zonas_disponibles = ctx.Bot.DeliveryZones.Select(z => z.Name), mensaje = "La zona indicada no está entre las zonas de entrega configuradas." };

        var subtotal = GetDecimal(args, "subtotal") ?? 0;
        var threshold = ctx.Bot.Negotiation.FreeShippingThreshold;
        var gratis = threshold > 0 && subtotal >= threshold;
        return new
        {
            disponible = true,
            zona = zona.Name,
            costo = zona.Cost is null ? "a coordinar con un asesor" : ctx.Bot.Money(gratis ? 0 : zona.Cost.Value),
            envio_gratis = gratis,
            falta_para_envio_gratis = threshold > 0 && !gratis && subtotal > 0 ? ctx.Bot.Money(threshold - subtotal) : null,
            detalle = zona.Description,
        };
    }

    // ── Pedidos ──────────────────────────────────────────────────────────────

    private async Task<object> CrearPedidoAsync(JsonElement args, ToolContext ctx, CancellationToken ct)
    {
        if (ctx.IsPreview)
            return new { simulado = true, mensaje = "Vista previa: en una conversación real aquí se registraría el pedido y se devolvería su número." };

        if (args.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array && items.GetArrayLength() > 0)
        {
            await _cart.ClearAsync(ctx.Bot.BusinessId, ctx.ConversationId, ct);
            foreach (var item in items.EnumerateArray())
            {
                if (GetInt(item, "product_id") is not int pid) return new { error = "Cada item necesita product_id." };
                var r = await _cart.AddItemAsync(ctx.Bot, ctx.ConversationId, pid, GetInt(item, "cantidad") ?? 1, GetInt(item, "variante_id"), ct);
                if (!r.Success) return new { error = r.Message };
            }
        }

        var result = await _cart.ConfirmAsync(ctx.Bot, ctx.ConversationId,
            GetString(args, "direccion_envio"), GetString(args, "metodo_pago"), GetString(args, "zona_envio"), GetString(args, "notas"), ct);
        if (!result.Success || result.Order == null) return new { error = result.Message };

        var o = result.Order;
        return new
        {
            numero_pedido = o.OrderNumber,
            estado        = OrderWorkflow.Label(o.SubStatus),
            subtotal      = ctx.Bot.Money(o.Subtotal),
            envio         = ctx.Bot.Money(o.ShippingCost),
            total         = ctx.Bot.Money(o.Total),
            productos     = o.Items.Select(i => $"{i.ProductName} × {i.Quantity}"),
            mensaje_confirmacion = ctx.Bot.PurchaseConfirmationMessage,
        };
    }

    private async Task<object> RastrearPedidoAsync(JsonElement args, ToolContext ctx, CancellationToken ct)
    {
        if (ctx.ContactId is not int cid) return new { pedidos = Array.Empty<object>(), mensaje = "No hay pedidos asociados a este cliente." };
        var numero = GetString(args, "numero_pedido");

        var q = _db.Orders.AsNoTracking()
            .Where(o => o.BusinessId == ctx.Bot.BusinessId && o.ContactId == cid && o.Status != OrderStatus.Cart);
        if (numero != null) q = q.Where(o => o.OrderNumber == numero);

        var orders = await q.OrderByDescending(o => o.CreatedAt).Take(3)
            .Select(o => new { o.OrderNumber, o.SubStatus, o.Total, o.CreatedAt, Items = o.Items.Select(i => i.ProductName + " × " + i.Quantity).ToList() })
            .ToListAsync(ct);

        if (orders.Count == 0)
            return new { pedidos = Array.Empty<object>(), mensaje = numero != null ? "No se encontró ese número de pedido para este cliente." : "El cliente no tiene pedidos registrados." };

        return new
        {
            pedidos = orders.Select(o => new
            {
                numero_pedido = o.OrderNumber,
                estado        = OrderWorkflow.Label(o.SubStatus),
                total         = ctx.Bot.Money(o.Total),
                fecha         = BusinessLocale.ToLocal(o.CreatedAt, ctx.Bot.Country).ToString("dd/MM/yyyy"),
                productos     = o.Items,
            }),
        };
    }

    private async Task<object> ObtenerHistorialClienteAsync(JsonElement args, ToolContext ctx, CancellationToken ct)
    {
        if (ctx.ContactId is not int cid) return new { mensaje = "Cliente sin historial (vista previa o cliente nuevo)." };
        var c = await _db.Contacts.AsNoTracking()
            .Where(x => x.Id == cid && x.BusinessId == ctx.Bot.BusinessId)
            .Select(x => new
            {
                x.FirstSeenAt, x.TotalPurchases, x.TotalSpent, x.LastPurchaseAt,
                Productos = x.Orders.Where(o => o.Status != OrderStatus.Cart && o.Status != OrderStatus.Cancelled)
                    .OrderByDescending(o => o.CreatedAt).SelectMany(o => o.Items.Select(i => i.ProductName)).Take(10).ToList(),
            })
            .FirstOrDefaultAsync(ct);
        if (c == null) return new { mensaje = "Cliente sin historial." };

        return new
        {
            cliente_desde       = BusinessLocale.ToLocal(c.FirstSeenAt, ctx.Bot.Country).ToString("dd/MM/yyyy"),
            compras             = c.TotalPurchases,
            total_gastado       = ctx.Bot.Money(c.TotalSpent),
            ultima_compra       = c.LastPurchaseAt is DateTime d ? BusinessLocale.ToLocal(d, ctx.Bot.Country).ToString("dd/MM/yyyy") : null,
            productos_comprados = c.Productos.Distinct(),
        };
    }

    private static object TransferirAHumano(JsonElement args, ToolContext ctx)
    {
        if (!ctx.Bot.EnableHandoff)
            return new { transferido = false, mensaje = "La derivación a asesores está desactivada; continúa ayudando al cliente." };
        ctx.HandoffTriggered = true;
        ctx.HandoffSummary = Shorten($"[{GetString(args, "prioridad") ?? "media"}] {GetString(args, "motivo") ?? "otro"}: {GetString(args, "resumen")}", 500);
        return new { transferido = true, mensaje = "Conversación transferida a un asesor humano." };
    }

    // ── Utilidades ───────────────────────────────────────────────────────────

    private static List<string> Tokenize(string? query)
        => string.IsNullOrWhiteSpace(query)
            ? []
            : query.Split([' ', ',', '.', ';', '/', '-'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                   .Where(t => t.Length >= 3).Select(t => t.ToLowerInvariant()).Distinct().Take(6).ToList();

    private static string? GetString(JsonElement el, string key)
        => el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString())
            ? v.GetString()!.Trim() : null;

    private static int? GetInt(JsonElement el, string key)
    {
        if (!el.TryGetProperty(key, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i)) return i;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d)) return (int)d;
        return v.ValueKind == JsonValueKind.String && int.TryParse(v.GetString(), out var s) ? s : null;
    }

    private static decimal? GetDecimal(JsonElement el, string key)
    {
        if (!el.TryGetProperty(key, out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d)) return d;
        return v.ValueKind == JsonValueKind.String && decimal.TryParse(v.GetString(), System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var s) ? s : null;
    }

    private static bool? GetBool(JsonElement el, string key)
        => el.TryGetProperty(key, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False ? v.GetBoolean() : null;

    private static string? Shorten(string? text, int max)
        => string.IsNullOrWhiteSpace(text) ? null : text.Length <= max ? text : text[..max] + "…";

    private static string Serialize(object value) => JsonSerializer.Serialize(value, JsonOpts);
}
