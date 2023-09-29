using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Serious.Abbot.Functions.DotNet;

public sealed class Program
{
    public static void Main()
    {
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        var host = new HostBuilder()
            .ConfigureFunctionsWorkerDefaults()
            .ConfigureLogging(logging => {
                logging.Configure(options => {
                    options.ActivityTrackingOptions = ActivityTrackingOptions.SpanId |
                                                      ActivityTrackingOptions.TraceId |
                                                      ActivityTrackingOptions.ParentId |
                                                      ActivityTrackingOptions.TraceState |
                                                      ActivityTrackingOptions.TraceFlags |
                                                      ActivityTrackingOptions.Tags |
                                                      ActivityTrackingOptions.Baggage;
                });
            })
            .ConfigureServices(services => {
                services.RegisterAbbotServices();
            })
            .Build();

        host.Run();
    }
}
