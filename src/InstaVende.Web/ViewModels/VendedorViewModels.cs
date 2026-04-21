using System.ComponentModel.DataAnnotations;
using InstaVende.Core.Enums;

namespace InstaVende.Web.ViewModels;

// Personalidad
public class VendorPersonalidadViewModel
{
    public int Id { get; set; }
    [Required, MaxLength(100)]
    public string VendorName { get; set; } = string.Empty;
    public string? VendorGender { get; set; }
    [MaxLength(200)]
    public string? BusinessName { get; set; }
    public string? Country { get; set; }
    [MaxLength(2000)]
    public string? BusinessDescription { get; set; }
    [MaxLength(500)]
    public string? TargetAudience { get; set; }
    [MaxLength(2000)]
    public string? Rules { get; set; }
    [MaxLength(200)]
    public string? CommunicationStyle { get; set; }
    [MaxLength(210)]
    public string? SalesStyle { get; set; }
    public int ResponseLength { get; set; } = 3;
    public bool UseEmojis { get; set; } = true;
    public bool UseOpeningPunctuation { get; set; } = false;
    public string? WordsToAvoid { get; set; }
    public string? EmojiPalette { get; set; }
    [MaxLength(500)]
    public string? WelcomeMessage { get; set; }
    public string? WelcomeMediaUrl { get; set; }
    [MaxLength(500)]
    public string? PurchaseConfirmationMessage { get; set; }
    public string? HumanHandoffSituations { get; set; }
    public bool AutoPauseOnHandoff { get; set; } = true;
    [MaxLength(500)]
    public string? HandoffExampleMessage { get; set; }
}

// Base de conocimiento
public class KnowledgeEntryViewModel
{
    public int Id { get; set; }
    [Required, MaxLength(300)]
    public string Title { get; set; } = string.Empty;
    [Required]
    public string Content { get; set; } = string.Empty;
    public KnowledgeCategory Category { get; set; } = KnowledgeCategory.Otros;
    public bool IsFavorite { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class BaseConocimientoViewModel
{
    public List<KnowledgeEntryViewModel> Entries { get; set; } = new();
    public KnowledgeCategory? FilterCategory { get; set; }
}

// Entrega
public class DeliveryZoneViewModel
{
    public int Id { get; set; }
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;
    public decimal? Cost { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

// Pagos del vendedor
public class VendedorPagoViewModel
{
    public List<VendedorPaymentMethodViewModel> PaymentMethods { get; set; } = new();
    public List<PaymentImageViewModel> PaymentImages { get; set; } = new();
}

public class VendedorPaymentMethodViewModel
{
    public int Id { get; set; }
    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Instructions { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public class PaymentImageViewModel
{
    public int Id { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
