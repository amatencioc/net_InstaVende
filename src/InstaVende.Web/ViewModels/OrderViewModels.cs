using System.ComponentModel.DataAnnotations;
using InstaVende.Core.Enums;
namespace InstaVende.Web.ViewModels;

public class OrderViewModel
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public OrderStatus Status { get; set; }
    public OrderSubStatus SubStatus { get; set; }
    public string StatusLabel => Status switch
    {
        OrderStatus.Pending    => "Pendiente",
        OrderStatus.Confirmed  => "Confirmado",
        OrderStatus.Preparing  => "En preparación",
        OrderStatus.Shipped    => "Enviado",
        OrderStatus.Delivered  => "Entregado",
        OrderStatus.Cancelled  => "Cancelado",
        OrderStatus.Refunded   => "Reembolsado",
        _ => "Desconocido"
    };
    public string SubStatusLabel => SubStatus switch
    {
        OrderSubStatus.EnValidacion   => "En validación",
        OrderSubStatus.EnPreparacion  => "En preparación",
        OrderSubStatus.ListoParaEnvio => "Listo para envío",
        OrderSubStatus.Enviado        => "Enviado",
        OrderSubStatus.Entregado      => "Entregado",
        OrderSubStatus.Finalizado     => "Finalizado",
        OrderSubStatus.Rechazado      => "Rechazado",
        OrderSubStatus.Cancelado      => "Cancelado",
        _ => "Desconocido"
    };
    public string SubStatusBadgeCss => SubStatus switch
    {
        OrderSubStatus.EnValidacion   => "substatus-purple",
        OrderSubStatus.EnPreparacion  => "substatus-blue",
        OrderSubStatus.ListoParaEnvio => "substatus-orange",
        OrderSubStatus.Enviado        => "substatus-teal",
        OrderSubStatus.Entregado      => "substatus-green",
        OrderSubStatus.Finalizado     => "substatus-green",
        OrderSubStatus.Rechazado      => "substatus-red",
        OrderSubStatus.Cancelado      => "substatus-red",
        _ => "substatus-gray"
    };
    public string TabGroup => SubStatus switch
    {
        OrderSubStatus.EnValidacion                                              => "nuevos",
        OrderSubStatus.EnPreparacion or OrderSubStatus.ListoParaEnvio or
        OrderSubStatus.Enviado                                                   => "encurso",
        OrderSubStatus.Entregado or OrderSubStatus.Finalizado                    => "completados",
        OrderSubStatus.Rechazado or OrderSubStatus.Cancelado                     => "cancelados",
        _                                                                        => "nuevos"
    };
    public string StatusCss => Status switch
    {
        OrderStatus.Pending    => "warning",
        OrderStatus.Confirmed  => "primary",
        OrderStatus.Preparing  => "info",
        OrderStatus.Shipped    => "primary",
        OrderStatus.Delivered  => "success",
        OrderStatus.Cancelled  => "danger",
        OrderStatus.Refunded   => "secondary",
        _ => "secondary"
    };
    public int ContactId { get; set; }
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public int? ConversationId { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Discount { get; set; }
    public decimal ShippingCost { get; set; }
    public decimal Total { get; set; }
    public string? PaymentMethodName { get; set; }
    public string? ShippingAddress { get; set; }
    public string? Notes { get; set; }
    public ChannelType ChannelType { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<OrderItemViewModel> Items { get; set; } = new();
}

public class OrderItemViewModel
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string? VariantDescription { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }
}

public class CreateOrderViewModel
{
    public int ContactId { get; set; }
    public int? ConversationId { get; set; }
    public string? PaymentMethodName { get; set; }
    public string? ShippingAddress { get; set; }
    public string? Notes { get; set; }
    public ChannelType ChannelType { get; set; }
    public List<CreateOrderItemViewModel> Items { get; set; } = new();
}

public class CreateOrderItemViewModel
{
    public int ProductId { get; set; }
    public string? VariantDescription { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
