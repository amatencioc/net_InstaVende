namespace InstaVende.Core.Enums;

public enum OrderSubStatus
{
    // Nuevos pedidos
    EnValidacion = 1,
    // En curso
    EnPreparacion = 2,
    ListoParaEnvio = 3,
    Enviado = 4,
    // Completados
    Entregado = 5,
    Finalizado = 6,
    // Cancelados
    Rechazado = 7,
    Cancelado = 8
}
