using System.ComponentModel.DataAnnotations;
using Serious.AspNetCore.DataAnnotations;
using Xunit;

public class RequiredIfAttributeTests
{
    public class TheGetValidationResultMethod
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void ReturnsErrorWhenDependentPropertyMatchesValue(string value)
        {
            var instance = new BooleanInputModel(value, true);
            var attribute = new RequiredIfAttribute("ValueRequired", true);
            var validationContext = new ValidationContext(instance) { DisplayName = "Value" };

            var result = attribute.GetValidationResult(null, validationContext);

            Assert.NotNull(result);
            Assert.Equal("Value is required.", result.ErrorMessage);
        }

        [Fact]
        public void ReturnsSuccessWhenDependentPropertyMatchesValueButValidatedPropertyNotEmpty()
        {
            var instance = new BooleanInputModel("Not-empty", true);
            var attribute = new RequiredIfAttribute("ValueRequired", true);
            var validationContext = new ValidationContext(instance) { DisplayName = "Value" };

            var result = attribute.GetValidationResult("Not-empty", validationContext);

            Assert.Equal(ValidationResult.Success, result);
        }

        [Fact]
        public void ReturnsSuccessWhenDependentPropertyMatchesValueButValidatedPropertyNotNull()
        {
            var instance = new BooleanInputModel("Not-empty", true);
            var attribute = new RequiredIfAttribute("ValueRequired", true);
            var validationContext = new ValidationContext(instance) { DisplayName = "Value" };

            var result = attribute.GetValidationResult(new object(), validationContext);

            Assert.Equal(ValidationResult.Success, result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void ReturnsSuccessWhenDependentPropertyDoesNotMatchValue(string value)
        {
            var instance = new BooleanInputModel(value, true);
            var attribute = new RequiredIfAttribute("ValueRequired", false);
            var validationContext = new ValidationContext(instance) { DisplayName = "Value" };

            var result = attribute.GetValidationResult(null, validationContext);

            Assert.Equal(ValidationResult.Success, result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void ReturnsErrorWhenDependentPropertyNotNull(string value)
        {
            var instance = new InputModel(value, "not-null");
            var attribute = new RequiredIfAttribute("ValueRequired");
            var validationContext = new ValidationContext(instance) { DisplayName = "Value" };

            var result = attribute.GetValidationResult(null, validationContext);

            Assert.NotNull(result);
            Assert.Equal("Value is required.", result.ErrorMessage);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void ReturnsSuccessWhenValueAndDependentPropertyNull(string value)
        {
            var instance = new InputModel(value, null);
            var attribute = new RequiredIfAttribute("ValueRequired");
            var validationContext = new ValidationContext(instance) { DisplayName = "Value" };

            var result = attribute.GetValidationResult(null, validationContext);

            Assert.Equal(ValidationResult.Success, result);
        }

        public record InputModel(string? Value, string? ValueRequired);
        public record BooleanInputModel(string? Value, bool ValueRequired);
    }
}
