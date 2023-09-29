using System;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using Newtonsoft.Json.Linq;
using Refit;
using Serious.Abbot.Integrations.Zendesk.Models;

namespace Serious.Abbot.Integrations.Zendesk;

public static class ApiExceptionExtensions
{
    public static bool TryGetErrorDetail(this ApiException apiException, [MaybeNullWhen(false)] out string code, [MaybeNullWhen(false)] out string description, out JObject? details)
    {
        if (apiException.StatusCode == HttpStatusCode.UnsupportedMediaType)
        {
            code = "UnsupportedMediaType";
            description = apiException.ReasonPhrase ?? "Unsupported Media Type";
            details = null;
            return true;
        }

        if (apiException.Content is not { Length: > 0 })
        {
            code = null;
            description = null;
            details = null;
            return false;
        }

        var json = JObject.Parse(apiException.Content);

        var errorProp = json.Property("error", StringComparison.Ordinal);
        var errorsProp = json.Property("errors", StringComparison.Ordinal);
        var descriptionProp = json.Property("description", StringComparison.Ordinal);
        var detailsProp = json.Property("details", StringComparison.Ordinal);

        // Ok, there are like 3 different formats these errors could be in 🙄.
        // 1. 'error' string, 'description' string, optional 'details' object
        if (errorProp is { Value.Type: JTokenType.String } && descriptionProp is { Value.Type: JTokenType.String })
        {
            code = errorProp.Value.Value<string>() ?? string.Empty;
            description = descriptionProp.Value.Value<string>() ?? string.Empty;
            details = detailsProp is { Value.Type: JTokenType.Object }
                ? (JObject)detailsProp.Value
                : null;
            return true;
        }
        // 2. 'error' object of type ApiError
        if (errorProp is { Value.Type: JTokenType.Object } && errorProp.Value.ToObject<ApiError>() is { } err)
        {
            code = err.Code ?? err.Title ?? string.Empty;
            description = err.Detail ?? err.Message ?? string.Empty;
            details = null;
            return true;
        }
        // 3. 'errors' array of ApiError objects
        if (errorsProp is { Value.Type: JTokenType.Array } && errorsProp.Value.ToObject<ApiError[]>() is { } errs)
        {
            if (errs.Length == 0)
            {
                code = null;
                description = null;
                details = null;
                return false;
            }
            code = errs[0].Code ?? errs[0].Title ?? string.Empty;
            description = errs[0].Detail ?? string.Empty;
            details = null;
            return true;
        }

        // 🤷 Who knows.
        code = null;
        description = null;
        details = null;
        return false;
    }
}
