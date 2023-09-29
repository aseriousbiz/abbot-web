using System.ComponentModel.DataAnnotations;
using Serious.AspNetCore.DataAnnotations;
using Xunit;

public class GreaterThanAttributeTests
{
    public class TheGetValidationResultMethod
    {
        [Theory]
        [InlineData(1, 1)]
        [InlineData(1, 2)]
        public void ReturnsErrorWhenValidatedValueIsLessThanOrEqualOtherValue(int firstValue, int secondValue)
        {
            var instance = new InputModel(firstValue, secondValue);
            var attribute = new GreaterThanAttribute(nameof(InputModel.OtherValue));
            var validationContext = new ValidationContext(instance)
            {
                DisplayName = "Value"
            };

            var result = attribute.GetValidationResult(firstValue, validationContext);

            Assert.NotNull(result);
            Assert.Equal("Value must be greater than Other Value.", result.ErrorMessage);
        }

        [Fact]
        public void ReturnsSuccessWhenValueGreaterThanDependentProperty()
        {
            var instance = new InputModel(42, 23);
            var attribute = new GreaterThanAttribute(nameof(InputModel.OtherValue));
            var validationContext = new ValidationContext(instance) { DisplayName = "Value" };

            var result = attribute.GetValidationResult(42, validationContext);

            Assert.Equal(ValidationResult.Success, result);
        }

        public record InputModel(
            int? Value,

            [property: Display(Name="Other Value")]
            int? OtherValue);
    }
}
