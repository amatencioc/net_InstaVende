namespace InstaVende.Core.Enums;
public enum PaymentMethodType
{
    BankTransfer = 1,
    CreditCard = 2,
    Cash = 3,
    MercadoPago = 4,
    PayPal = 5,
    Stripe = 6,
    /// <summary>Billeteras digitales locales (Yape, Plin, Nequi…).</summary>
    DigitalWallet = 7,
    Other = 99
}
