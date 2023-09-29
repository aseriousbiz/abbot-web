# Abbot Infrastructure

Abbot is primarily hosted on Azure. Here are the sites that make up Abbot.

Web Site                    | Type         | Resource Group       | DNS                                         
----------------------------|--------------|----------------------|---------------------------
`abbot-web`                 | App Service  | `serious-bots`       | https://app.ab.bot/ (website)
`abbot-web/stage`           | App Service  | `serious-bots`       | https://stage.ab.bot/
`abbot-skills-dotnet-main`       | Function App | `serious-bots-westus2-rg`       | <https://abbot-skills-dotnet-main.azurewebsites.net>
`abbot-skills-js-main`   | Function App | `serious-bots-linux-westus2-rg` | https://abbot-skills-js-main.azurewebsites.net
`abbot-skills-python-main`       | Function App | `serious-bots-skills-python` | https://abbot-skills-python-main.azurewebsites.net


Other important Azure resources

Resource                  | Type         | Resource Group       | Notes
--------------------------|--------------|----------------------|----------------------
`abbot-skills-keepalive`  | Function App | `serious-bots`       | Scheduled function to keep `abbot-skills-csharp` alive and well.

## DNS

Domain name for `ab.bot` is hosted on Hexonet (Check 1Password for login info).
All our DNS is managed at https://github.com/aseriousbiz/dns.

## External Web Hooks

We have some endpoints for external services to post to us. It's useful to document these.

URL                                 | Notes
----------------------------------- | --------------
https://app.ab.bot/billing-webhook  | Used by Stripe to send us subscription/billing events.
https://app.ab.bot/api/slack        | Called by Slack to send us Slack events and messages.
https://api.ab.bot/api/cli          | Used by the [abbot-cli](https://github.com/aseriousbiz/abbot-cli) command-line tool


## Feature Flags

To add someone to a feature flag, like `Conversations` (to enable Conversation management),
go to [`serious-bots-app-configuration` in the Azure Portal](https://portal.azure.com/#@aseriousbusiness.com/resource/subscriptions/114d4132-6977-430c-a803-38afcadd0e8b/resourceGroups/serious-bots/providers/Microsoft.AppConfiguration/configurationStores/serious-bots-appconfiguration/ff).
Select "Feature Manager" in the sidebar, and click the "..." icon on the right side of the feature you want to edit.
Select "Edit", and add a row to the "Groups" list at the bottom.
The group name is `domain:[slack domain]`, for example `domain:aseriousbiz.slack.com`.
Add the row and **then click "Apply"** at the bottom.

<img width="752" alt="image" src="https://user-images.githubusercontent.com/7574/160163262-6d087f5d-fb04-4fe0-8e1d-1ffc65204959.png">

The change may take a few minutes to apply, but no further action from you is needed.

## Certificates

We use Azure App Service Managed Certificates for our certs.
