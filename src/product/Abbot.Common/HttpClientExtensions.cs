using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Serious.Abbot.Serialization;

namespace Serious.Abbot;

public static class HttpClientExtensions
{
    public static async Task<TResponseBody> GetJsonAsync<TResponseBody>(
        this HttpClient httpClient,
        Uri uri,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(uri, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsAsync<TResponseBody>(cancellationToken).ConfigureAwait(false);
    }

    public static async Task<TResponseBody> PostJsonAsync<TBody, TResponseBody>(
        this HttpClient httpClient,
        Uri uri,
        TBody body,
        CancellationToken cancellationToken = default)
    {
        var json = AbbotJsonFormat.Default.Serialize(body);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await httpClient.PostAsync(uri, content, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsAsync<TResponseBody>(cancellationToken)
            .ConfigureAwait(false);
    }

    public static async Task<TResponseBody> SendJsonAsync<TResponseBody>(
        this HttpClient httpClient,
        HttpRequestMessage message,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.SendAsync(message, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();
        var str = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return AbbotJsonFormat.Default.Deserialize<TResponseBody>(str)!;
    }

    [SuppressMessage("Microsoft.Reliability",
        "CA2000",
        Justification = "The caller is responsible for disposing the response which will dispose the StringContent.")]
    public static Task<HttpResponseMessage> PostJsonAsync<TBody>(
        this HttpClient httpClient,
        Uri uri,
        TBody body,
        CancellationToken cancellationToken = default)
    {
        var json = AbbotJsonFormat.Default.Serialize(body);
        return httpClient.PostAsync(uri, new StringContent(json, Encoding.UTF8, "application/json"), cancellationToken);
    }

    public static void AddJsonContent<TContent>(
        this HttpRequestMessage message,
        TContent content)
    {
        var json = AbbotJsonFormat.Default.Serialize(content);
        message.Content = new StringContent(
            json,
            Encoding.UTF8,
            "application/json");
    }
}
