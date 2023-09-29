using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Reflection;
using Abbot.Scripting;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.Exporter;
using Hangfire;
using Hangfire.Dashboard;
using Hangfire.PostgreSql;
using MassTransit.Logging;
using MassTransit.Monitoring;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.FeatureManagement.FeatureFilters;
using Microsoft.OpenApi.Models;
using MirrorSharp;
using MirrorSharp.AspNetCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NodaTime;
using NodaTime.Serialization.JsonNet;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serious.Abbot.AI;
using Serious.Abbot.AppStartup;
using Serious.Abbot.Billing;
using Serious.Abbot.Clients;
using Serious.Abbot.Compilation;
using Serious.Abbot.Configuration;
using Serious.Abbot.Conversations;
using Serious.Abbot.Entities;
using Serious.Abbot.Eventing;
using Serious.Abbot.Extensions;
using Serious.Abbot.Filters;
using Serious.Abbot.Forms;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Infrastructure.AppStartup;
using Serious.Abbot.Infrastructure.Hangfire;
using Serious.Abbot.Infrastructure.Middleware;
using Serious.Abbot.Infrastructure.Security;
using Serious.Abbot.Integrations;
using Serious.Abbot.Integrations.GitHub;
using Serious.Abbot.Integrations.HubSpot;
using Serious.Abbot.Live;
using Serious.Abbot.Messaging;
using Serious.Abbot.Onboarding;
using Serious.Abbot.Pages;
using Serious.Abbot.PayloadHandlers;
using Serious.Abbot.Routing;
using Serious.Abbot.Security;
using Serious.Abbot.Serialization;
using Serious.Abbot.Services;
using Serious.Abbot.Services.DefaultResponder;
using Serious.Abbot.Telemetry;
using Serious.Abbot.Validation;
using Serious.Abbot.Web.Infrastructure;
using Serious.AspNetCore;
using Serious.AspNetCore.ModelBinding;
using Serious.Cryptography;
using Serious.Logging;
using Serious.Razor;
using Serious.Razor.Controllers;
using Serious.Slack.AspNetCore;
using StackExchange.Profiling;
using Stripe;
using File = System.IO.File;

[assembly: CLSCompliant(false)]

namespace Serious.Abbot.Web;

public class Startup
{
    static readonly Histogram<long> ConfigureServicesDuration = AbbotTelemetry.Meter.CreateHistogram<long>(
        "startup.configureServices.duration",
        "milliseconds",
        "The duration of the ConfigureServices method");
    static readonly Histogram<long> ConfigureDuration = AbbotTelemetry.Meter.CreateHistogram<long>(
        "startup.configure.duration",
        "milliseconds",
        "The duration of the Configure method");

    public Startup(IConfiguration configuration, IHostEnvironment hostEnvironment)
    {
        Configuration = configuration;
        HostEnvironment = hostEnvironment;
    }

    public IConfiguration Configuration { get; }

    public IHostEnvironment HostEnvironment { get; }

    // This method gets called by the runtime. Use this method to add services to the container.
    public void ConfigureServices(IServiceCollection services)
    {
        // This should be the first line in configure services.
        using var _ = ConfigureServicesDuration.Time();

        // Order matters! Hosted Services will run in the order registered in the container.
        services.AddHostedService<MigratorService>();
        services.AddHostedService<DataSeedRunnerService>();
        services.AddScoped<DataSeedRunner>();
        services.RegisterAllTypesInSameAssembly<IDataSeeder>(ServiceLifetime.Scoped);

        services.AddTransient<Migrator>();

        services.AddSystemClock();
        services.AddSingleton<IStopwatchFactory, SystemStopwatchFactory>();

        services.Configure<RequestLocalizationOptions>(options => {
            options.DefaultRequestCulture = new RequestCulture("en-US");
        });

        services.AddHttpClient();
        services.AddTransient<IAuditLog, AuditLog>();
        services.AddTransient<IAuditLogReader, AuditLogReader>();
        services.AddTransient<ISkillAuditLog, SkillAuditLog>();
        services.AddTransient<PlatformRequirements>();
        services.AddTransient<ISlackThreadExporter, SlackThreadExporter>();
        services.AddTransient<IImportService, ImportService>();
        services.AddTransient<IOrganizationApiSyncer, OrganizationApiSyncer>();
        services.AddScoped<CoverageHoursResponseTimeCalculator>();
        services.AddApiServices();

        services.AddSingleton<ICorrelationService, CorrelationService>();

        // Used for the markdown text area
        services.AddMarkdownTextArea();

        // Recurring Jobs
        services.AddTransient<DailyMetricsRollupJob>();
        services.AddTransient<DailySlackEventsRollupJob>();
        services.AddTransient<AssemblyCacheGarbageCollectionJob>();

        // Skill Compiler
        services.AddSkillCompilationServices(Configuration.GetSection("Compilation"), Configuration);

        services.AddBotServices(Configuration);

        services.AddDatabaseAndRepositoryServices(Configuration);
        services.AddAzurePostgresServices(Configuration);
        services.Configure<SkillOptions>(
            Configuration.GetSection(SkillOptions.Skill));

        services.Configure<GoogleOptions>(
            Configuration.GetSection(GoogleOptions.Google));

        services.Configure<Auth0Options>(
            Configuration.GetSection(Auth0Options.Auth0));

        services.Configure<OpenAIOptions>(Configuration.GetSection(OpenAIOptions.OpenAI));

        services.AddHttpClient<IGeocodeService, GoogleGeocodeService>();
        services.AddTransient<IDefaultResponderService, DefaultResponderService>();
        services.AddAbbotAIServices(Configuration);
        services.RegisterAllBuiltInSkills();
        services.RegisterAllHandlers();

        services.AddHangfire(options => options
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_170)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings(jss => { jss.ConfigureForNodaTime(DateTimeZoneProviders.Tzdb); })
            .UseFilter(new DiagnosticJobFilter())
            .UsePostgreSqlStorage(Configuration.GetConnectionString(AbbotContext.ConnectionStringName)));

        services.AddHangfireServer(options => {
            // "alpha" is our priority queue. Incoming Slack events are placed here.
            // Queues are run in the order that depends on the concrete storage implementation.
            // Hangfire.SqlServer the order is defined by alphanumeric order and array index is ignored
            // Hangfire.Pro.Redis package, array index is important and queues with a lower index will be processed first.
            // I don't know what the priority is for Hangfire.PostgreSql, so it seemed safe to hedge for both cases
            // with the "alpha" queue.
            options.Queues = new[]
            {
                HangfireQueueNames.HighPriority, HangfireQueueNames.NormalPriority, HangfireQueueNames.Default,
                HangfireQueueNames.Maintenance
            };
        });

        services.AddSingleton<ITimeZoneResolver>(new TimeZoneConverterResolver());

        services
            .AddControllers(options => {
                options.ModelBinderProviders.Insert(0, new SecretStringModelBinder.Provider());
            })
            .AddNewtonsoftJson(options => {
                NewtonsoftJsonAbbotJsonFormat.Apply(options.SerializerSettings);
            })
            .AddApplicationPart(typeof(MarkdownController).Assembly);

        services.AddRazorPages()
            .AddRazorPagesOptions(options => {
                AuthorizationPolicies.ConfigureRazorPagesAuthorization(options);

                options.Conventions.AddPageRoute("/Skills/Edit", "/skills/{skill}");

                // When adding routes to a page, make sure the more specific ones
                // (the ones that consume more route values) come first.
                // Otherwise, they could end up being matched by the more general one
                // and the remaining route values will end up as query string parameters.
                options.Conventions.AddMultiRoutePage("/Index",
                    "/rooms/{room}/conversations", "/");
                options.Conventions.AddMultiRoutePage("/Playbooks/Versions/Import",
                    "/playbooks/{slug}/import", "/playbooks/import");

                options.Conventions.Add(new LowercasePageRoutingConvention("handler"));
                options.Conventions.AddFolderRouteModelConvention("/",
                    model => {
                        // Lowercase everything not inside a "{}" placeholder
                        foreach (var selector in model.Selectors)
                        {
                            if (selector.AttributeRouteModel is not { Template: { Length: > 0 } template })
                            {
                                continue;
                            }

                            selector.AttributeRouteModel.Template = RoutingHelper.LowercaseRouteTemplate(template);
                        }
                    });

                // This should be the last convention applied so that staff routes are added for each of the routes added above
                options.Conventions.AddStaffViewablePages(
                    "/Customers/View",
                    "/Playbooks/Index",
                    "/Playbooks/View",
                    "/Playbooks/Settings",
                    "/Playbooks/Runs/Index",
                    "/Playbooks/Runs/View",
                    "/Playbooks/Runs/Group",
                    "/Playbooks/Versions/Index",
                    "/Playbooks/Versions/Import",
                    "/Playbooks/Versions/View");
            })
            .AddMvcOptions(options => {
                options.Conventions.Add(new AbbotApiExplorerVisibilityConvention());
                options.Filters.AddSlackRequestVerificationFilter();
                options.Filters.Add<HubSpotWebhookSignatureVerificationFilter>();
                options.Filters.Add<OrganizationStateFilter>();
            })
            .AddNewtonsoftJson(options => {
                options.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
                options.SerializerSettings.Converters.Add(new StringEnumConverter());
            })
            .AddRazorRuntimeCompilation();

        services.Configure<CookieTempDataProviderOptions>(options => {
            // https://docs.microsoft.com/en-us/aspnet/core/security/gdpr?view=aspnetcore-2.2#tempdata-provider-and-session-state-cookies-arent-essential
            // Mark the cookie used to store MVC [TempData] as Essential.
            // This means we should take care to not store any tracking/identification information in [TempData].
            options.Cookie.IsEssential = true;
        });

        services.AddRouting(options => {
            options.ConstraintMap.Add("tolower", typeof(LowercaseParameterTransformer));
        });

        services.Configure<SkillOptions>(
            Configuration.GetSection(SkillOptions.Skill));

        services.AddTransient<IBuiltinSkillRegistry, BuiltinSkillRegistry>();
        AuthenticationConfig.Apply(services, Configuration);

        services.AddDataProtectionKeys(Configuration, HostEnvironment);
        services.AddSingleton<ISensitiveLogDataProtector, SensitiveLogDataProtector>();

        services.Configure<WebStorageOptions>(Configuration.GetSection("WebStorage"));

        services.AddSingleton<IAbbotWebFileStorage, AbbotWebFileStorage>();
        services.AddSingleton<IApiTokenCache, ApiTokenCache>();
        services.AddSingleton<IIdentityProviderTokenRetriever, IdentityProviderTokenRetriever>();
        services.AddTransient<ISkillNameValidator, SkillNameValidator>();
        services.AddTransient<IPatternValidator, PatternValidator>();
        services.AddTransient<IRoleManager, RoleManager>();
        services.AddTransient<IBotInstaller, BotInstaller>();
        services.AddOptions<StripeOptions>()
            .Bind(Configuration.GetSection("Stripe"));

        services.AddTransient<IBillingService, BillingService>();
        services.AddPolicies();
        services.AddTransient<IApiTokenFactory, ApiTokenFactory>();
        services.AddTransient<IAuthenticationHandler, AuthenticationHandler>();
        services.AddSkillSecretServices();

        StripeConfiguration.ApiKey = Configuration["Stripe:SecretKey"];

        var mayViewMiniProfiler = new Func<HttpRequest, bool>(
            request => request.IsLocal() || request.HttpContext.IsStaffMode());

        services.AddMiniProfiler(options => {
            options.PopupRenderPosition = RenderPosition.Right;
            options.ResultsListAuthorize = mayViewMiniProfiler;
            options.ResultsAuthorize = mayViewMiniProfiler;
        }).AddEntityFramework();

        services.AddHttpContextAccessor();
        services.AddSingleton<IActionContextAccessor, ActionContextAccessor>();
        services.AddSingleton<IUrlGenerator, UrlGenerator>();

        services.AddFeatureFlagServices(Configuration);
        services.Configure<TargetingEvaluationOptions>(o => { o.IgnoreCase = true; });

        services.Configure<AbbotOptions>(Configuration.GetSection("Abbot"));

        services.AddTransient<IMessageRenderer, MessageRenderer>();

        services.AddSwaggerGen(options => {
            var defaultSchemaIdSelector = options.SchemaGeneratorOptions.SchemaIdSelector;
            options.MapType<TimeSpan>(() => new OpenApiSchema()
            {
                Type = "number",
                Format = "double"
            });

            options.UseOneOfForPolymorphism();
            options.CustomSchemaIds(t => {
                var displayNameAttribute = t.GetCustomAttribute<DisplayNameAttribute>();
                if (displayNameAttribute is not null)
                {
                    return displayNameAttribute.DisplayName;
                }

                if (t.Name.EndsWith("ResponseModel", StringComparison.Ordinal))
                {
                    return t.Name[0..^"ResponseModel".Length];
                }

                return defaultSchemaIdSelector(t);
            });

            options.SwaggerDoc("internal",
                new OpenApiInfo
                {
                    Version = "internal",
                    Title = "Abbot Internal API",
                    Description = "APIs used by the Abbot website. No support is provided for these APIs.",
                });

            var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }

            // Tell Swagger about how some of our custom types are serialized
            // Unfortunately this doesn't support open generics :(.
            options.MapType<Id<Skill>>(() => new OpenApiSchema { Type = "number" });
            options.MapType<Id<Playbook>>(() => new OpenApiSchema { Type = "number" });
        });

        services.AddSwaggerGenNewtonsoftSupport();

        services.AddAnalyticsServices(Configuration.GetSection("Analytics"));
        services.AddScoped<TicketModalService>();
        services.AddScoped<TicketNotificationService>();
        services.AddSingleton<ITicketIntegrationService, TicketIntegrationService>();
        services.AddTransient(typeof(TicketLinkerJob<>));
        services.AddTransient<DismissHandler>();
        services.AddTransient<BlockKitToHtmlFormatter>();
        services.AddTransient<ConversationMessageToHtmlFormatter>();
        services.AddZendeskIntegrationServices(Configuration.GetSection("Zendesk"));
        services.AddHubSpotIntegrationServices(Configuration.GetSection("HubSpot"));
        services.AddGitHubIntegrationServices(Configuration.GetSection("GitHub"));
        services.AddMergeDevIntegrationServices(Configuration.GetSection("MergeDev"));
        services.AddFormsServices();
        services.AddAnnouncementDispatcherAndTargets();

        services.AddMassTransitConfig(Configuration.GetSection("Eventing"));

        var hcOptions = new AbbotHealthCheckOptions();
        Configuration.GetSection("Abbot:HealthChecks").Bind(hcOptions);
        services.AddHealthChecks()
            .AddDbContextCheck<AbbotContext>()
            .AddHangfire(options => {
                // Dev environments tend to have more failed jobs 🤣
                options.MaximumJobsFailed = hcOptions.MaximumHangfireJobsFailed;
            })
            .AddCheck<EventingHealthCheck>("Eventing");

        services.AddAzureClients(builder => {
            builder.UseCredential(new DefaultAzureCredential(new DefaultAzureCredentialOptions()
            {
                Diagnostics =
                {
                    IsLoggingEnabled = true,
                    IsLoggingContentEnabled = HostEnvironment.IsDevelopment(),
                    IsAccountIdentifierLoggingEnabled = true,
                }
            }));
        });

        services.AddAbbotLiveServices(Configuration);

        // Add our notifier after the MassTransit service so the bus is running.
        services.AddTransient<IHostedService, StartupNotifierService>();

        services.AddPlaybookServices();
        services.AddOnboardingServices();

        // Here, we configure Tracing and Metrics, whereas logging was configured up in Program.cs
        services.AddOpenTelemetry()
            .ConfigureResource(Program.AddAbbotOtelService)
            .WithTracing(builder => {
                builder.AddAspNetCoreInstrumentation();
                builder.AddEntityFrameworkCoreInstrumentation();
                builder.AddHttpClientInstrumentation();
                builder.AddHangfireInstrumentation();
                builder.AddSource(DiagnosticHeaders.DefaultListenerName);
                builder.AddSource(AbbotTelemetry.ActivitySource.Name);
                if (Configuration["ApplicationInsights:ConnectionString"] is
                    { Length: > 0 } appInsightsConnectionString)
                {
                    builder.AddAzureMonitorTraceExporter(o => {
                        o.ConnectionString = appInsightsConnectionString;
                    });
                }
            })
            .WithMetrics(builder => {
                builder.AddAspNetCoreInstrumentation();
                builder.AddHttpClientInstrumentation();
                builder.AddMeter(InstrumentationOptions.MeterName);
                builder.AddMeter(AbbotTelemetry.Meter.Name);
                if (Configuration["ApplicationInsights:ConnectionString"] is
                    { Length: > 0 } appInsightsConnectionString)
                {
                    builder.AddAzureMonitorMetricExporter(o => {
                        o.ConnectionString = appInsightsConnectionString;
                    });
                }
            });

        services.Configure<JobsOptions>(Configuration.GetSection("Jobs"));
        services.Configure<SlackEventOptions>(Configuration.GetSection("Slack:Events"));
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    [SuppressMessage("Microsoft.Design", "CA1822", Justification = "Method called via reflection")]
    public void Configure(
        IApplicationBuilder app,
        IWebHostEnvironment env,
        ILoggerFactory loggerFactory,
        IDataProtectionProvider dataProtectionProvider,
        ISensitiveLogDataProtector sensitiveLogDataProtector,
        IOptions<AbbotOptions> abbotOptions,
        IOptions<LiveOptions> liveOptions)
    {
        // This should be the first line in Configure
        using var _ = ConfigureDuration.Time();

        ApplicationLoggerFactory.Configure(loggerFactory, sensitiveLogDataProtector);
        ApplicationEnvironment.Configure(env);
        AllowedHosts.Init(abbotOptions.Value, liveOptions.Value);
        SecretString.Configure(dataProtectionProvider);

        async Task LoggingScopeMiddleware(HttpContext context, RequestDelegate next)
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<Startup>>();
            using var scope = logger.BeginRequestScope(context);
            await next(context);
        }

        // We need an explicit request scope. It seems ASP.NET Core doesn't provide one for OpenTelemetry?
        app.Use(LoggingScopeMiddleware);
        app.UseForwardedHeaders();
        app.UseRequestLocalization();
        if (!env.IsDevelopment())
        {
            // Must be after AllowedHosts.Init()
            Expect.True(WebConstants.DefaultHost != "localhost:4979");

            app.UseHsts();
        }

        app.UseExceptionHandler("/Status/500");
        app.UseWhen(context => !context.IsSwapPingPath(), mainApp => { mainApp.UseHttpsRedirection(); });

        app.UseRewriter(new RewriteOptions()
            .AddRedirect("^account/apikeys", "settings/account/apikeys")
            .AddRedirect("^settings/organization/rooms(.*)", "settings/rooms$1"));

        app.UseDefaultFiles(new DefaultFilesOptions
        {
            DefaultFileNames = new List<string>
            {
                "index.md",
                "index.htm",
                "index.html"
            }
        });

        // .webmanifest is used to serve the correct version of the site icon / favicon.
        var provider = new FileExtensionContentTypeProvider();
        provider.Mappings[".webmanifest"] = "application/manifest+json";
        app.UseStaticFiles(new StaticFileOptions
        {
            ContentTypeProvider = provider
        });

        app.UseCookiePolicy();
        app.UseWebSockets();

        app.UseStatusCodePagesWithReExecute("/Status/{0}");

        app.UseRouting();

        app.UseCors();
        app.UseFeatureFlagMiddleware();
        app.UseAuthentication();
        app.UseStaffMode(); // Consumes data from Authentication and provides data to Authorization, so it should be between them.
        app.UseMiniProfiler(); // Mini profiler does it's own authn/authz
        app.UseAuthorization();

        app.UseMiddleware<OrganizationMiddleware>();
        app.UseMiddleware<BlogRedirectMiddleware>();
        app.UseEndpoints(endpoints => {
            endpoints.MapHealthChecks("/api/healthz").AllowAnonymous();
            endpoints.MapHangfireDashboard("/staff/jobs",
                    new DashboardOptions
                    {
                        AppPath = "/staff",
                        DashboardTitle = "Abbot Scheduled Jobs",

                        // We're using ASP.NET Core authorization, but we need to explicitly pass an empty array here.
                        // Otherwise Hangfire uses it's default authorization that only works for localhost.
                        Authorization = Array.Empty<IDashboardAuthorizationFilter>(),
                        AsyncAuthorization = Array.Empty<IDashboardAsyncAuthorizationFilter>(),
                    })
                .RequireAuthorization(AuthorizationPolicies.RequireStaffOrLocalDev)
                .RequireHost(AllowedHosts.Web);

            var nameSpaces = AbbotScriptOptions.NameSpaces;

            endpoints.MapLive(liveOptions);

            endpoints.MapMirrorSharp("/mirrorsharp",
                    new MirrorSharpOptions
                    {
                        SelfDebugEnabled = true,
                        IncludeExceptionDetails = true
                    }
                        .SetupCSharp(o => {
                            var analyzerReference =
                                new AnalyzerImageReference(
                                    ImmutableArray.Create<DiagnosticAnalyzer>(new ForbiddenAccessAnalyzer()));

                            o.AnalyzerReferences = o.AnalyzerReferences.Add(analyzerReference);
                            o.ParseOptions = CSharpParseOptions.Default
                                .WithLanguageVersion(AbbotScriptOptions.LanguageVersion);

                            o.MetadataReferences = AssemblyReferenceLocator.GetAllAssemblyReferences();
                            o.CompilationOptions = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                                .WithUsings(nameSpaces);

                            o.SetScriptMode(hostObjectType: typeof(IScriptGlobals));
                        }))
                .RequireHost(AllowedHosts.Web)
                .AllowAnonymous();

            endpoints.MapControllers(); // DO NOT CALL RequireHost here. Instead decorate every API Controller with
            // [AbbotWebHost] (except for the TriggerController).

            endpoints.MapRazorPages()
                .RequireHost(AllowedHosts.Web);

            endpoints.MapControllerRoute("default", "{controller:tolower=Home}/{action:tolower=Index}/{id?}")
                .RequireHost(AllowedHosts.Web);

            endpoints.MapSwagger()
                .RequireAuthorization(AuthorizationPolicies.RequireStaffOrLocalDev);

            if (AllowedHosts.All.Except(AllowedHosts.Web).ToArray() is [_, ..] nonWebHosts)
            {
                // Already handled by Razor Page for Web hosts
                endpoints.Map("/",
                        context => context.Response.WriteAsync(
                            $"Abbot {Program.BuildMetadata.InformationalVersion} - Commit {Program.BuildMetadata.CommitId}"))
                    .RequireHost(nonWebHosts);

                endpoints.Map("/Status/500",
                        ErrorPage.RequestDelegateFactory(env, loggerFactory))
                    .RequireHost(nonWebHosts);

                endpoints.Map("/Status/404",
                        context => context.Response.WriteAsync("404 Not Found"))
                    .RequireHost(nonWebHosts);
            }

            endpoints.Map("/staff/staff/boom", Boom);

            if (abbotOptions.Value.StaffAssetsPath is { Length: > 0 } staffAssetsPath)
            {
                staffAssetsPath = Path.Combine(HostEnvironment.ContentRootPath, staffAssetsPath);
                var staffAssetApp = endpoints.CreateApplicationBuilder()
                    .Use(async (context, next) => {
                        // Work around https://github.com/dotnet/aspnetcore/issues/24252
                        // Static files refuses to run when the Endpoint is set.
                        // https://github.com/dotnet/aspnetcore/issues/24252#issuecomment-663620294
                        context.SetEndpoint(null);
                        await next();
                    })
                    .UseStaticFiles(new StaticFileOptions()
                    {
                        FileProvider = new PhysicalFileProvider(staffAssetsPath),
                        // Endpoint routing doesn't strip the prefix off!
                        RequestPath = new PathString("/staff/assets"),
                        ServeUnknownFileTypes = true,
                        DefaultContentType = "application/octet-stream",
                    })
                    .Build();

                endpoints.Map("/staff/assets/{**path}", staffAssetApp)
                    .RequireAuthorization(AuthorizationPolicies.RequireStaffRole);
            }
        });
    }

    static async Task Boom(HttpContext httpContext) =>
        throw new InvalidOperationException($"({httpContext.Request.Method}) Staff, staff, :boom:");
}
