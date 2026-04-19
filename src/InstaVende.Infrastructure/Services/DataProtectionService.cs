using Microsoft.AspNetCore.DataProtection;

namespace InstaVende.Infrastructure.Services;

public class DataProtectionService
{
    private readonly IDataProtector _protector;

    public DataProtectionService(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("InstaVende.ChannelTokens");
    }

    public string Encrypt(string plainText) => _protector.Protect(plainText);
    public string Decrypt(string cipherText) => _protector.Unprotect(cipherText);
}
