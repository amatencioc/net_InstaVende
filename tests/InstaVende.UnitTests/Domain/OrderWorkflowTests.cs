using InstaVende.Core.Domain;
using InstaVende.Core.Entities;
using InstaVende.Core.Enums;

namespace InstaVende.UnitTests.Domain;

public class OrderWorkflowTests
{
    [Theory]
    [InlineData(OrderSubStatus.EnValidacion, OrderStatus.Pending)]
    [InlineData(OrderSubStatus.EnPreparacion, OrderStatus.Preparing)]
    [InlineData(OrderSubStatus.ListoParaEnvio, OrderStatus.Preparing)]
    [InlineData(OrderSubStatus.Enviado, OrderStatus.Shipped)]
    [InlineData(OrderSubStatus.Entregado, OrderStatus.Delivered)]
    [InlineData(OrderSubStatus.Finalizado, OrderStatus.Delivered)]
    [InlineData(OrderSubStatus.Rechazado, OrderStatus.Cancelled)]
    [InlineData(OrderSubStatus.Cancelado, OrderStatus.Cancelled)]
    public void StatusFor_DerivaElEstadoAgregado(OrderSubStatus sub, OrderStatus expected)
        => Assert.Equal(expected, OrderWorkflow.StatusFor(sub));

    [Fact]
    public void TodoSubEstado_PerteneceAUnaColumnaDeSuPestana()
    {
        foreach (var sub in Enum.GetValues<OrderSubStatus>())
            Assert.Contains(sub, OrderWorkflow.ColumnsFor(OrderWorkflow.TabFor(sub)));
    }

    [Fact]
    public void MoveTo_ActualizaSubEstadoEstadoYFecha()
    {
        var order = new Order { Status = OrderStatus.Pending, SubStatus = OrderSubStatus.EnValidacion, UpdatedAt = DateTime.MinValue };

        OrderWorkflow.MoveTo(order, OrderSubStatus.Enviado);

        Assert.Equal(OrderSubStatus.Enviado, order.SubStatus);
        Assert.Equal(OrderStatus.Shipped, order.Status);
        Assert.True(order.UpdatedAt > DateTime.MinValue);
    }

    [Fact]
    public void UnCarrito_NoSePuedeMoverNiCuentaComoIngreso()
    {
        var cart = new Order { Status = OrderStatus.Cart };

        Assert.False(OrderWorkflow.CanMove(cart, OrderSubStatus.Enviado));
        Assert.Throws<InvalidOperationException>(() => OrderWorkflow.MoveTo(cart, OrderSubStatus.Enviado));
        Assert.DoesNotContain(OrderStatus.Cart, OrderWorkflow.RevenueStatuses);
        Assert.DoesNotContain(OrderStatus.Cart, OrderWorkflow.OrderStatuses);
    }

    [Fact]
    public void ConfirmCart_ConvierteElCarritoEnPedidoEnValidacion()
    {
        var cart = new Order { Status = OrderStatus.Cart };

        OrderWorkflow.ConfirmCart(cart);

        Assert.Equal(OrderStatus.Pending, cart.Status);
        Assert.Equal(OrderSubStatus.EnValidacion, cart.SubStatus);
    }

    [Fact]
    public void LosCanceladosYReembolsados_NoSumanIngresos()
    {
        Assert.DoesNotContain(OrderStatus.Cancelled, OrderWorkflow.RevenueStatuses);
        Assert.DoesNotContain(OrderStatus.Refunded, OrderWorkflow.RevenueStatuses);
    }

    [Fact]
    public void NewOrderNumber_TieneFormatoLegibleYEsUnico()
    {
        var numbers = Enumerable.Range(0, 500).Select(_ => OrderWorkflow.NewOrderNumber()).ToList();

        Assert.All(numbers, n => Assert.Matches(@"^IV-\d{6}-[2-9A-HJ-NP-Z]{6}$", n));
        Assert.Equal(numbers.Count, numbers.Distinct().Count());
    }
}
