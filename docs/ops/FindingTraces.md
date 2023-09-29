# Finding Exception Details

So somehow, you got a request ID (like `00-0632194efe00b0c2c12ad6b7f3282525-67e28473b160fcc8-00`) and you need to find logs and other detail for it?
You've come to the right place.

## Understanding Activity IDs

We use [`Activity.Current.Id`](https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.activity.id?view=net-6.0) to generate our tracing identifiers.
These values are [W3C "Traceparent" values](https://www.w3.org/TR/trace-context/#traceparent-header).
There are four parts to the identifier:

```
<version>-<trace-id>-<parent-id>-<trace-flags>
```

* `<version>` is the version of the traceparent value. Currently this is always `00`.
* `<trace-id>` is the ID of the entire "trace forest". Think of it as a global identifier for a user-initiated request. All logs, exceptions, etc. from _all services_ involved in the request should have the same `<trace-id>`.
* `<parent-id>`, aka "Span ID". This is the ID of the _current operation_. A single `<trace-id>` _might_ have multiple `<parent-id>`s within it. For example, a call from service `A` to service `B` as part of servicing a request would yield a new `<parent-id>` but _not_ a new `<trace-id>`
* `<trace-flags>` are special flags to mark the trace session as sampled, or set a trace level. We do not use this field.

In our logging, **most** log events are tagged with the following keys:

* `TraceId` - The `<trace-id>` value
* `SpanId` - The `<parent-id>` value
* `TraceIdentifier` - The full Activity ID / Trace Parent value

**NOTE**: `TraceIdentifier` and `TraceId` are _not_ equivalent. Logs for the same logical request (i.e. same `TraceId`) may have different `TraceIdentifier` values, because the latter includes the `SpanId` value which can change throughout a request.

To find exceptions and logs for a request then, we want to extract the `<trace-id>`.
That gives us the "top level" identifier for a request.
As we expand our tracing and services, the `<trace-id>` should always represent logs/exceptions/traces related to a _single request_.

## Finding logs and such

All our production logs end up in the [abbot-logs-prod](https://portal.azure.com/#@aseriousbusiness.com/resource/subscriptions/114d4132-6977-430c-a803-38afcadd0e8b/resourceGroups/serious-bots/providers/Microsoft.OperationalInsights/workspaces/abbot-logs-prod/Overview) Log Analytics Workspace.
You can also go to the `abbot-insights` App Insights resource, which is how logs are ingested, but it's best to just go to the backend directly.
The Log Analytics Workspace aggregates App Insights and any other logs we want to collect (for example, Database logs).

To find the logs **and** exceptions, we need to query two tables in Kusto: `AppTraces` (logs) and `AppExceptions` (exceptions).
An important note: **If you provide an `Exception` parameter to an `ILogger` method**, then the logs go to `AppExceptions`, and **not** `AppTraces`.

We provide a handy "Function" in Azure Log Analytics called `GetAllLogs` to allow you to quickly query all the logs for any environment, and ensure useful properties are extracted from the property bag and queryable.
All the key-value pairs provided when logging an event or exception (including from scopes) are available in the `Properties` column.
This column has the [Kusto type `dynamic`](https://docs.microsoft.com/en-us/azure/data-explorer/kusto/query/scalar-data-types/dynamic) which means querying generally requires "promoting" the individual keys to real columns using the [`extend` operator](https://docs.microsoft.com/en-us/azure/data-explorer/kusto/query/extendoperator) and specifying the type of the value using a function like [`tostring`](https://docs.microsoft.com/en-us/azure/data-explorer/kusto/query/tostringfunction).

The following base query will give you the latest log events in reverse-chronological order (newest first)

```
GetAllLogs("Production")
| order by TimeGenerated desc
```

From there, you can apply any filters you need.
For example, if you want to see all logs related to the request with Trace ID `0632194efe00b0c2c12ad6b7f3282525` in _reverse-chronological order_ (newest first), you could use this query:

```
GetAllLogs("Production")
| where TraceId == "0632194efe00b0c2c12ad6b7f3282525"
| order by TimeGenerated desc
```

You can also specify a different environment using the parameter to `GetAllLogs`, for example to query `Canary` logs:

```
GetAllLogs("Canary")
| order by TimeGenerated desc
```

_Find [docs on the Kusto Query Language on Microsoft Docs](https://docs.microsoft.com/en-us/azure/data-explorer/kusto/query/)_

**MOST** Logs should be tagged with `TraceId` and `SpanId`
