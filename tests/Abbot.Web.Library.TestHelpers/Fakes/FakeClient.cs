using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Serious.Slack;

namespace Serious.TestHelpers;

public record FakeClientResponse(
    object? Body,

    Exception? Exception = null);

public abstract class FakeClient
{
    protected readonly Dictionary<(Type, string?, string?), Dictionary<string, IEnumerable<string>>> Headers = new();
    readonly Dictionary<(Type, string?, string?), FakeClientResponse> _responses = new();

    protected TInfoResponse GetInfoResponse<TInfoResponse, TBody>(string accessToken, string? id)
        where TInfoResponse : InfoResponse<TBody>, new()
    {
        return TryGetInfoResponse<TInfoResponse, TBody>(accessToken, id, out var response)
            ? response
            : new TInfoResponse
            {
                Ok = false,
                Error = "not_found"
            };
    }

    protected bool TryGetInfoResponse<TInfoResponse, TBody>(
        string accessToken,
        string? id,
        [NotNullWhen(true)] out TInfoResponse? response)
        where TInfoResponse : InfoResponse<TBody>, new()
    {
        var key = (typeof(TInfoResponse), accessToken, id);
        if (_responses.TryGetValue(key, out var responseObject))
        {
            switch (responseObject)
            {
                case { Body: TInfoResponse infoResponse }:
                    response = infoResponse;
                    return true;
                case { Exception: { } exception }:
                    throw exception;
            }
        }

        response = default;
        return false;
    }

    protected void AddInfoResponseHeaders<TInfoResponse, TBody>(
        string accessToken,
        string? id,
        string headerName,
        IEnumerable<string> headerValue)
        where TInfoResponse : InfoResponse<TBody>, new()
    {
        var key = (typeof(TInfoResponse), accessToken, id);
        if (!Headers.TryGetValue(key, out var headers))
        {
            headers = new Dictionary<string, IEnumerable<string>> { { headerName, headerValue } };
            Headers.Add(key, headers);
        }
        else
        {
            Headers[key].Add(headerName, headerValue);
        }
    }

    protected TInfoResponse AddInfoResponse<TInfoResponse, TBody>(
        string accessToken,
        string? id,
        TBody responseBody)
        where TInfoResponse : InfoResponse<TBody>, new()
    {
        return AddInfoResponse<TInfoResponse, TBody>(
            accessToken,
            id,
            new TInfoResponse { Ok = true, Body = responseBody });
    }

    protected void AddInfoExceptionResponse<TInfoResponse>(
        string accessToken,
        string? id,
        Exception exception)
    {
        var key = (typeof(TInfoResponse), accessToken, id);
        _responses[key] = new FakeClientResponse(null, exception);
    }

    protected TInfoResponse AddInfoResponse<TInfoResponse, TBody>(
        string accessToken,
        string? id,
        TInfoResponse response)
        where TInfoResponse : InfoResponse<TBody>
    {
        var key = (typeof(TInfoResponse), accessToken, id);
        _responses[key] = new FakeClientResponse(response);
        return response;
    }
}
