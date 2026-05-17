using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace InstaVende.Infrastructure.Services;

public class DataProtectionService
{
    private readonly IDataProtector _protector;
    private readonly ILogger<DataProtectionService> _logger;

    public DataProtectionService(
        IDataProtectionProvider provider,
        ILogger<DataProtectionService> logger)
    {
        _protector = provider.CreateProtector("InstaVende.ChannelTokens");
        _logger    = logger;
    }

    /// <summary>Encrypts plainText. Throws if plainText is null or empty.</summary>
    public string Encrypt(string plainText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plainText);
        return _protector.Protect(plainText);
    }

    /// <summary>Returns null on decryption failure instead of throwing.</summary>
    public string? TryDecrypt(string cipherText)
    {
        try { return _protector.Unprotect(cipherText); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "TryDecrypt failed — cipher may be from a different key ring");
            return null;
        }
    }

    /// <summary>Decrypts cipherText. Throws with context if decryption fails.</summary>
    public string Decrypt(string cipherText)
    {
        try   { return _protector.Unprotect(cipherText); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Decrypt failed — cipher may be from a different key ring or corrupt");
            throw;
        }
    }
}
