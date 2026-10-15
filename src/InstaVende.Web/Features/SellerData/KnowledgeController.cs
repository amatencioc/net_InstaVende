using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using InstaVende.Core.Entities;
using InstaVende.Core.Enums;
using InstaVende.Infrastructure.Data;
using InstaVende.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace InstaVende.Web.Features.SellerData;

// ── DTOs ─────────────────────────────────────────────────────────────────────

/// <summary>Tema de la base de conocimiento que el vendedor IA usa para responder.</summary>
public sealed record KnowledgeEntryDto(
    int Id, string Title, string Content, KnowledgeCategory Category, bool IsFavorite, DateTime CreatedAt, DateTime UpdatedAt);

public sealed record KnowledgeCategoryCountDto(KnowledgeCategory Category, int Count);

/// <summary>Listado filtrado más los contadores por categoría (aplicando la búsqueda, sin el filtro de categoría).</summary>
public sealed record KnowledgeListDto(IReadOnlyList<KnowledgeEntryDto> Items, IReadOnlyList<KnowledgeCategoryCountDto> Counts, int Total);

public sealed record KnowledgeUpsertRequest(
    [Required(ErrorMessage = "El título es obligatorio."),
               StringLength(KnowledgeLimits.TitleMax, MinimumLength = 1, ErrorMessage = "El título debe tener entre 1 y 200 caracteres.")]
    string Title,
    [Required(ErrorMessage = "El contenido es obligatorio."),
               StringLength(KnowledgeLimits.ContentMax, MinimumLength = 1, ErrorMessage = "El contenido debe tener entre 1 y 2000 caracteres.")]
    string Content,
    [EnumDataType(typeof(KnowledgeCategory), ErrorMessage = "Selecciona una categoría válida.")]
    KnowledgeCategory Category);

public sealed record KnowledgeFavoriteRequest(bool IsFavorite);

public sealed record KnowledgeImportRowErrorDto(int Row, string? Title, string Message);

/// <summary>Resultado de una importación CSV: filas procesadas, temas creados y errores por fila (fila 1 = cabecera).</summary>
public sealed record KnowledgeImportResultDto(int TotalRows, int Created, IReadOnlyList<KnowledgeImportRowErrorDto> Errors);

public static class KnowledgeLimits
{
    public const int TitleMax = 200;
    public const int ContentMax = 2000;
    public const int ImportMaxRows = 500;
    public const long ImportMaxBytes = 1024 * 1024;
}

/// <summary>Base de conocimiento del vendedor IA (preguntas frecuentes, horarios, políticas…).</summary>
[Route("api/knowledge")]
public sealed class KnowledgeController(AppDbContext db) : TenantController
{
    /// <summary>Temas del negocio: favoritos primero y luego los editados más recientemente.</summary>
    [HttpGet]
    [ProducesResponseType<KnowledgeListDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<KnowledgeListDto>> List(
        [FromQuery] KnowledgeCategory? category, [FromQuery] string? search, CancellationToken ct)
    {
        var query = db.KnowledgeEntries.AsNoTracking().Where(k => k.BusinessId == BusinessId);

        var term = SellerDataText.Clean(search);
        if (term != null)
        {
            if (term.Length > 100) term = term[..100];
            query = query.Where(k => k.Title.Contains(term) || k.Content.Contains(term));
        }

        var counts = await query.GroupBy(k => k.Category)
            .Select(g => new KnowledgeCategoryCountDto(g.Key, g.Count()))
            .ToListAsync(ct);

        if (category is { } cat) query = query.Where(k => k.Category == cat);

        var items = await query
            .OrderByDescending(k => k.IsFavorite).ThenByDescending(k => k.UpdatedAt).ThenByDescending(k => k.Id)
            .Select(k => new KnowledgeEntryDto(k.Id, k.Title, k.Content, k.Category, k.IsFavorite, k.CreatedAt, k.UpdatedAt))
            .ToListAsync(ct);

        var ordered = counts.OrderBy(c => c.Category).ToList();
        return new KnowledgeListDto(items, ordered, ordered.Sum(c => c.Count));
    }

    [HttpPost]
    [ProducesResponseType<KnowledgeEntryDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KnowledgeEntryDto>> Create(KnowledgeUpsertRequest request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var entry = new KnowledgeEntry
        {
            BusinessId = BusinessId,
            Title = request.Title.Trim(),
            Content = request.Content.Trim(),
            Category = request.Category,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.KnowledgeEntries.Add(entry);
        await db.SaveChangesAsync(ct);
        return StatusCode(StatusCodes.Status201Created, ToDto(entry));
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType<KnowledgeEntryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<KnowledgeEntryDto>> Update(int id, KnowledgeUpsertRequest request, CancellationToken ct)
    {
        var entry = await db.KnowledgeEntries.FirstOrDefaultAsync(k => k.Id == id && k.BusinessId == BusinessId, ct);
        if (entry == null) return NotFoundProblem("El tema no existe o fue eliminado.");

        entry.Title = request.Title.Trim();
        entry.Content = request.Content.Trim();
        entry.Category = request.Category;
        entry.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToDto(entry);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        // Remove + SaveChanges (no ExecuteDelete) para que el interceptor invalide la caché del bot
        var entry = await db.KnowledgeEntries.FirstOrDefaultAsync(k => k.Id == id && k.BusinessId == BusinessId, ct);
        if (entry == null) return NotFoundProblem("El tema no existe o fue eliminado.");
        db.KnowledgeEntries.Remove(entry);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Marca o desmarca un tema como favorito (los favoritos tienen prioridad en el prompt del bot).</summary>
    [HttpPost("{id:int}/favorite")]
    [ProducesResponseType<KnowledgeEntryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<KnowledgeEntryDto>> SetFavorite(int id, KnowledgeFavoriteRequest request, CancellationToken ct)
    {
        var entry = await db.KnowledgeEntries.FirstOrDefaultAsync(k => k.Id == id && k.BusinessId == BusinessId, ct);
        if (entry == null) return NotFoundProblem("El tema no existe o fue eliminado.");

        if (entry.IsFavorite != request.IsFavorite)
        {
            entry.IsFavorite = request.IsFavorite;
            await db.SaveChangesAsync(ct);
        }
        return ToDto(entry);
    }

    /// <summary>
    /// Importa temas desde un CSV con columnas <c>titulo,contenido,categoria</c> (separador coma, punto y coma o tabulación).
    /// Máximo 500 filas. Las filas válidas se crean y las inválidas se informan con su número de fila.
    /// </summary>
    [HttpPost("import")]
    [RequestSizeLimit(KnowledgeLimits.ImportMaxBytes + 64 * 1024)]
    [ProducesResponseType<KnowledgeImportResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<KnowledgeImportResultDto>> Import(IFormFile? file, CancellationToken ct)
    {
        if (file == null || file.Length == 0) return BadRequestProblem("Selecciona un archivo CSV.");
        if (file.Length > KnowledgeLimits.ImportMaxBytes) return BadRequestProblem("El archivo supera el tamaño máximo de 1 MB.");
        if (!Path.GetExtension(file.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase)
            && !Path.GetExtension(file.FileName).Equals(".txt", StringComparison.OrdinalIgnoreCase))
            return BadRequestProblem("El archivo debe tener formato CSV.");

        string text;
        await using (var stream = file.OpenReadStream())
        using (var buffer = new MemoryStream())
        {
            await stream.CopyToAsync(buffer, ct);
            text = KnowledgeCsv.Decode(buffer.ToArray());
        }

        var records = KnowledgeCsv.Parse(text);
        if (records.Count == 0) return BadRequestProblem("El archivo está vacío.");

        var header = records[0].Fields.Select(KnowledgeCsv.NormalizeHeader).ToList();
        var titleCol = header.FindIndex(h => h is "titulo" or "title" or "pregunta" or "tema");
        var contentCol = header.FindIndex(h => h is "contenido" or "content" or "respuesta" or "descripcion");
        var categoryCol = header.FindIndex(h => h is "categoria" or "category");
        if (titleCol < 0 || contentCol < 0)
            return BadRequestProblem("La primera fila debe tener las columnas: titulo, contenido, categoria.");

        var rows = records.Skip(1).Where(r => r.Fields.Any(f => !string.IsNullOrWhiteSpace(f))).ToList();
        if (rows.Count == 0) return BadRequestProblem("El archivo no contiene filas para importar.");
        if (rows.Count > KnowledgeLimits.ImportMaxRows)
            return BadRequestProblem($"El archivo tiene {rows.Count} filas; el máximo por importación es {KnowledgeLimits.ImportMaxRows}.");

        // Evita duplicar temas al reimportar el mismo archivo (comparación sin mayúsculas ni espacios extremos)
        var existingTitles = (await db.KnowledgeEntries.AsNoTracking()
                .Where(k => k.BusinessId == BusinessId)
                .Select(k => k.Title)
                .ToListAsync(ct))
            .Select(t => t.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var errors = new List<KnowledgeImportRowErrorDto>();
        var now = DateTime.UtcNow;
        var created = 0;
        foreach (var row in rows)
        {
            var title = row.Field(titleCol)?.Trim() ?? string.Empty;
            var content = row.Field(contentCol)?.Trim() ?? string.Empty;
            var rawCategory = categoryCol >= 0 ? row.Field(categoryCol) : null;
            var shownTitle = title.Length == 0 ? null : title.Length > 80 ? title[..80] + "…" : title;

            string? error = null;
            KnowledgeCategory category = KnowledgeCategory.Otros;
            if (title.Length == 0) error = "El título está vacío.";
            else if (title.Length > KnowledgeLimits.TitleMax) error = $"El título supera los {KnowledgeLimits.TitleMax} caracteres.";
            else if (content.Length == 0) error = "El contenido está vacío.";
            else if (content.Length > KnowledgeLimits.ContentMax) error = $"El contenido supera los {KnowledgeLimits.ContentMax} caracteres.";
            else if (!KnowledgeCsv.TryParseCategory(rawCategory, out category))
                error = $"Categoría \"{rawCategory?.Trim()}\" no válida. Usa: Productos, Horarios, Envíos, Pagos, Devoluciones u Otros.";
            else if (!existingTitles.Add(title)) error = "Ya existe un tema con este título.";

            if (error != null)
            {
                errors.Add(new KnowledgeImportRowErrorDto(row.Number, shownTitle, error));
                continue;
            }

            db.KnowledgeEntries.Add(new KnowledgeEntry
            {
                BusinessId = BusinessId, Title = title, Content = content, Category = category, CreatedAt = now, UpdatedAt = now,
            });
            created++;
        }

        if (created > 0) await db.SaveChangesAsync(ct);
        return new KnowledgeImportResultDto(rows.Count, created, errors);
    }

    private static KnowledgeEntryDto ToDto(KnowledgeEntry k) =>
        new(k.Id, k.Title, k.Content, k.Category, k.IsFavorite, k.CreatedAt, k.UpdatedAt);
}

/// <summary>Lectura de CSV (RFC 4180 tolerante: comillas, saltos de línea dentro de campos, separador autodetectado).</summary>
internal static class KnowledgeCsv
{
    public sealed record Record(int Number, IReadOnlyList<string> Fields)
    {
        public string? Field(int index) => index >= 0 && index < Fields.Count ? Fields[index] : null;
    }

    /// <summary>UTF-8 (con o sin BOM); si no es UTF-8 válido se asume Latin-1 (CSV de Excel en Windows).</summary>
    public static string Decode(byte[] bytes)
    {
        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2);
        try
        {
            return new UTF8Encoding(false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(bytes);
        }
    }

    public static List<Record> Parse(string text)
    {
        var delimiter = DetectDelimiter(text);
        var records = new List<Record>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var number = 1;

        void EndField()
        {
            fields.Add(field.ToString());
            field.Clear();
        }

        void EndRecord()
        {
            EndField();
            records.Add(new Record(number++, fields.ToList()));
            fields.Clear();
        }

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else inQuotes = false;
                }
                else field.Append(c);
            }
            else if (c == '"' && field.Length == 0) inQuotes = true;
            else if (c == delimiter) EndField();
            else if (c == '\r' || c == '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                EndRecord();
            }
            else field.Append(c);
        }
        if (field.Length > 0 || fields.Count > 0) EndRecord();
        return records;
    }

    private static char DetectDelimiter(string text)
    {
        var end = text.IndexOfAny(['\r', '\n']);
        var firstLine = end < 0 ? text : text[..end];
        char[] candidates = [',', ';', '\t'];
        return candidates.OrderByDescending(d => firstLine.Count(ch => ch == d)).First();
    }

    public static string NormalizeHeader(string value) => RemoveDiacritics(value.Trim().Trim('﻿')).ToLowerInvariant();

    public static bool TryParseCategory(string? raw, out KnowledgeCategory category)
    {
        category = KnowledgeCategory.Otros;
        if (string.IsNullOrWhiteSpace(raw)) return true;

        var key = NormalizeHeader(raw);
        if (int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && Enum.IsDefined(typeof(KnowledgeCategory), n))
        {
            category = (KnowledgeCategory)n;
            return true;
        }
        KnowledgeCategory? match = key switch
        {
            "productos" or "producto" or "products" => KnowledgeCategory.Productos,
            "horarios" or "horario" or "hours" => KnowledgeCategory.Horarios,
            "envios" or "envio" or "entregas" or "entrega" or "shipping" => KnowledgeCategory.Envios,
            "pagos" or "pago" or "payments" => KnowledgeCategory.Pagos,
            "devoluciones" or "devolucion" or "cambios" or "returns" => KnowledgeCategory.Devoluciones,
            "otros" or "otro" or "general" or "other" => KnowledgeCategory.Otros,
            _ => null,
        };
        if (match is not { } found) return false;
        category = found;
        return true;
    }

    private static string RemoveDiacritics(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) sb.Append(ch);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
