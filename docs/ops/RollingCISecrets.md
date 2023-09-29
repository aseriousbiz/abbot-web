# Rolling CI Secrets

We have two major CI secrets that need to be rolled frequently:

1. Cloudflare token for DNS/Pulumi
2. Azure credentials for Pulumi, deployment, etc.

## Cloudflare token

To roll the Cloudflare token, log in to the Cloudflare dashboard (ping #engineering for an account).
Click the user icon in the top-right and select "My Profile".
Navigate to "API Tokens"
Create a new token with the following settings:

* Name: "Edit Zone DNS" (or similar)
* Permissions:
  * Zone, Zone, Edit
  * Zone, DNS, Edit
* Zone Resources:
  * Include, All Zones
* Client IP Address Filtering: None
* TTL: 6 months from today

Then, take the token and update the `CLOUDFLARE_TOKEN` Organization-level GitHub Actions Secret with the appropriate value.
Finally, ask SlackBot to remind #engineering to do this again about 15 days before the token is due to expire.

## Azure AD token

Navigate to the [deploy-bot Azure AD App Registration](https://portal.azure.com/#view/Microsoft_AAD_RegisteredApps/ApplicationMenuBlade/~/Credentials/appId/0072f48c-6b34-4f54-b950-1f1c8c16b60e/isMSAApp~/false) in the portal.
Go to "Certificates and Secrets".
Create a new token, with the default 6 month expiry.
Take the token and update the `DEPLOYBOT_AZURE_AD_CLIENT_SECRET` Organization-level GitHub Actions Secret.
Finally, ask SlackBot to remind #engineering to do this again about 15 days before the token is due to expire.
