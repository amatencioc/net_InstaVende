using System.ComponentModel.DataAnnotations;
using InstaVende.Core.Enums;
namespace InstaVende.Web.ViewModels;

public class PaymentMethodViewModel
{
    public int Id { get; set; }
    public PaymentMethodType Type { get; set; }
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(150, ErrorMessage = "Máximo 150 caracteres.")]
    public string Name { get; set; } = string.Empty;
    public string? Instructions { get; set; }
    public string? AccountAlias { get; set; }
    public string? AccountNumber { get; set; }
    public string? PaymentLink { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}
