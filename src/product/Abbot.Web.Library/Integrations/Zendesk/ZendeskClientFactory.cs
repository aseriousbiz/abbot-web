using System;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using Refit;

namespace Serious.Abbot.Integrations.Zendesk;

public interface IZendeskClientFactory
{
    IZendeskClient CreateClient(ZendeskSettings settings);
    IZendeskOAuthClient CreateOAuthClient(string subdomain);
}

public class ZendeskClientFactory : IZendeskClientFactory
{
    public static readonly string UserAgentProductName = "Serious.Abbot.Zendesk";
    static readonly RefitSettings RefitSettings = new()
    {
        ContentSerializer = new NewtonsoftJsonContentSerializer(new()
        {
            DefaultValueHandling = DefaultValueHandling.Ignore,
        }),
    };
    readonly IHttpClientFactory _httpClientFactory;

    public ZendeskClientFactory(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public IZendeskClient CreateClient(ZendeskSettings settings)
    {
        if (!settings.HasApiCredentials)
        {
            throw new InvalidOperationException(
                "Cannot create Zendesk client, settings does not include a valid 'Subdomain', 'Email', and 'ApiToken'.");
        }

        var apiToken = settings.ApiToken.Reveal();

        var httpClient = _httpClientFactory.CreateClient("Zendesk");
        httpClient.BaseAddress = new Uri($"https://{settings.Subdomain}.zendesk.com/");

        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
        SetCommonHeaders(httpClient);

        return RestService.For<IZendeskClient>(httpClient, RefitSettings);
    }

    public IZendeskOAuthClient CreateOAuthClient(string subdomain)
    {
        var httpClient = _httpClientFactory.CreateClient("Zendesk");
        httpClient.BaseAddress = new Uri($"https://{subdomain}.zendesk.com/");
        SetCommonHeaders(httpClient);

        return RestService.For<IZendeskOAuthClient>(httpClient, RefitSettings);
    }

    static readonly AssemblyBuildMetadata BuildMetadata = typeof(ZendeskClientFactory).Assembly.GetBuildMetadata();
    static void SetCommonHeaders(HttpClient httpClient)
    {
        // We use this User-Agent to identify comments we post when syncing from Zendesk to Slack
        httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(UserAgentProductName, BuildMetadata.InformationalVersion));
    }
}
