namespace InstaVende.Web.Infrastructure;

/// <summary>Roles globales de Identity (los roles dentro de un negocio están en UserRole).</summary>
public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Merchant = "Merchant";
    public const string Member = "Member";

    public static readonly string[] All = [Admin, Merchant, Member];
}
