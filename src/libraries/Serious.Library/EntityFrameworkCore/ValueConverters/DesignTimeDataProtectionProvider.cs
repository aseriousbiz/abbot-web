using Microsoft.AspNetCore.DataProtection;

namespace Serious.EntityFrameworkCore.ValueConverters;

public class DesignTimeDataProtectionProvider : IDataProtectionProvider
{
    public IDataProtector CreateProtector(string purpose)
    {
        return new DesignTimeDataProtector();
    }

    class DesignTimeDataProtector : IDataProtector
    {
        public IDataProtector CreateProtector(string purpose)
        {
            return this;
        }

        public byte[] Protect(byte[] plaintext)
        {
            return plaintext;
        }

        public byte[] Unprotect(byte[] protectedData)
        {
            return protectedData;
        }
    }
}
