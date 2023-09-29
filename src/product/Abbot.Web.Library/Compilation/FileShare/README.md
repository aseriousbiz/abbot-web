# Azure File Assembly Cache

Compiled C# Skills are stored in an Azure File Share which is part of an Azure Blob Storage Account (configured in the Connection String `AbbotSkillAssemblyStorage`).

The name of the file share is configured in the AppSetting `AbbotAssemblyFileShare`.

The directory structure for the file share is:

```
{Platform}-{PlatformId}  // ex. Slack-T0123456789
          |
          +-{Skill Cache Key} // Hash of the code.
```

We have a schedule task via Hangfire that collects garbage assemblies. Garbage assemblies are assemblies that are no longer in user.

The starting point for accessing this cache is [`AzureAssemblyCacheClient`](./AzureAssemblyCacheClient.cs).