using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Refit;
using Serious.Abbot.Integrations.MergeDev.Models;

namespace Serious.Abbot.Integrations.MergeDev;

public interface IMergeDevClientFactory
{
    HttpRequestMessage ApplyAuthorization(HttpRequestMessage request, string? accountToken = null);

    IMergeDevClient CreateClient(TicketingSettings settings);
}

public class MergeDevClientFactory : IMergeDevClientFactory
{
    public static readonly string UserAgentProductName = "Serious.Abbot.MergeDev";

    static readonly RefitSettings RefitSettings = new()
    {
        ContentSerializer = new NewtonsoftJsonContentSerializer(new()
        {
            DefaultValueHandling = DefaultValueHandling.Ignore,
        }),
    };

    readonly MergeDevOptions _mergeDevOptions;
    readonly IHttpClientFactory _httpClientFactory;

    public MergeDevClientFactory(IOptions<MergeDevOptions> mergeDevOptions, IHttpClientFactory httpClientFactory)
    {
        _mergeDevOptions = mergeDevOptions.Value;
        _httpClientFactory = httpClientFactory;
    }

    public IMergeDevClient CreateClient(TicketingSettings settings)
    {
        if (!settings.HasApiCredentials)
        {
            throw new InvalidOperationException(
                "Cannot create MergeDev client, settings does not include a valid 'AccountToken'.");
        }

        var httpClient = _httpClientFactory.CreateClient("MergeDev");
        httpClient.BaseAddress = IMergeDevClient.TicketingApiUrl;

        ApplyAuthorization(httpClient.DefaultRequestHeaders, settings.AccessToken.Reveal());

        return RestService.For<IMergeDevClient>(httpClient, RefitSettings);
    }

    public HttpRequestMessage ApplyAuthorization(HttpRequestMessage request, string? accountToken = null)
    {
        ApplyAuthorization(request.Headers, accountToken);
        return request;
    }

    void ApplyAuthorization(HttpRequestHeaders headers, string? accountToken)
    {
        SetCommonHeaders(headers);
        headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _mergeDevOptions.AccessToken.Require());

        if (accountToken is not null)
        {
            headers.Add("X-Account-Token", accountToken);
        }
    }

    static readonly AssemblyBuildMetadata BuildMetadata = typeof(MergeDevClientFactory).Assembly.GetBuildMetadata();
    static void SetCommonHeaders(HttpRequestHeaders requestHeaders)
    {
        requestHeaders.UserAgent.Add(new ProductInfoHeaderValue(UserAgentProductName, BuildMetadata.InformationalVersion));
    }
}

public interface IMergeDevClient
{
    public static readonly Uri TicketingApiUrl = new("https://api.merge.dev/api/ticketing/v1");

    /// <summary>
    /// Creates a <see cref="MergeDevTicket"/> object with the given values.
    /// </summary>
    /// <param name="ticket">The ticket to create.</param>
    /// <returns>The created <see cref="MergeDevTicket"/>.</returns>
    [Post("/tickets")]
    Task<ModelEnvelope<MergeDevTicket>> CreateTicketAsync(Create ticket);
}
