
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Serious.AspNetCore.ModelBinding;
using Xunit;

public class ModelBindingExtensionsTests
{
    public class TheRemoveExceptMethod
    {
        [Fact]
        public void RemovesModelStateWithoutThePrefixAndUpdatesValidationStateAccordingly()
        {
            var modelState = new ModelStateDictionary();
            modelState.AddModelError("Foo", "Foo error");
            modelState.AddModelError("Bar", "Bar error");
            modelState.SetModelValue("Baz.Foo", "123", "123");
            modelState.SetModelValue("Baz.Biz", "345", "345");
            modelState.MarkFieldValid("Baz.Foo");
            modelState.MarkFieldValid("Baz.Biz");
            Assert.Equal(4, modelState.Count);
            Assert.False(modelState.IsValid);

            modelState.RemoveExcept("Baz");

            Assert.True(modelState.IsValid);
            Assert.Equal(2, modelState.Count);
            Assert.True(modelState.ContainsKey("Baz.Foo"));
            Assert.True(modelState.ContainsKey("Baz.Biz"));
        }

        [Fact]
        public void KeepsFieldWithSameNameAsPrefix()
        {
            var modelState = new ModelStateDictionary();
            modelState.AddModelError("Foo", "Foo error");
            modelState.AddModelError("Bar", "Bar error");
            modelState.SetModelValue("Baz", "123", "123");
            modelState.MarkFieldValid("Baz");
            Assert.Equal(3, modelState.Count);
            Assert.False(modelState.IsValid);

            modelState.RemoveExcept("Baz");

            Assert.True(modelState.IsValid);
            Assert.Equal(1, modelState.Count);
            Assert.True(modelState.ContainsKey("Baz"));
        }

        [Fact]
        public void RemovesModelStateWithoutThePrefix()
        {
            var modelState = new ModelStateDictionary();
            modelState.AddModelError("Foo", "Foo error");
            modelState.AddModelError("Bar", "Bar error");
            modelState.AddModelError("Baz.Foo", "Baz.Foo error");
            modelState.SetModelValue("Baz.Biz", "345", "345");
            modelState.MarkFieldValid("Baz.Biz");
            Assert.Equal(4, modelState.Count);
            Assert.False(modelState.IsValid);

            modelState.RemoveExcept("Baz");

            Assert.False(modelState.IsValid);
            Assert.Equal(2, modelState.Count);
            Assert.True(modelState.ContainsKey("Baz.Foo"));
            Assert.True(modelState.ContainsKey("Baz.Biz"));
        }
    }
}
