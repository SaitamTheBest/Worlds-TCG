using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace web_tcg.Models.Market;

// HTML number inputs submit a dot even when the server uses a French culture.
public sealed class MarketPriceModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext context)
    {
        var value = context.ValueProvider.GetValue(context.ModelName);
        if (value == ValueProviderResult.None) return Task.CompletedTask;
        context.ModelState.SetModelValue(context.ModelName,value);
        if (decimal.TryParse(value.FirstValue?.Replace(',', '.'),
            NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var price))
            context.Result=ModelBindingResult.Success(price);
        else context.ModelState.TryAddModelError(context.ModelName,"Indiquez un prix valide avec deux décimales maximum.");
        return Task.CompletedTask;
    }
}
