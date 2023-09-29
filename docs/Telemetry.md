# Telemtry in Abbot

## Tl;dr - "The Rules"

* **DO** Use OpenTelemetry APIs and Microsoft.Extensions.Logging
* **DO NOT** install new App Insights packages without checking if it's absolutely necessary

### Logging

* **DO** use `ApplicationLoggerFactory.CreateLogger<MyType>()` to get a logger.
* **DO NOT** use `LogInformation`/`LogWarning`/`LogError`/`LogDebug`/`LogTrace` to log.
* **DO** define logging extension methods in a type unique to the place they are used (i.e. `MyComponentLoggingExtensions` for `MyComponent`)
* **DO** give each event a unique event ID (within the "Category", i.e. the `T` of `ILogger<T>`).
* **DO** give each event a unique method name.
* **DO** use `PascalCase` in logger message placeholders.
* **DO** use `camelCase` in event method parameter names.
* **DO** take advantage of "scopes" to set global values
* **DO NOT** re-log information provided by a scope see [Things you don't need to log](#things-you-dont-need-to-log) below.
* **NOTE** The `LoggingExtensions` class in `Abbot.Common.Logging` predates this guidance. When in conflict, follow this guidance for new code.

### Metrics

* **DO* use `AbbotTelemetry.Meter.CreateHistogram` or `AbbotTelemetry.Meter.CreateCounter` to create metrics

### Traces

* **DO** use `AbbotTelemetry.ActivitySource.StartActivity` to wrap key pieces that should appear in the end-to-end transaction timeline in App Insights
* **DO NOT** wrap each method in an activity. Use some judgement to determine which activities justify Activities.

## OpenTelemetry

We use the [OpenTelemetry standard](https://opentelemetry.io/docs/what-is-opentelemetry/) to emit our operations telemetry (business telemetry is usually done through Segment, but that line is blurry).
OpenTelemetry defines three main "Signals" of telemetry:

* [Logs](https://opentelemetry.io/docs/concepts/signals/logs/) - Timestamped structured records of individual events with a textual message.
* [Metrics](https://opentelemetry.io/docs/concepts/signals/metrics/) - A measurement about a service, captured at runtime.
* [Traces](https://opentelemetry.io/docs/concepts/signals/traces/) - An overview of the "big picture" for a full end-to-end transaction, such as a Request.

We use the following APIs to instrument our application with each of these:

* [Logs - `Microsoft.Extensions.Logging`](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging?tabs=command-line)
* [Metrics - `System.Diagnostics.Meter`](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics-instrumentation)
* [Traces - `System.Diagnostics.Activity`](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing-instrumentation-walkthroughs)

In general, we **avoid** usage of platform-specific telemetry APIs like App Insights.
This allows us to easily include telemetry from **any library or service** that supports OpenTelemetry.
OpenTelemetry has become the industry standard for telemetry and other telemetry products are quickly adopting it.
We use the Azure Monitor Exporter for .NET to take the OpenTelemetry events we generate in-process and export them to App Insights.

## Logging with M.E.L

Logging is a key way we understand the behavior of our system.
We use the [.NET Core Logging infrastructure](https://docs.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-6.0) provided via `ILogger` and `ILoggerFactory`.
This is sometimes called `Microsoft.Extensions.Logging`, or `M.E.L`.
At any location in Abbot.Web, you can access a logger using `ApplicationLoggerFactory`.
Logging is such a critical piece of cross-cutting functionality that we make it available via a global static rather than using DI (though you can also use DI to inject `ILogger<T>`/`ILoggerFactory` as usual).
As a standard practice, any type that intends to log messages should use a `static readonly` property to fetch the logger once:

```csharp
public class MyType
{
    static readonly ILogger<MyType> Log =
        ApplicationLoggerFactory.CreateLogger<MyType>();
}
```

Once you have a logger, you can log messages to it.
We use the [compile-time logging source generation](https://docs.microsoft.com/en-us/dotnet/core/extensions/logger-message-generator) feature to pre-generate high-performance logging methods.
When you want to define a new event to be logged, add a `static partial class` next to the type in which you are logging to hold the events

```csharp
public class MyComponent
{

}

partial static class MyComponentLoggingExtensions
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Error,
        Message = "Something bad happened to foozle '{FoozleName}'")]
    public static partial void BadThingHappenedToFoozle(
        this ILogger<MyComponent> logger,
        Exception exception,
        string foozleName);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Information,
        Message = "Something good happened to foozle '{FoozleName}'")]
    public static partial void GoodThingHappenedToFoozle(
        this ILogger<MyComponent> logger,
        string foozleName);
}
```

Then, to log this event, use your `ILogger` instance and call the extension method:

```csharp
public void DoFoozleThing()
{
    try
    {
        foozle.Do();
        Log.GoodThingHappenedToFoozle(foozle.Name);
    }
    catch(Exception ex)
    {
        Log.BadThingHappenedToFoozle(ex, foozle.Name);
    }
}
```

Note that the message has format placeholders (`{FoozleName}`).
These serve two purposes:
First, they interpolate the values of parameters passed into the method into the message.
Second, they define structured key-value pairs that can be queried later.
The matching of format placeholders to parameter names is **case-insensitive** so you can and should use `PascalCase` for the format placeholder and `camelCase` for the parameter name.
If the "foozle name" was `bar`, then the message would look like: `Something bad happened to foozle 'bar'`.
Plus, when querying for the event in Application Insights, you could query the `FoozleName` value directly:

```kusto
traces
| where customDimensions.FoozleName == "bar"
```

Event IDs are not really necessary since Event Names can be used to look up events just as easily, and they are symbolic and easier to remember.
However, the logging analyzers require a unique Event ID for every method in a given type.
Since we generally have one type containing all the events for a specific "category" (the `T` in `ILogger<T>`), we can just use a simple monotonically-incrementing number.
Event Names are automatically generated from method names.
By keeping all our Logging Messages in the same type, we ensure Event Names are most likely to be unique.

## Scopes

Scopes give us a way to attach key-value pairs to every log within a lexical scope.
For example:

```csharp
async Task DoSomethingAsync()
{
    // The "_logger" here can be _any_ logger in the system.
    _logger.LogInformation("Doing the thing");
    // ...
}

_logger.LogInformation("Finding organization...");
var organization = await FindOrganizationAsync(...);
using(var scope = _logger.BeginScope("Organization ID: {OrganizationId}", organization.Id))
{
    await DoSomethingAsync();
    _logger.LogInformation("Did the thing");
}
_logger.LogInformation("Outside the scope");
```

In this example, the key-value pairs associated with the logs would look something like this (this is just pseudo-output, not actual console logger output):

```
Finding organization...
[OrganizationId = 42] Doing the thing
[OrganizationId = 42] Did the thing
Outside the scope
```

Take advantage of scopes where reasonable!
We create scopes in the following places automatically:

1. [.NET creates a "fake" logger scope _automatically_ at the top level](https://github.com/dotnet/runtime/blob/2306813eaf2066fe63cb4766572fc68e80a24ef7/src/libraries/Microsoft.Extensions.Logging/src/LoggerFactoryScopeProvider.cs) containing information from `Activity.Current`
2. [ASP.NET creates a scope for every request containing the "RequestId" and "RequestPath"](https://github.com/dotnet/aspnetcore/blob/2cfadbbccff1109b65dcee717dc1fa9c08f57bd6/src/Hosting/Hosting/src/Internal/HostingApplicationDiagnostics.cs#L442) - Note that this Request ID _is not the one we report to customers_.
3. We create a scope before `MetaBot` runs with the _Bot Framework_ Activity ID (we call this `TurnActivityId`) and Activity Type.
4. We create a scope inside `MetaBot` before running any `IPayloadHandler`, `IHandler` or skill containing the `PlatformTeamId`, `PlatformUserId` of the sender, `PlatformRoomId` in which the messsage was received (if any), `EventMessageId` of the message (if any).
5. We create a scope before launching any background job with the `JobId`, and the Type, Assembly and Method name of the job.

### Things you don't need to log

Because of scopes, you generally don't need to include the following in your log messages, because they will already be there:

1. Organization, User and Room ID of the triggering messsage (Exception: In `SlackPlatformMessageFactory`, which is outside the scope).
2. Activity ID, Request ID, Trace "Identifiers"

## Querying Logs

Query logs by going to `abbot-insights` in the portal.
Log messages are available in the "Logs" tab on the sidebar, in the "traces" table.
Unless you know you've filtered the time window to a small-enough range, we recommend adding `| sample 1000` to the end of your query the first time you run it.
This causes Kusto to select 1000 "random" rows to display.
This is a very fast operation for Kusto and allows you to preview the results you get back before running a possibly-long query.
Note that, when using `sample`, the results are **not** ordered at all.

We highly recommend the following "base" query, on which you can add additional filters:

```kusto
traces 
| extend EventName = customDimensions.EventName, CategoryName = customDimensions.CategoryName, RequestId = customDimensions.RequestId
```

This query adds `EventName`, `CategoryName`, and `RequestId` as columns that can be easily filtered by further query clauses.

For example, to find all instances of the Event named `ReceivedUserEvent` from the `Serious.Abbot.Infrastructure.SkillRouter` logs:

```kusto
traces 
| extend EventName = customDimensions.EventName, CategoryName = customDimensions.CategoryName, RequestId = customDimensions.RequestId
| where CategoryName == "Serious.Abbot.Infrastructure.SkillRouter" and EventName == "ReceivedUserEvent"
```

Or all logs related to Request ID `12003456-0000-aaaa-bbbb-ffffffffffff`:

```kusto
traces 
| extend EventName = customDimensions.EventName, CategoryName = customDimensions.CategoryName, RequestId = customDimensions.RequestId
| where RequestId == "12003456-0000-aaaa-bbbb-ffffffffffff"
```

## Metrics

Metrics are a great way to capture high-level information about system usage and performance.
Emitting a metric value is _much_ "cheaper" than writing a log event.
Metrics are aggregated **in-process**, meaning the Sum, Min, Max, Average and any percentiles are computed in-memory **before** transmitting to the telemetry client.

OpenTelemetry defines three main kinds of Metric:

* Counter - A value that accumulates over time. It can only ever go up. This is further broken down into "Asynchronous Counters", "Up/Down Counters", and "Asynchronous Up/Down Counters"
  * The Odometer in a car is a Counter
* Gauge - An instantaneous reading of a value that varies over time.
  * The Fuel Gage in a car is a Gauge
* Histogram - A client-side aggregated value. Good for when you emit a lot of values and want to see percentiles and aggregates over it.

Most of our metrics will be `Counter`s (to track the count of events) or `Histogram`s (to track latencies, or other attributes about each event).

Define a metric with the `AbbotTelemetry.Meter.CreateNNN` APIs, this API takes the name of the metric and the units as parameters:

```csharp
_redactedEntitiesMetric = AbbotTelemetry.Meter.CreateHistogram<int>("ai.redaction.redacted-entities", "entities");
```

Then, when the event you want to record occurs, call `.Record` on that object.
You can specify key-value pair "tags" to associate with the metric.
We'll get a separate aggregation for _each combination of tag values_.
You need to consider the "cardinality" (the number of possible values) for a tag.
Since each combination of tag values produces a new record in the telemetry store, you should limit tag values to discrete low-cardinality values (avoid things like "string length" as a tag value).

```csharp
// Note: Organization is a great example of a "high-cardinality" tag that we should avoid, but while we have few organizations, we're using it.
_redactedEntitiesMetric.Record(entities.Count, new TagList() {
    {"Organization", org.PlatformId },
});
```

## Traces / Activities

Activities allow you to define a "span" of time in a distributed trace.
Think of an activity as tracking a single "function call" in a profile, but at a higher-level than actually tracking each call in the callstack (which is costly).
Create an activity using the `AbbotTelemetry.ActivitySource.StartActivity` API.
The result is an `IDisposable` that should be disposed when the activity completes.

```csharp
using var activity = AbbotTelemetry.ActivitySource.StartActivity($"{nameof(SkillCompiler)}:CreateScript");
script = new DotNetScript(CSharpScript.Create<dynamic>(code, globalsType: typeof(IScriptGlobals),
    options: options));
```

A few notes:

* The Activity Name parameter is marked with `[CallerMemberName]` so if you omit it, it will get the simple name of the method you call it from. This is often not granular enough for us though, so please specify a useful name.
* You should select an appropriate `ActivityKind`. The _default_ kind is `ActivityKind.Internal` which is _usually_ what we want:
  * `Internal` - The activity represents an entirely internal process within the processing of a request
  * `Server` - The activity represents the receipt of an incoming request (for example, ASP.NET Core starts one of these when a request comes in, all our activities within a request are _descendants_ of this activity.)
  * `Client` - The activity represents an outgoing request to another process (this includes calls to our own internal services, and to external services like Slack)
  * `Producer` - The activity represents a Producer _emitting_ an event to be processed (MassTransit uses these for publishing messages, for example)
  * `Consumer` - The activity represents a Consumer _receiving_ an event to be processed (MassTransit uses these for consuming messages, for example)
* .NET will automatically handle flowing parent activity links.
* Use APIs on the activity to set well-known OpenTelemetry tags. For example `activity.SetStatus` to set the "result" of the activity (Success/Fail, with a description).
