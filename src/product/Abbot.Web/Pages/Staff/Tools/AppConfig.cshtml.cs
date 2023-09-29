using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.FeatureManagement;
using Microsoft.FeatureManagement.FeatureFilters;
using Newtonsoft.Json;
using Npgsql;
using Serious.Abbot.Entities;
using Serious.Abbot.FeatureManagement;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Pages.Staff.Tools;

public class AppConfigModel : StaffToolsPage
{
    // Attributes on Controllers and actions get swept up into Endpoint Metadata.
    // Many are not relevant as Endpoint Metadata though, so this is a list of types to ignore.
    // We use strings and type names instead of Type objects because some of these are Embedded and present in _every_ assembly
    // So typeof(NullableContextAttribute), for example, doesn't really work.
    public static readonly HashSet<string> IgnoredMetadataTypes = new()
    {
        "System.Runtime.CompilerServices.CreateNewOnMetadataUpdateAttribute",
        "System.Runtime.CompilerServices.NullableAttribute",
        "System.Runtime.CompilerServices.NullableContextAttribute",
        "System.Runtime.CompilerServices.AsyncStateMachineAttribute",
        "System.Diagnostics.DebuggerStepThroughAttribute",
    };

    readonly IFeatureDefinitionProvider _featureDefinitionProvider;
    readonly FeatureService _featureService;
    readonly List<FeatureDefinition> _features = new();
    readonly IConfiguration _configuration;
    readonly IHostEnvironment _hostEnvironment;
    readonly EndpointDataSource _endpointsDataSource;
    readonly PageLoader _pageLoader;
    readonly IHttpClientFactory _httpClientFactory;
    readonly IApplicationDiscriminator _applicationDiscriminator;
    readonly IDataProtectionProvider _dataProtectionProvider;
    readonly AbbotContext _abbotContext;
    readonly HealthCheckService _healthCheckService;
    readonly ISettingsManager _settingsManager;

    static readonly List<string> AllowedConfigValues = new()
    {
        "Auth0:Domain",
        "Skill:ProxyLink",
        "Skill:DotNetEndpoint",
        "Skill:JavaScriptEndpoint",
        "Skill:PythonEndpoint",
        "Slack:AppId",
        "Slack:ClientId",
        "Zendesk:ClientId",
        "Zendesk:RequiredScopes",
        "HubSpot:AppId",
        "HubSpot:ClientId",
        "HubSpot:RequiredScopes",
        "HubSpot:TimelineEvents:LinkedSlackConversation",
        "HubSpot:TimelineEvents:SlackMessagePosted",
        "Stripe:PublishableKey",
        "Eventing:Transport",
        "Eventing:Endpoint",

        // App Services env vars
        "WEBSITE_SITE_NAME",
        "WEBSITE_HOSTNAME",
        "WEBSITE_OS",
    };

    public AppConfigModel(
        FeatureService featureService,
        IFeatureDefinitionProvider featureDefinitionProvider,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        EndpointDataSource endpointsDataSource,
        PageLoader pageLoader,
        IHttpClientFactory httpClientFactory,
        IApplicationDiscriminator applicationDiscriminator,
        IDataProtectionProvider dataProtectionProvider,
        AbbotContext abbotContext,
        HealthCheckService healthCheckService,
        ISettingsManager settingsManager)
    {
        _featureService = featureService;
        _featureDefinitionProvider = featureDefinitionProvider;
        _configuration = configuration;
        _hostEnvironment = hostEnvironment;
        _endpointsDataSource = endpointsDataSource;
        _pageLoader = pageLoader;
        _httpClientFactory = httpClientFactory;
        _applicationDiscriminator = applicationDiscriminator;
        _dataProtectionProvider = dataProtectionProvider;
        _abbotContext = abbotContext;
        _healthCheckService = healthCheckService;
        _settingsManager = settingsManager;
    }

    public DomId RouteTableDomId => new("route-table");


    public IReadOnlyList<FeatureDefinition> Features => _features;

    public IFeatureActor? YourActor { get; private set; }

    public string EnvironmentName { get; set; } = null!;

    public string ContentRootPath { get; set; } = null!;

    public string? DataProtectionAppDiscriminator { get; set; }

    public DateTime StartTime { get; set; } = DateTime.MinValue;

    public IDictionary<string, string> ConfigValues { get; set; } = new Dictionary<string, string>();

    public IReadOnlyList<Endpoint> Endpoints { get; private set; } = null!;

    public ReleaseChannel? DotNetReleaseChannel { get; set; }

    public string DotNetRuntimePath { get; set; } = string.Empty;

    public HealthReport HealthReport { get; set; } = null!;

    public IReadOnlyList<string> LatestMigrations { get; set; } = null!;

    public IReadOnlyList<string> CompletedDataSeeders { get; set; } = null!;
    public string? DatabaseHost { get; set; }
    public int? DatabasePort { get; set; }
    public string? DatabaseUser { get; set; }
    public string? DatabaseName { get; set; }

    public async Task OnGet()
    {
        // Check health
        HealthReport = await _healthCheckService.CheckHealthAsync();
        LatestMigrations = (await _abbotContext.Database.GetAppliedMigrationsAsync())
            .OrderByDescending(id => id).Take(15).ToList();
        CompletedDataSeeders = await CollectCompletedDataSeedersAsync();

        // Fetch the release channel
        try
        {
            DotNetReleaseChannel = await GetReleaseChannelAsync();
        }
        catch
        {
            // Meh, don't care.
        }

        DotNetRuntimePath = Path.GetDirectoryName(typeof(string).Assembly.Location).Require();

        YourActor = HttpContext.GetFeatureActor();
        var definitions = _featureDefinitionProvider.GetAllFeatureDefinitionsAsync();

        await foreach (var featureDef in definitions)
        {
            _features.Add(featureDef);
        }

        EnvironmentName = _hostEnvironment.EnvironmentName;
        ContentRootPath = _hostEnvironment.ContentRootPath;

        // Hacky, but it works and gives us the _actual_ discriminator value.
        var protectorType = typeof(DataProtectionOptions).Assembly
            .GetType("Microsoft.AspNetCore.DataProtection.KeyManagement.KeyRingBasedDataProtector");

        if (protectorType is not null && _dataProtectionProvider.GetType() == protectorType)
        {
            var prop = protectorType.GetProperty("Purposes", BindingFlags.NonPublic | BindingFlags.Instance);
            if (prop is not null)
            {
                if (prop.GetValue(_dataProtectionProvider) is string[] { Length: > 0 } purposes)
                {
                    DataProtectionAppDiscriminator = purposes[0];
                }
            }
        }

        StartTime = Program.StartTime;

        foreach (var key in AllowedConfigValues)
        {
            if (_configuration[key] is { Length: > 0 } v)
            {
                ConfigValues[key] = v;
            }
        }

        var connectionString = new NpgsqlConnectionStringBuilder(_abbotContext.Database.GetConnectionString().Require());
        DatabaseHost = connectionString.Host;
        DatabasePort = connectionString.Port;
        DatabaseUser = connectionString.Username;
        DatabaseName = connectionString.Database;
    }

    async Task<IReadOnlyList<string>> CollectCompletedDataSeedersAsync()
    {
        var settings = await _settingsManager.GetAllAsync(SettingsScope.Global, "DataSeeder:");
        return settings
            .Where(s => bool.TryParse(s.Value, out var b) && b)
            .Select(s => s.Name["DataSeeder:".Length..])
            .ToList();
    }

    public async Task<IActionResult> OnPostRouteTableAsync()
    {
        // Loading endpoints is made a little tricky by Razor Pages, which dynamically "compile" endpoint metadata when the page is launched.
        // This is done to allow you to change page content and refresh without relaunching
        // So we need to force compilation of any endpoint that hasn't been compiled already.
        var endpoints = new List<Endpoint>();
        foreach (var ep in _endpointsDataSource.Endpoints)
        {
            var pad = ep.Metadata.GetMetadata<PageActionDescriptor>();
            if (pad is not null)
            {
                // Load the page
                var compiled = await _pageLoader.LoadAsync(pad, ep.Metadata);
                endpoints.Add(compiled.Endpoint.Require());
            }
            else
            {
                endpoints.Add(ep);
            }
        }

        return TurboReplace(RouteTableDomId, Partial("_RouteTable", endpoints));
    }

    async Task<ReleaseChannel?> GetReleaseChannelAsync()
    {
        // TODO: We could make a Refit client or something but meh.
        var httpClient = _httpClientFactory.CreateClient("DotNetReleases");
        var response =
            await httpClient.GetAsync(
                new Uri("https://raw.githubusercontent.com/dotnet/core/main/releases-index.json"));

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var releases = JsonConvert.DeserializeObject<ReleasesIndexResponse>(await response.Content.ReadAsStringAsync());
        return releases?.ReleasesIndex.FirstOrDefault(r =>
            r.ChannelVersion == $"{Environment.Version.Major}.{Environment.Version.Minor}");
    }

    public class ReleasesIndexResponse
    {
        [JsonProperty("releases-index")]
        public IReadOnlyList<ReleaseChannel> ReleasesIndex { get; set; } = null!;
    }

    public class ReleaseChannel
    {
        [JsonProperty("channel-version")]
        public string ChannelVersion { get; set; } = null!;

        [JsonProperty("latest-runtime")]
        public string LatestRuntime { get; set; } = null!;

        [JsonProperty("latest-release-date")]
        public DateTime LatestReleaseDate { get; set; }

        [JsonProperty("eol-date")]
        public DateTime? EndOfLifeDate { get; set; }

        [JsonProperty("support-phase")]
        public string SupportPhase { get; set; } = null!;
    }
}
