namespace InstaVende.Core.Enums;

/// <summary>Estado de entrega de un mensaje saliente. Los entrantes siempre son <see cref="Sent"/>.</summary>
public enum MessageDeliveryStatus { Pending = 0, Sent = 1, Failed = 2 }
