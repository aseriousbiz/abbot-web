using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Binders;
using Serious.Cryptography;

namespace Serious.AspNetCore.ModelBinding;

public class SecretStringModelBinder : IModelBinder
{
    readonly IDataProtectionProvider _dataProtectionProvider;

    public SecretStringModelBinder(IDataProtectionProvider dataProtectionProvider)
    {
        _dataProtectionProvider = dataProtectionProvider;
    }

    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var modelName = bindingContext.ModelName;

        var valueProviderResult = bindingContext.ValueProvider.GetValue(modelName);

        if (valueProviderResult == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(modelName, valueProviderResult);

        var value = valueProviderResult.FirstValue;

        if (string.IsNullOrEmpty(value))
        {
            return Task.CompletedTask;
        }

        bindingContext.Result = ModelBindingResult.Success(new SecretString(value, _dataProtectionProvider));
        return Task.CompletedTask;
    }

    public class Provider : IModelBinderProvider
    {
        public IModelBinder? GetBinder(ModelBinderProviderContext? context)
        {
            ArgumentNullException.ThrowIfNull(context);

            return context.Metadata.ModelType == typeof(SecretString)
                ? new BinderTypeModelBinder(typeof(SecretStringModelBinder))
                : null;
        }
    }
}
