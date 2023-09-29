using Microsoft.AspNetCore.DataProtection;
using Newtonsoft.Json;
using Serious.Cryptography;
using Serious.TestHelpers;
using Xunit;

public class SecretStringTests
{
    const int NonceLength = 8;

    public class TheConstructor
    {
        [Fact]
        public void EncryptsPlainTextValue()
        {
            var protectionProvider = new FakeDataProtectionProvider();

            var secret = new SecretString("the cake is a lie", protectionProvider);

            Assert.False(secret.RequiresMigration);
            Assert.StartsWith("serious-shhh:", secret.ProtectedValue);
            Assert.Equal(':', secret.ProtectedValue["serious-shhh:".Length + NonceLength]);
            var result = secret.Reveal();
            Assert.Equal("the cake is a lie", result);
            Assert.False(secret.RequiresMigration);
        }

        [Fact]
        public void ReturnsSecretStringWithStoredProtectedValue()
        {
            var dataProtectionProvider = new FakeDataProtectionProvider();
            var secret = new SecretString("the cake is a lie", dataProtectionProvider);
            var protectedValue = secret.ProtectedValue;

            var result = new SecretString(protectedValue, dataProtectionProvider);

            Assert.Equal(protectedValue, result.ProtectedValue);
            Assert.False(result.RequiresMigration); // As far as we can tell, the string is already protected
        }

        [Theory]
        [InlineData("this is plain text")]
        [InlineData("this is plain text: as you might guess")]
        [InlineData("serious-shhh:01234567")] // Missing encrypted part
        [InlineData("serious-shhh:01234567:")] // Missing encrypted part
        [InlineData("serious-shhh:0123456:nonce-is-too-short")]
        [InlineData("serious-shhh:0123456:nonce-is-too-long")]
        [InlineData("serious-shhh|01234567|wrong-separator")]
        [InlineData("serious-shit|01234567|wrong-prefix")]
        public void WithPlainTextEncryptsValue(string plainText)
        {
            var result = new SecretString(plainText, new FakeDataProtectionProvider());

            Assert.Equal(plainText, result.Reveal());
            Assert.NotEqual(plainText, result.ProtectedValue);
            Assert.False(result.RequiresMigration);
        }

        [Fact]
        public void HandlesEmptyString()
        {
            var result = new SecretString("", new FakeDataProtectionProvider());

            Assert.Equal("", result.ProtectedValue);
            Assert.False(result.RequiresMigration);
        }
    }

    public class TheRevealMethod
    {
        [Fact]
        public void ReturnsDecryptedString()
        {
            const string nonce = "noncebla";
            var protectorProvider = new FakeDataProtectionProvider();
            var protector = protectorProvider.CreateProtector(nonce);
            var encrypted = $"serious-shhh:{nonce}:{protector.Protect("the cake is a lie")}";
            var secret = new SecretString(encrypted, protectorProvider);

            var value = secret.Reveal();

            Assert.Equal("the cake is a lie", value);
            Assert.False(secret.RequiresMigration);
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public void ReturnsDecryptedStringAndSetsRequiresMigrationTrueWhenKeyRevokedOrKeyExpired(bool wasRevoked, bool keyExpired)
        {
            const string nonce = "noncebla";
            var protectorProvider = new FakeDataProtectionProvider();
            var protector = protectorProvider.CreateProtector(nonce);
            var encrypted = $"serious-shhh:{nonce}:{protector.Protect("the cake is a lie")}";
            var secret = new SecretString(encrypted, protectorProvider);
            protectorProvider.WasRevoked = wasRevoked;
            protectorProvider.RequiresMigration = keyExpired;

            var value = secret.Reveal();

            Assert.Equal("the cake is a lie", value);
            Assert.True(secret.RequiresMigration);
        }
    }

    public class TheMigrateMethod
    {
        [Fact]
        public void ReturnsNewSecretWithUpdatedNonce()
        {
            var protectionProvider = new FakeDataProtectionProvider();
            var secret = new SecretString("the cake is a lie", protectionProvider);
            var originalProtectedValue = secret.ProtectedValue;

            var migrated = secret.Migrate();

            Assert.NotEqual(originalProtectedValue, migrated.ProtectedValue);
            var migratedPlainText = migrated.Reveal();
            Assert.Equal(migratedPlainText, "the cake is a lie");
        }

        [Fact]
        public void ReturnsNewSecretWithUpdatedNonceWhenExistingRequiresMigration()
        {
            var protectionProvider = new FakeDataProtectionProvider();
            var secret = new SecretString("the cake is a lie", protectionProvider);
            var originalProtectedValue = secret.ProtectedValue;
            protectionProvider.RequiresMigration = true;
            secret.Reveal();

            var migrated = secret.Migrate();

            Assert.NotEqual(originalProtectedValue, migrated.ProtectedValue);
            var migratedPlainText = migrated.Reveal();
            Assert.Equal(migratedPlainText, "the cake is a lie");
        }
    }

    public class SerializationTests
    {
        [Fact]
        public void CanRoundTrip()
        {
            SecretString.Configure(new FakeDataProtectionProvider());
            var secretString = new SecretString("test");

            var json = JsonConvert.SerializeObject(secretString);

            var deserialize = JsonConvert.DeserializeObject<SecretString>(json);
            Assert.NotNull(deserialize);
            Assert.Equal(deserialize.ProtectedValue, secretString.ProtectedValue);
        }

        [Fact]
        public void CanRoundTripProperty()
        {
            SecretString.Configure(new FakeDataProtectionProvider());
            var someObject = new ObjectWithNullableSecret
            {
                Name = "bruh",
                Password = new SecretString("pwd")
            };
            var json = JsonConvert.SerializeObject(someObject);

            var deserialize = JsonConvert.DeserializeObject<ObjectWithNullableSecret>(json);
            Assert.NotNull(deserialize?.Password);
            Assert.Equal(deserialize.Password.ProtectedValue, someObject.Password.ProtectedValue);
        }

        [Fact]
        public void CanSerializeSecretString()
        {
            SecretString.Configure(new FakeDataProtectionProvider());
            var secretString = new SecretString("test");

            var json = JsonConvert.SerializeObject(secretString);

            Assert.Equal($"\"{secretString.ProtectedValue}\"", json);
        }

        [Fact]
        public void CanSerializeNullSecretString()
        {
            SecretString.Configure(new FakeDataProtectionProvider());
            var someObject = new ObjectWithNullableSecret
            {
                Name = "bruh",
                Password = null
            };
            var json = JsonConvert.SerializeObject(someObject);

            Assert.Equal("""{"Name":"bruh","Password":null}""", json);
        }

        [Theory]
        [InlineData("{\"Name\":\"bruh\" }")]
        [InlineData("{\"Name\":\"bruh\", \"Password\": null }")]
        public void CanDeserializeNullValueForNullableProperty(string serialized)
        {
            SecretString.Configure(new FakeDataProtectionProvider());

            var result = JsonConvert.DeserializeObject<ObjectWithNullableSecret>(serialized);

            Assert.NotNull(result);
            Assert.Equal("bruh", result.Name);
            Assert.Null(result.Password);
        }

        [Fact]
        public void ReturnsEmptyForEmptyStringForNullableProperty()
        {
            SecretString.Configure(new FakeDataProtectionProvider());

            var result = JsonConvert.DeserializeObject<ObjectWithNullableSecret>("{\"Name\":\"bruh\", \"Password\": \"\" }");

            Assert.NotNull(result);
            Assert.Equal("bruh", result.Name);
            Assert.Same(SecretString.EmptySecret, result.Password);
        }

        [Fact]
        public void ReturnsEmptyForEmptyStringOnNonNullableProperty()
        {
            SecretString.Configure(new FakeDataProtectionProvider());

            var result = JsonConvert.DeserializeObject<ObjectWithNonNullableSecret>("{\"Name\":\"bruh\", \"Password\": \"\" }");

            Assert.NotNull(result);
            Assert.Equal("bruh", result.Name);
            Assert.Same(SecretString.EmptySecret, result.Password);
        }

        [Theory]
        [InlineData("{\"Name\":\"bruh\" }")]
        [InlineData("{\"Name\":\"bruh\", \"Password\": null }")]
        public void CanDeserializeNullValueForNonNullableProperty(string serialized)
        {
            SecretString.Configure(new FakeDataProtectionProvider());

            var result = JsonConvert.DeserializeObject<ObjectWithNonNullableSecret>(serialized);

            Assert.NotNull(result);
            Assert.Equal("bruh", result.Name);
            // Ugh! I want this to be `SecretString.Empty`, but the JsonConverter doesn't have access to the
            // nullability of the property.
            Assert.Null(result.Password);
        }

        public class ObjectWithNullableSecret
        {
            public string? Name { get; set; }

            public SecretString? Password { get; set; }
        }

        public class ObjectWithNonNullableSecret
        {
            public string? Name { get; set; }

            public SecretString Password { get; set; } = null!;
        }
    }
}
