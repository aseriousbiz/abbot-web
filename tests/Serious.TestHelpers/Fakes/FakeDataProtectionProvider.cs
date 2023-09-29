using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;

namespace Serious.TestHelpers;

public class FakeDataProtectionProvider : IDataProtectionProvider
{
    readonly ConcurrentDictionary<string, IDataProtector> _protectors = new();

    public bool RequiresMigration { get; set; }

    public bool WasRevoked { get; set; }

    public IDataProtector CreateProtector(string purpose) =>
        _protectors.GetOrCreate(purpose, _ => new FakePersistedDataProtector(purpose, this));

    class FakePersistedDataProtector : IPersistedDataProtector
    {
        readonly FakeDataProtectionProvider _dataProtectionProvider;

        public FakePersistedDataProtector(string purpose, FakeDataProtectionProvider dataProtectionProvider)
        {
            _purpose = Encoding.UTF8.GetBytes(purpose);
            _dataProtectionProvider = dataProtectionProvider;
        }

        readonly byte[] _purpose;

        public byte[] Protect(byte[] plaintext)
        {
            return _purpose.Concat(plaintext.Reverse()).ToArray();
        }

        public IDataProtector CreateProtector(string purpose)
        {
            return _dataProtectionProvider.CreateProtector(purpose);
        }

        public byte[] Unprotect(byte[] protectedData)
        {
            if (_dataProtectionProvider.RequiresMigration)
            {
                throw new CryptographicException("Migration required.");
            }

            if (_dataProtectionProvider.WasRevoked)
            {
                throw new CryptographicException("Revoked.");
            }

            return UnprotectBytes(protectedData);
        }

        public byte[] DangerousUnprotect(
            byte[] protectedData,
            bool ignoreRevocationErrors,
            out bool requiresMigration,
            out bool wasRevoked)
        {
            if (!ignoreRevocationErrors && (_dataProtectionProvider.RequiresMigration || _dataProtectionProvider.WasRevoked))
            {
                throw new CryptographicException();
            }
            requiresMigration = _dataProtectionProvider.RequiresMigration;
            wasRevoked = _dataProtectionProvider.WasRevoked;
            return UnprotectBytes(protectedData);
        }

        byte[] UnprotectBytes(byte[] protectedData)
        {
            var purposeBytes = protectedData.Take(_purpose.Length).ToArray();
            if (!purposeBytes.SequenceEqual(_purpose))
            {
                throw new CryptographicException("Invalid purpose");
            }

            return protectedData.Skip(_purpose.Length).Reverse().ToArray();
        }
    }
}
