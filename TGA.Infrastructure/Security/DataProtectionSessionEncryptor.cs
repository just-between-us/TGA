using Microsoft.AspNetCore.DataProtection;
using TGA.Contract.Abstractions;

namespace TGA.Infrastructure.Security;

public class DataProtectionSessionEncryptor : ISessionEncryptor
{
    private readonly IDataProtector _protector;

    public DataProtectionSessionEncryptor(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("TelegramAssistant.SessionData.v1");
    }

    public byte[] Encrypt(byte[] raw) => _protector.Protect(raw);
    public byte[] Decrypt(byte[] encrypted) => _protector.Unprotect(encrypted);
}