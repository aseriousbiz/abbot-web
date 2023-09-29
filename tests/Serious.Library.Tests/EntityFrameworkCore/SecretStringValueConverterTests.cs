using Serious.Cryptography;
using Serious.EntityFrameworkCore.ValueConverters;
using Serious.TestHelpers;
using Xunit;

public class SecretStringValueConverterTests
{
    public class ThConvertFromProviderMethod
    {
        [Fact]
        public void ReturnsStoredEncryptedStringAsSecretString()
        {
            var dataProtectionProvider = new FakeDataProtectionProvider();
            var converter = new SecretStringValueConverter(dataProtectionProvider);
            var secret = new SecretString("this is a secret", dataProtectionProvider);
            var encrypted = secret.ProtectedValue;

            var result = converter.ConvertFromProvider(encrypted);

            var retrieved = Assert.IsType<SecretString>(result);
            Assert.Equal(encrypted, retrieved.ProtectedValue);
        }

        [Fact]
        public void ReturnsStoredPlainTextStringAsSecretString()
        {
            var dataProtectionProvider = new FakeDataProtectionProvider();
            var converter = new SecretStringValueConverter(dataProtectionProvider);

            var result = converter.ConvertFromProvider("this is a secret");

            var retrieved = Assert.IsType<SecretString>(result);
            Assert.Equal("this is a secret", retrieved.Reveal());
        }
    }

    public class TheConvertToProviderMethod
    {
        [Fact]
        public void ConvertsSecretToEncryptedString()
        {
            var dataProtectionProvider = new FakeDataProtectionProvider();
            var encryptedSecret = new SecretString("xoxb-the-token", dataProtectionProvider).ProtectedValue;
            // We have to populate SecretString with an already encrypted secret, otherwise it will encrypt the
            // plain text value and cache RequiresMigration to false.
            var secret = new SecretString(encryptedSecret, dataProtectionProvider);
            dataProtectionProvider.RequiresMigration = true;
            Assert.True(secret.RequiresMigration);
            var converter = new SecretStringValueConverter(dataProtectionProvider);

            var result = converter.ConvertToProvider(secret);

            var stored = Assert.IsType<string>(result);
            Assert.Equal(secret.ProtectedValue, stored);
            Assert.Equal("xoxb-the-token", new SecretString(stored, dataProtectionProvider).Reveal());
        }

        [Fact]
        public void WhenMigrateOnSaveTrueMigratesExpiredToken()
        {
            var dataProtectionProvider = new FakeDataProtectionProvider();
            var encryptedSecret = new SecretString("xoxb-the-token", dataProtectionProvider).ProtectedValue;
            // We have to populate SecretString with an already encrypted secret, otherwise it will encrypt the
            // plain text value and cache RequiresMigration to false.
            var secret = new SecretString(encryptedSecret, dataProtectionProvider);
            dataProtectionProvider.RequiresMigration = true;
            Assert.True(secret.RequiresMigration);
            var converter = new SecretStringValueConverter(dataProtectionProvider, true);

            var result = converter.ConvertToProvider(secret);

            var stored = Assert.IsType<string>(result);
            Assert.NotEqual(secret.ProtectedValue, stored);
            Assert.Equal("xoxb-the-token", new SecretString(stored, dataProtectionProvider).Reveal());
        }
    }
}
