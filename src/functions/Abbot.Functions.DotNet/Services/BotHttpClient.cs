using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Fizzler.Systems.HtmlAgilityPack;
using HtmlAgilityPack;
using Refit;
using Serious.Abbot.Messages;
using Serious.Abbot.Scripting;
using Serious.Abbot.Serialization;

namespace Serious.Abbot.Functions.Services;

public class BotHttpClient : IBotHttpClient
{
    readonly HttpClient _httpClient;

    public BotHttpClient(HttpClient httpClient) // Configured in Abbot.Functions Startup.
    {
        _httpClient = httpClient;

        var meta = typeof(BotHttpClient).Assembly.GetBuildMetadata();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"Mozilla/5.0 (compatible; Abbot/{meta.Version}#{meta.CommitId})");
    }

    public async Task<dynamic?> SendJsonAsync(Uri url, HttpMethod httpMethod, object? content, Headers headers)
    {
        using var request = new HttpRequestMessage
        {
            Method = httpMethod,
            RequestUri = url,
            Headers =
            {
                { HttpRequestHeader.Accept.ToString(), "application/json" }
            }
        };
        if (content is not null)
        {
            request.AddJsonContent(content);
        }

        headers.CopyTo(request.Headers);
        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var responseText = await response.Content.ReadAsStringAsync();
        return AbbotJsonFormat.Default.Deserialize(responseText);
    }

    public async Task<AbbotResponse<TResponse>> SendJsonAsAsync<TResponse>(Uri url, HttpMethod httpMethod, object? content, Headers headers)
    {
        using var request = new HttpRequestMessage
        {
            Method = httpMethod,
            RequestUri = url,
            Headers =
            {
                { HttpRequestHeader.Accept.ToString(), "application/json" }
            }
        };
        if (content is not null)
        {
            request.AddJsonContent(content);
        }

        headers.CopyTo(request.Headers);
        var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            return AbbotResponse.Error<TResponse>(new ProblemDetails
            {
                Status = (int)response.StatusCode,
                Detail = await response.Content.ReadAsStringAsync(),
            });
        }
        var responseBody = await response.Content.ReadAsAsync<TResponse>();
        return AbbotResponse.Success((int)response.StatusCode, responseBody);
    }

    /// <summary>
    /// Requests the specified url and returns an <see cref="HtmlNode"/> with
    /// the first section of the HTML that matches the selector. This uses the
    /// HtmlAgilityPack under the hood.
    /// </summary>
    /// <returns>An <see cref="HtmlNode"/> with the section of the web page.</returns>
    public async Task<HtmlNode?> ScrapeAsync(Uri url, string selector)
    {
        var document = await GetDocument(url);
        return document?.QuerySelector(selector);
    }

    /// <summary>
    /// Requests the specified url and returns an <see cref="HtmlNode"/> with
    /// all sections of the HTML that match the selector. This uses the
    /// HtmlAgilityPack under the hood.
    /// </summary>
    /// <returns>An <see cref="HtmlNode"/> with the section of the web page.</returns>
    public async Task<IReadOnlyList<HtmlNode>> ScrapeAllAsync(Uri url, string selector)
    {
        var document = await GetDocument(url);
        return document?.QuerySelectorAll(selector).ToReadOnlyList() ?? Array.Empty<HtmlNode>();
    }

    static async Task<HtmlNode?> GetDocument(Uri url)
    {
        var web = new HtmlWeb();
        var html = await web.LoadFromWebAsync(url.ToString());
        return html?.DocumentNode;
    }
}
