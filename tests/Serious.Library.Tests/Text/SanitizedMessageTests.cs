using Newtonsoft.Json;
using Serious.Cryptography;
using Serious.TestHelpers;
using Serious.Text;
using Xunit;

public class SanitizedMessageTests
{
    public class TheRestoreMethod
    {
        [Fact]
        public void ReturnsSameMessageIfNoReplacementsExist()
        {
            SecretString.Configure(new FakeDataProtectionProvider());
            var summary = "Foo Bar Baz";
            var replacements = new Dictionary<string, SecretString>();

            var restored = SanitizedMessage.Restore(new(summary), replacements);

            Assert.Equal("Foo Bar Baz", restored);
        }

        [Theory]
        [InlineData("Foo Bar Baz FooBar", "Millipedes Snakes Baz FooBar")]
        [InlineData("foo bar Baz FooBar", "Millipedes Snakes Baz FooBar")]
        [InlineData("Hey, my phone number is 555-100-1001.", "Hey, my phone number is (555) 555-5559.")]
        [InlineData("Hey, my phone number is 555-100-1001", "Hey, my phone number is (555) 555-5559")]
        [InlineData("Hey, my phone number is 555-100-1001 so you know", "Hey, my phone number is (555) 555-5559 so you know")]
        public void RestoresOriginalStringHonoringWordBoundaries(string summary, string expected)
        {
            SecretString.Configure(new FakeDataProtectionProvider());
            var replacements = new Dictionary<string, SecretString>
            {
                ["Foo"] = "Millipedes",
                ["Bar"] = "Snakes",
                ["555-100-1001"] = "(555) 555-5559",
            };

            var restored = SanitizedMessage.Restore(summary, replacements);

            Assert.Equal(expected, restored);
        }

        [Fact]
        public void CanBeRestoredFromSerializedPlainTextSanitizedMessage()
        {
            const string json = """
                {"Message":"Foo Bar Baz FooBar","Replacements":{"Foo":"Millipedes","Bar":"Snakes"}}
                """;

            var sanitizedMessage = JsonConvert.DeserializeObject<SanitizedMessage>(json);

            Assert.NotNull(sanitizedMessage);
            var restored = SanitizedMessage.Restore(sanitizedMessage.Message.Reveal(), sanitizedMessage.Replacements);
            Assert.Equal("Millipedes Snakes Baz FooBar", restored);
        }

        [Fact]
        public void CanBeRestoredFromSerializedSanitizedMessage()
        {
            SecretString.Configure(new FakeDataProtectionProvider());
            var replacements = new Dictionary<string, SecretString>
            {
                ["Foo"] = new("Millipedes"),
                ["Bar"] = new("Snakes"),
            };
            var sanitizedMessage = new SanitizedMessage(new("Foo Bar Baz FooBar"), replacements);
            var json = JsonConvert.SerializeObject(sanitizedMessage);
            var deserialized = JsonConvert.DeserializeObject<SanitizedMessage>(json);

            Assert.NotNull(deserialized);
            var restored = SanitizedMessage.Restore(deserialized.Message.Reveal(), deserialized.Replacements);
            Assert.Equal("Millipedes Snakes Baz FooBar", restored);
        }

        [Fact]
        public void ReplacesMultipleInstancesOfSameEmailWithSameReplacement()
        {
            SecretString.Configure(new FakeDataProtectionProvider());
            var replacements = new Dictionary<string, SecretString>
            {
                ["email.1@protected.ab.bot"] = new("me+you@ab.bot"),
                ["email.2@protected.ab.bot"] = new("phil@ab.bot"),
                ["email.3@protected.ab.bot"] = new("some.body@ab.bot"),
            };
            const string redacted = "email.1@protected.ab.bot is my personal email and (email.2@protected.ab.bot) is my work email and email.1@protected.ab.bot and email.3@protected.ab.bot";

            var original = SanitizedMessage.Restore(new(redacted), replacements);

            Assert.Equal(
                "me+you@ab.bot is my personal email and (phil@ab.bot) is my work email and me+you@ab.bot and some.body@ab.bot",
                original);
        }
    }
}
