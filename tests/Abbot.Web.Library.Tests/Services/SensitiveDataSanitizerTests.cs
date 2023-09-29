using Azure.AI.TextAnalytics;
using Serious.Abbot.Services;
using Serious.Cryptography;
using Serious.TestHelpers;

public class SensitiveDataSanitizerTests
{
    public class TheScanEmailsMethod
    {
        public TheScanEmailsMethod()
        {
            SecretString.Configure(new FakeDataProtectionProvider());
        }

        [Fact]
        public void ReturnsEmailsWithOffsets()
        {
            const string email1 = "me+you@ab.bot";
            const string email2 = "phil.haack@ab.bot";
            const string original = $"Hey, {email1} is my personal email and ({email2}) is my work email";

            var sensitiveValues = SensitiveDataSanitizer.ScanEmails(original);

            int expectedEmail1Offset = original.IndexOf(email1, StringComparison.Ordinal);
            int expectedEmail2Offset = original.IndexOf(email2, StringComparison.Ordinal);
            Assert.Collection(sensitiveValues,
                v => Assert.Equal((expectedEmail1Offset, email1.Length, email1), (v.Offset, v.Length, v.Text)),
                v => Assert.Equal((expectedEmail2Offset, email2.Length, email2), (v.Offset, v.Length, v.Text)));
        }

        [Theory]
        [InlineData("{0}+{1} are two email addresses.")]
        [InlineData("~~{0}@~~ [{1}] is a nice way to format emails.")]
        public void FindsEmailsInWeirdlyFormattedMessages(string template)
        {
            const string email1 = "foo+biz@bar.com";
            const string email2 = "biz.baz@ab.bb.bot";
            var original = string.Format(template, email1, email2);

            var sensitiveValues = SensitiveDataSanitizer.ScanEmails(original);

            int expectedEmail1Offset = original.IndexOf(email1, StringComparison.Ordinal);
            int expectedEmail2Offset = original.IndexOf(email2, StringComparison.Ordinal);
            Assert.Collection(sensitiveValues,
                v => Assert.Equal((expectedEmail1Offset, email1.Length, email1), (v.Offset, v.Length, v.Text)),
                v => Assert.Equal((expectedEmail2Offset, email2.Length, email2), (v.Offset, v.Length, v.Text)));
        }

        [Fact]
        public void FindsSameEmailMultipleTimesWithoutProblem()
        {
            const string email1 = "me+you@ab.bot";
            const string email2 = "you+me@ab.bot";
            var original = $"{email1} {email2} and {email1}";

            var sensitiveValues = SensitiveDataSanitizer.ScanEmails(original);

            int expectedEmail1Offset = original.IndexOf(email1, StringComparison.Ordinal);
            int expectedEmail2Offset = original.IndexOf(email2, StringComparison.Ordinal);
            int expectedEmail1SecondOffset = original.LastIndexOf(email1, StringComparison.Ordinal);
            Assert.Collection(sensitiveValues,
                v => Assert.Equal((expectedEmail1Offset, email1.Length, email1), (v.Offset, v.Length, v.Text)),
                v => Assert.Equal((expectedEmail2Offset, email2.Length, email2), (v.Offset, v.Length, v.Text)),
                v => Assert.Equal((expectedEmail1SecondOffset, email1.Length, email1), (v.Offset, v.Length, v.Text)));
        }

        [Theory]
        [InlineData("Hey, foo is my work email")]
        [InlineData("Hey, (foo+bar@bar.) is not an email")]
        public void DoesNotFindNonEmailAddresses(string original)
        {
            var sensitiveValues = SensitiveDataSanitizer.ScanEmails(original);

            Assert.Empty(sensitiveValues);
        }
    }

    public class TheSanitizeMethod
    {
        public TheSanitizeMethod()
        {
            SecretString.Configure(new FakeDataProtectionProvider());
        }

        [Fact]
        public void ReturnsOriginalStringWhenSensitiveValuesEmpty()
        {
            var result = SensitiveDataSanitizer.Sanitize("Hey, this is just some random text", Array.Empty<SensitiveValue>());

            Assert.Equal("Hey, this is just some random text", result.Message.Reveal());
        }

        [Fact]
        public void WithEmailsReplacesThemWithLookOurFakeEmails()
        {
            const string email1 = "me+you@ab.bot";
            const string email2 = "phil.haack@ab.bot";
            var sensitiveValues = new[]
            {
                new SensitiveValue(email1, PiiEntityCategory.Email, null, 0.9, 0, email1.Length),
                new SensitiveValue(email2, PiiEntityCategory.Email, null, 0.9, email1.Length + 1, email2.Length)
            };

            var result = SensitiveDataSanitizer.Sanitize($"{email1} {email2}", sensitiveValues);

            Assert.Equal("email.1@protected.ab.bot email.2@protected.ab.bot", result.Message.Reveal());
        }

        [Fact]
        public void WithPhoneNumbersReplacesThem()
        {
            const string phoneNumber1 = "+1 425-121-1234";
            const string phoneNumber2 = "310-866-1001";
            var sensitiveValues = new[]
            {
                new SensitiveValue(phoneNumber1, PiiEntityCategory.PhoneNumber, null, 0.9, 19, phoneNumber1.Length),
                new SensitiveValue(phoneNumber2, PiiEntityCategory.PhoneNumber, null, 0.9, 39, phoneNumber2.Length)
            };

            var result = SensitiveDataSanitizer.Sanitize($"My phone number is {phoneNumber1} and {phoneNumber2}.", sensitiveValues);

            Assert.Equal("My phone number is 555-100-0001 and 555-100-0002.", result.Message.Reveal());
        }

        [Fact]
        public void ReplacesCreditCardNumbersWithHashes()
        {
            const string ccNumber1 = "2222 4053 4324 8877";
            const string ccNumber2 = "2222 4053 4324 8822";
            var sensitiveValues = new[]
            {
                new SensitiveValue(ccNumber1, PiiEntityCategory.CreditCardNumber, null, 0.9, 16, ccNumber1.Length),
                new SensitiveValue(ccNumber2, PiiEntityCategory.CreditCardNumber, null, 0.9, 40, ccNumber2.Length)
            };

            var result = SensitiveDataSanitizer.Sanitize($"My cc number is {ccNumber1} and {ccNumber2}.", sensitiveValues);

            Assert.Equal("My cc number is 1111 1111 1111 1111 and 2222 2222 2222 2222.", result.Message.Reveal());
        }

        [Fact]
        public void ReplacesSocialSecurityNumbersWithHashes()
        {
            const string ssn1 = "999-99-9999";
            const string ssn2 = "888-88-8888";
            var sensitiveValues = new[]
            {
                new SensitiveValue(ssn1, PiiEntityCategory.USSocialSecurityNumber, null, 0.9, 10, ssn1.Length),
                new SensitiveValue(ssn2, PiiEntityCategory.USSocialSecurityNumber, null, 0.9, 42, ssn2.Length)
            };

            var result = SensitiveDataSanitizer.Sanitize($"My ssn is {ssn1} and my partner's is {ssn2}.", sensitiveValues);

            Assert.Equal("My ssn is 999-00-0001 and my partner's is 999-00-0002.", result.Message.Reveal());
        }

        [Fact]
        public void ReplacesPersonNamesWithPersonIndex()
        {
            const string person1 = "Phil Haack";
            const string person2 = "Paul Nakata";
            var sensitiveValues = new[]
            {
                new SensitiveValue(person1, PiiEntityCategory.Person, null, 0.9, 21, person1.Length),
                new SensitiveValue(person2, PiiEntityCategory.Person, null, 0.9, 36, person2.Length)
            };

            var result = SensitiveDataSanitizer.Sanitize($"Abbot was founded by {person1} and {person2}.", sensitiveValues);

            Assert.Equal("Abbot was founded by Person-1 and Person-2.", result.Message.Reveal());
        }

        [Fact]
        public void DoesNotReplacePersonType()
        {
            const string personType1 = "support agent";
            const string personType2 = "Customer";
            const string personType3 = "doctor";
            var sensitiveValues = new[]
            {
                new SensitiveValue(personType1, "PersonType", null, 0.9, 37, personType1.Length),
                new SensitiveValue(personType2, "PersonType", null, 0.9, 57, personType2.Length),
                new SensitiveValue(personType3, "PersonType", null, 0.9, 77, personType3.Length)
            };

            var result = SensitiveDataSanitizer.Sanitize($"Abbot tracks conversations between a {personType1} and a {personType2}, but not a {personType3}.", sensitiveValues);

            Assert.Equal("Abbot tracks conversations between a Support Agent and a Customer, but not a Doctor.", result.Message.Reveal());
        }

        [Fact]
        public void ReplacesUnknownCategoryWithCategoryNameAndIndex()
        {
            const string number1 = "12343-43-11-a";
            const string number2 = "12343-42-12-b";
            var sensitiveValues = new[]
            {
                new SensitiveValue(number1, PiiEntityCategory.NZSocialWelfareNumber, null, 0.9, 27, number1.Length),
                new SensitiveValue(number2, PiiEntityCategory.NZSocialWelfareNumber, null, 0.9, 45, number2.Length)
            };

            var result = SensitiveDataSanitizer.Sanitize($"Our nz welfare numbers are {number1} and {number2}.", sensitiveValues);

            Assert.Equal("Our nz welfare numbers are NZSocialWelfareNumber-1 and NZSocialWelfareNumber-2.", result.Message.Reveal());
        }

        [Fact]
        public void DoesNotRedactEntitiesMarkedUnredacted()
        {
            const string sanitizeMe = "sanitary";
            const string doNotSanitizeMe = "unsanitary";
            const string message = "This is a message with a sanitary and unsanitary value.";
            var sensitiveValues = new[]
            {
                new SensitiveValue(sanitizeMe, PiiEntityCategory.CASocialInsuranceNumber, null, 0.9, message.IndexOf(sanitizeMe, StringComparison.Ordinal), sanitizeMe.Length),
                new SensitiveValue(doNotSanitizeMe, PiiEntityCategory.Date, null, 0.9, message.IndexOf(doNotSanitizeMe, StringComparison.Ordinal), doNotSanitizeMe.Length)
            };

            var result = SensitiveDataSanitizer.Sanitize(message, sensitiveValues, new HashSet<PiiEntityCategory> {
                PiiEntityCategory.Date
            });

            Assert.Equal("This is a message with a CASocialInsuranceNumber-1 and unsanitary value.", result.Message.Reveal());
        }

        [Fact]
        public void MergesWithExistingReplacementsDictionary()
        {
            const string number1 = "12343-43-11-a";
            const string number2 = "12343-42-12-b";
            const string number3 = "12343-42-12-c";
            var initialSensitiveValues = new[]
            {
                new SensitiveValue(number1, PiiEntityCategory.NZSocialWelfareNumber, null, 0.9, 27, number1.Length),
                new SensitiveValue(number2, PiiEntityCategory.NZSocialWelfareNumber, null, 0.9, 45, number2.Length)
            };
            var initialResult = SensitiveDataSanitizer.Sanitize(
                $"Our nz welfare numbers are {number1} and {number2}.",
                initialSensitiveValues);
            var sensitiveValues = new[]
            {
                new SensitiveValue(number3, PiiEntityCategory.NZSocialWelfareNumber, null, 0.9, 21, number3.Length),
                new SensitiveValue(number2, PiiEntityCategory.NZSocialWelfareNumber, null, 0.9, 39, number2.Length),
            };

            var result = SensitiveDataSanitizer.Sanitize(
                $"Another message with {number3} and {number2}.",
                sensitiveValues,
                existingReplacements: initialResult.Replacements);

            var replacements = result.Replacements.Select(r => $"{r.Key}={r.Value.Reveal()}").ToArray();
            Assert.Equal(new[]
            {
                "NZSocialWelfareNumber-1=12343-43-11-a",
                "NZSocialWelfareNumber-2=12343-42-12-b",
                "NZSocialWelfareNumber-3=12343-42-12-c"
            }, replacements);
        }

        [Fact]
        public void ReturnsExistingReplacementsWhenTextEmpty()
        {
            var replacements = new Dictionary<string, SecretString>
            {
                ["secret"] = "shhhhh!"
            };
            var initialSensitiveValues = new[]
            {
                new SensitiveValue("s", PiiEntityCategory.NZSocialWelfareNumber, null, 0.9, 0, 1),
                new SensitiveValue("h", PiiEntityCategory.NZSocialWelfareNumber, null, 0.9, 1, 1)
            };
            var result = SensitiveDataSanitizer.Sanitize("", initialSensitiveValues, existingReplacements: replacements);

            Assert.Same(replacements, result.Replacements);
        }

        [Fact]
        public void ReturnsExistingReplacementsWhenSensitiveValuesEmpty()
        {
            var replacements = new Dictionary<string, SecretString>
            {
                ["secret"] = "shhhhh!"
            };
            var result = SensitiveDataSanitizer.Sanitize("Some text", Array.Empty<SensitiveValue>(), existingReplacements: replacements);

            Assert.Same(replacements, result.Replacements);
            Assert.Equal("Some text", result.Message.Reveal());
        }
    }
}
