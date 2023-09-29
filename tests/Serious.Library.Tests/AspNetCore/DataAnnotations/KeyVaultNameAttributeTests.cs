using System.ComponentModel.DataAnnotations;
using Serious.AspNetCore.DataAnnotations;
using Xunit;

public class KeyVaultNameAttributeTests
{
    public class TheIsValidMethod
    {
        [Theory]
        [InlineData("abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyz")]
        [InlineData("abcde*")]
        [InlineData("ábcde")]
        [InlineData("ab_cd")]
        public void ReturnsErrorResultForInvalid(string text)
        {
            var attribute = new KeyVaultSecretNameAttribute();

            var result = attribute.GetValidationResult(text, new ValidationContext(new object()));

            Assert.NotNull(result);
            Assert.Equal(
                "Name must be a 127 character or fewer string and may only contain 0-9, a-z, A-Z, and - characters.",
                result.ErrorMessage);
        }

        [Theory]
        [InlineData("abc")]
        [InlineData("0123")]
        [InlineData("abcdefghijklmnopqrstuvwx")]
        [InlineData("-----")]
        [InlineData("a-b-c-0-1")]
        public void ReturnsSuccessResultForValid(string text)
        {
            var attribute = new KeyVaultSecretNameAttribute();

            var result = attribute.GetValidationResult(text, new ValidationContext(new object()));

            Assert.Equal(ValidationResult.Success, result);
        }
    }
}
