using Microsoft.Extensions.Configuration;
using Segment;
using Serious;
using Serious.Abbot.Infrastructure.Telemetry;

namespace Microsoft.Extensions.DependencyInjection;

public static class AnalyticsStartupExtensions
{
    public static void AddAnalyticsServices(this IServiceCollection services, IConfiguration analyticsConfigSection)
    {
        var writeKey = analyticsConfigSection.Get<AnalyticsOptions>()?.SegmentWriteKey;
        var segmentWriteKey = writeKey.Require($"Analytics:{nameof(AnalyticsOptions.SegmentWriteKey)} not found!");

        Analytics.Initialize(segmentWriteKey);
        services.AddSingleton<IAnalyticsClient>(Analytics.Client);

        services.Configure<AnalyticsOptions>(analyticsConfigSection);
    }
}
