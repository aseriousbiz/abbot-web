# Serious.Slack.AspNetCore

Library with useful classes to respond to Slack requests within an ASP.NET Core application.

## Configuration

Make sure your App Settings have the following values:

```json
{
  "Slack": {
    "ClientId": "YOUR SLACK APP CLIENT ID",
    "SigningSecret": "YOUR-SLACK-SIGNING-SECRET",
  }
}
```

If you're using Azure App Service, the keys will be `Slack:ClientId` and `Slack:SigningSecret`.

## Startup.cs

At the top of `Startup.cs` make sure the `Serious.Slack.AspNetCore;` is imported:

```csharp
using Serious.Slack.AspNetCore;
```

Then in your `ConfigureServices` method, you'll call two methods:

1. `services.AddSlackRequestVerificationFilter()`,
2. `options.Filters.AddSlackRequestVerificationFilter()`

It'll look something like this:

```csharp
public void ConfigureServices(IServiceCollection services)
{
  // All your other services go here
  
  services.AddSlackRequestVerificationFilter();
  
  services.AddMvc().AddMvcOptions(options => {
        options.Filters.AddSlackRequestVerificationFilter();
        // Other filters go here.
  }));
}
```

Note: For your app, `services.AddMvc()` might be something else like `services.AddControllers()` or `services.AddRazorPages()`.