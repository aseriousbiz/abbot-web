# Development Setup

Also check out [the development environments docs](./DEVELOPMENT.md)

A full development environment requires setting up some cloud services in Azure and Slack.

In these instructions, replace `{username}` with your GitHub username.

## Slack

Make sure you have an account with our Slack (aseriousbiz.slack.com) before doing this.

1. Copy our [slack app manifest](../config/Slack/manifest.json) file (located at `../config/Slack/manifest.json`)
2. In the `display_information` section, change `name` to `Abbot-{username}-Dev` to avoid conflicts.
3. In the `features` section, change `display_name` to `abbot-{username}-dev`.
4. In the `oauth_config` section, add your Auth0 domain to `redirect_urls`, e.g.
    - `https://aseriousbiz-{username}-dev.us.auth0.com/login/callback`
5. In the `settings` section (near the bottom of the manifest)...
   1. In the `event_subscriptions` sub-section, change the `request_url` to `https://abbot-{username}-dev-in.ngrok.io/api/slack.`.
   2. In the `interactivity` sub-section, change the `request_url` and `message_menu_options_url` settings to `https://abbot-{username}-dev-in.ngrok.io/api/slack.`.
6. [Create a new Slack App](https://api.slack.com/apps?new_app=1) using the manifest.
7. Make note of the **App ID**, **Client ID**, **Client Secret** and, **Signing Secret** for later.
    - If you created your own OAuth Application, you can now finish connecting to Slack.
8. To allow installing in other workspaces (but not publish to the App Directory)...
   1. Under **Manage distribution**, click **Distribute App**.
   2. Scroll down and complete the steps under **Share Your App with Other Workspaces**.
   3. Click **Activate Public Distribution**.

## Auth0

1. Sign up for [Auth0](https://auth0.com/) with Google SSO. Try to skip their onboarding flow.
2. From the top-right context menu, **Create Tenant** with Domain named `aseriousbiz-{username}-dev`.
3. **Create Application** named `Abbot {username} Dev` for **Regular Web Application**, then _Skip Integration_.
4. From the new Application's **Settings**:
    - Make note of the **Domain**, **Client ID**, and **Client Secret** for later.
    - Scroll down to update **Application URIs**:
      - **Allowed Callback URLs**
        ```text
        https://localhost:4979/callback, https://ipv4.fiddler:4979/callback
        ```
      - Allowed Logout URLs
        ```text
        https://localhost:4979/, https://ipv4.fiddler:4979/
        ```
    - Scroll to the bottom and **Save Changes**
5. Navigate to **Auth Pipeline > Rules** and **Create** an **Empty rule** named `Fetch User Profile Script` with the **Script** from [`config/Auth0/rule.js`](../config/Auth0/rule.js).
6. Navigate to **Authentication > Social** click **Create Connection** and select **Create Custom** at the bottom.
    1. Name: `slack` (casing is important, ask me how I know)
    2. Authorization URL: `https://slack.com/openid/connect/authorize`
    3. Token URL: `https://slack.com/api/openid.connect.token`
    4. Scope: `openid profile email`
    5. Client ID: Your Slack app's Client ID
    6. Client Secret: Your Slack app's Client Secret
    7. Fetch User Profile Script: Paste the content of [`config/Auth0/fetch-user-profile-SLACK.js`](../config/Auth0/fetch-user-profile-SLACK.js)
    8. Save and use "Try Connection" to verify that Auth0 returns your profile information like this:

```json
{
  "sub": "oauth2|slack|Uxxxxxxxxxx",
  "nickname": "<your nickname>",
  "name": "<your name>"
  "picture": "<your avatar>",
  "updated_at": "<some timestamp>",
  "https://schemas.ab.bot/platform_user_id": "Uxxxxxxxxxx",
  "https://schemas.ab.bot/version": "0.0.112",
  "https://schemas.ab.bot/platform_id": "T013108BYLS",
  "https://schemas.ab.bot/platform_name": "Serious Business",
  "https://schemas.ab.bot/platform_domain": "aseriousbiz.slack.com",
  "https://schemas.ab.bot/platform_avatar": "https://avatars.slack-edge.com/2021-02-19/1758280923911_ab828c0314ecd23af8bc_68.png"
}
```

7. Navigate to your `Abbot {username} Dev` application and select the **Connections** tab. Disable `Username-Password-Authentication` and `google-oauth2` (leave `Slack` enabled)

## ngrok

Log in to [ngrok.com](https://dashboard.ngrok.com) with your Google `@aseriousbusiness.com` account. This should give you a Pro account.

The `bootstrap` script should:
1. Request your [auth token](https://dashboard.ngrok.com/get-started/your-authtoken)
2. Configure a Docker image to run `ngrok` tunnels to receive webhooks (aka it runs something equivalent to `ngrok http https://localhost:4979 -subdomain=abbot-{username}-dev`)

## GitHub

To use the GitHub integration, you need to configure a [GitHub App](https://docs.github.com/en/developers/apps).

1. Create a new GitHub App in [your settings](https://github.com/settings/apps), or in [`aseriousbiz`](https://github.com/organizations/aseriousbiz/settings/apps) if you prefer (and have permissions).
    - App Name: `Abbot Hub ({username} Dev)`
    - Homepage URL: `https://ab.bot/`
    - Callback URL: `https://localhost:4979/github/install/complete`
      - Leave **Expire user authorization tokens** checked
    - Setup URL (optional): `https://localhost:4979/github/install/complete`
    - Webhook: uncheck **Active**
    - Repository permissions:
      - **Issues**: Read and write
      - **Metadata** (mandatory): Read-only
    - Where can this GitHub App be installed? **Any account**
2. Once the app has been created, collect secrets for [Environment Setup](#environment-setup):
    - Make note of **App ID** and **Client ID**
    - **App Slug** is the normalized name at the end of the **Public link**.
    - **Generate a new client secret** and copy/save to use as **Client Secret**
    - **App Name** is only for display purposes in Abbot
    - **Generate a private key** and make note of the downloaded `.private-key.pem` file name and location
3. Delete the `.private-key.pem` once your user secret is set

Now run Abbot locally and [configure the GitHub integration](https://localhost:4979/settings/organization/integrations) 
and click Install. You will be prompted to choose where to install and for which repositories.

## HubSpot

To use the HubSpot integration, you need to configure the client secret for the Development HubSpot OAuth Client.
This client secret is not used in production, but you should still take care in where you share it.
If we have to reset it, it will be a pain ;).

Find the client secret in 1Password in the Shared vault entry named "HubSpot Local Dev OAuth".
The password for this entry is the Client Secret, you'll use it in the "Environment Setup" step.
You don't need to set the Client ID, that's already stored in appsettings.Development.json.
This client _requires_ that your site is listening on  `https://localhost:4979`.

_If you're setting Hubspot up after having run the "Environment Setup" step, run the following command:_

> script/user-secrets set HubSpot:ClientSecret $HubSpot_ClientSecret

In a browser, log in to https://app.hubspot.com/ with your @aseriousbusiness.com Google account.
You should end up in our Developer Account, from there you can create your own test CRM Accounts.
Go to the "Testing" tab in the top nav then "Create app test account", which should give you a new
account (call it "ASB Test Account {your-username}")

Now run Abbot locally and [configure the Hubspot integration](https://localhost:4979/settings/organization/integrations) 
and click Install. When you're prompted to log into Hubspot, select the "ASB Test Account {your-username}" account you
just created.


## Zendesk

To use the Zendesk integration, you need to configure the client secret for the Development Zendesk OAuth Client.
This client secret is not used in production, but you should still take care in where you share it.
If we have to reset it, it will be a pain ;).

Find the client secret in 1Password in the Shared vault entry named "Zenesk Local Dev OAuth".
The password for this entry is the Client Secret, you'll use it in the "Environment Setup" step.

_If you're setting Zendesk up after having run the "Environment Setup" step, run the following command:_

> script/user-secrets set Zendesk:ClientSecret $Zendesk_ClientSecret

Also make sure your `Abbot:PublicHostName` is set to `abbot-{username}-dev.ngrok.io`.

> script/user-secrets set Abbot:PublicHostName abbot-{username}-dev.ngrok.io

You don't need to set the Client ID, that's already stored in appsettings.Development.json.
This client _requires_ that your site is listening on  `https://localhost:4979`.

When you [configure the Zendesk integration](https://localhost:4979/settings/organization/integrations/zendesk), use `d3v-aseriousbusiness` as the subdomain.

You'll need to be invited by @analogrelay to our Zendesk account. When you get to the login form, click "I am an agent" to create your account.

## Azure

Make sure you have an account with aseriousbusiness.com on Azure. Talk to @pmn or @haacked if you don't.

Log in with your Serious Business account to https://portal.azure.com/

1. Create a resource group named `serious-bots-{username}-dev`. For example, mine is `serious-bots-haacked-dev`.
2. Create a new `Azure Bot` resource. **Review these carefully** as they cannot be changed once the Bot is created, you'll have to delete and recreate it.
    1. Bot Handle: `abbot-bot-{username}-dev`.
    2. Subscription: `Corp Subscription`
    3. Resource Group: `serious-bots-{username}-dev` (The one you created in step 1.)
    4. Location: `West US 2` (Probably doesn't matter, but West is closer to our app server).
    5. Pricing Tier: `F0 Free`
    6. Type of App: `Multi Tenant`


### Environment Setup

You'll need to override some of Abbot's settings to point at your bot.
We can use the `dotnet user-secrets` command to set overrides for any of the settings in `appSettings.json` and `appSettings.Development.json`.

First, set these variables:

```bash
AbbotUserName=
AbbotBotName=abbot-bot-$AbbotUserName-dev
PublicHostName=abbot-$AbbotUserName-dev.ngrok.io
PublicIngestionHostName=abbot-$AbbotUserName-dev-in.ngrok.io
PublicTriggerHostName=abbot-$AbbotUserName-dev-run.ngrok.io
Auth0_Domain=
Auth0_ClientId=
Auth0_ClientSecret=
OpenAI_ApiKey= # Go to https://platform.openai.com/account/api-keys to get one of these.
Slack_AppId=
Slack_ClientId=
Slack_ClientSecret=
Slack_SigningSecret=
# Optional
GitHub_AppId=
GitHub_AppName=
GitHub_AppSlug=
GitHub_AppKeyPath=~/Downloads/$GitHub_AppSlug.$(date +%F).private-key.pem
GitHub_ClientId=
GitHub_ClientSecret=
Zendesk_ClientSecret=
HubSpot_ClientSecret=
```

Then set the user secrets (from the root of the repo):

```
script/user-secrets set BotName $AbbotBotName
script/user-secrets set Abbot:PublicHostName $PublicHostName
script/user-secrets set Abbot:PublicIngestionHostName $PublicIngestionHostName
script/user-secrets set Abbot:PublicTriggerHostName $PublicTriggerHostName
script/user-secrets set MicrosoftAppId $MicrosoftAppId
script/user-secrets set MicrosoftAppPassword $MicrosoftAppPassword
script/user-secrets set Auth0:Domain $Auth0_Domain
script/user-secrets set Auth0:ClientId $Auth0_ClientId
script/user-secrets set Auth0:ClientSecret $Auth0_ClientSecret
script/user-secrets set OpenAI:ApiKey $OpenAI_ApiKey
script/user-secrets set Slack:AppId $Slack_AppId
script/user-secrets set Slack:ClientId $Slack_ClientId
script/user-secrets set Slack:ClientSecret $Slack_ClientSecret
script/user-secrets set Slack:SigningSecret $Slack_SigningSecret
script/user-secrets set GitHub:AppId $GitHub_AppId
script/user-secrets set GitHub:AppName $GitHub_AppName
script/user-secrets set GitHub:AppSlug $GitHub_AppSlug
script/user-secrets set GitHub:AppKey "$(cat $GitHub_AppKeyPath)"
script/user-secrets set GitHub:ClientId $GitHub_ClientId
script/user-secrets set GitHub:ClientSecret $GitHub_ClientSecret
script/user-secrets set Zendesk:ClientSecret $Zendesk_ClientSecret
script/user-secrets set HubSpot:ClientSecret $HubSpot_ClientSecret

# Confirm everything is configured as expected
script/user-secrets list
```
