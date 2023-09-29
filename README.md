# Abbot

A Serious Bot for [A Serious Business, Inc.](https://www.aseriousbusiness.com/).

https://app.ab.bot/

## TL;DR

- `script/bootstrap` - Setup dependencies (see below for details)
- `script/build` - Build everything
- `script/run` - Run the .net skill runner and watch for changes
- `script/server` - Run the website and watch for changes
- `script/watch` - Run the website and the dotnet skill runner and watch for changes

## Quick notes

**Interested in using Codespaces?** Check out [our Codespaces docs](./docs/CODESPACES.md)

Also check out [the development environments docs](./docs/DEVELOPMENT.md)

### Adding Migrations

To add a migration, run `./script/ef-migrations-add {MigrationName}` (Replacing `{MigrationName}` with the descriptive name of your migration) from anywhere in the repository.

### Bumping versions

Versions are automatically generated based on a fixed `Major.Minor` part, plus the commit height since the last time the version got bumped, and an optional prerelease prefix. This is done by the Nerdbank.Gitversioning package that is automatically included as part of the build process via the root [Directory.Build.props](Directory.Build.props) file.

The fixed part and suffix are configured in version.json files, one for each area that we want to track versions separately:
- `src/product`: Edit the [version.json](version.json) file at the root of the repo. This is the version reported in our UI.
- `src/scripting`: Edit the [src/scripting/version.json](src/scripting/version.json) file.
- `src/functions`: Edit the [src/functions/version.json](src/functions/version.json) file.
- `src/libraries`: Edit the [src/libraries/version.json](src/libraries/version.json) file.

To bump the major or minor parts of a project, edit the corresponding version.json file, then *commit* the change.
The version bump will only be seen in a build after files have been commited.

Not all edited files will trigger a version bump. The list of files that are excluded from triggering a version bump are in version.json files as well:
- `src/product`: [src/product/version.json](src/product/version.json) file.
- `src/scripting`: [src/scripting/version.json](src/scripting/version.json) file.
- `src/functions`: [src/functions/version.json](src/functions/version.json) file.
- `src/libraries`: [src/libraries/version.json](src/libraries/version.json) file.

Nerdbank.Gitversioning doesn't currently support wildcards in path filters, and path filters need to be in a version.json at the level that controls a version bump (i.e. if we want multiple projects to all be bumped together if one of them has changes, then a version.json file needs to exist in a parent folder, and all the exclusions need to be at that level as well). If you want to exclude a file or folder from triggering a version bump, add it to the corresponding version.json file. The exclusion syntax is `:!path/to/exclude`.

## Overview

Abbot consists of a series of Azure Functions and Web Applications. Some are hosted in other repositories.

* `Abbot.Web`
* `Abbot.Functions.DotNet` [README](src/functions/Abbot.Functions.DotNet/README.md)
* `abbot-skills-javascript` [README](https://github.com/aseriousbiz/abbot-js)
* `abbot-skills-python` [README](https://github.com/aseriousbiz/abbot-py)
* `Abbot.Functions.KeepAlive` [README](https://github.com/aseriousbiz/abbot-keepalive)

All of these sites are continuously deployed when commits are pushed or merged into the `main` branch.

## Prerequisites to run locally

* [scoop](https://scoop.sh) (Windows)
* [Homebrew](https://brew.sh/) (Mac)

The `script/bootstrap` will set up the following for you:

* [Docker Desktop](https://www.docker.com/products/docker-desktop) _NOTE: Do not install this from `brew` or `scoop`._
* .NET 6.0.100
* [Azure Functions Core Tools](https://www.npmjs.com/package/azure-functions-core-tools)
* [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli)
* nodenv + node (16.x, whatever is set in .node-version)
* Postgres + pgadmin on Docker
* ngrok on Docker

You will also need access to [our Azure Portal](https://portal.azure.com/) (log in with your `@aseriousbusiness.com` Microsoft account).

### ngrok

Before anything else, you'll need to set up an ngrok account and redirection.

1. Log in to [ngrok.com](https://dashboard.ngrok.com) with your Google `@aseriousbusiness.com` account. This should give you a Pro account.
2. Copy **Your Authtoken** to use with `script/bootstrap`.

### Stripe

To test billing, you'll need to set up a local listener for Stripe webhooks.

1. Install the Stripe CLI - `brew install stripe/stripe-cli/stripe`
2. Login to Stripe - `stripe login` (use your @aseriousbusiness.com account)

And then every time you run the site locally to test billing, run:

```bash
stripe listen --forward-to https://localhost:4979/billing-webhook
```

This will return a Webhook secret which needs to be set in User Secrets:

```bash
script/user-secrets set Stripe:WebhookSecret whsec_{REDACTED}
```

Alternatively, since we use ngrok, you can go to https://dashboard.stripe.com/test/webhooks to set up a persistent webhook pointing at your ngrok URL. For example, it'll be something like:

```
https://abbot-{USERNAME}-dev.ngrok.io/billing-webhook
```

You'll need to reveal the webhook secret and run the command to set `Stripe:WebhookSecret` to it in User Secrets.

### Build and Run

Note: If you don't want to build and run everything locally, you can use Docker to host most things.

Note: You can't run skill runners in Docker in M1 Macs.

1. The `script/bootstrap` script should have installed Docker for you. Install Docker Desktop [from the website](https://www.docker.com/products/docker-desktop) if you don't have it. (_`brew` and `scoop` can't be trusted for this yet._)
1. After cloning the repository, run `script/bootstrap` from the root to set up prerequisites and run them in docker.
1. Run the skill runners in Docker if you don't need to work on them locally:
	1. Run `script/docker-run-dev -dotnet -l -p` for the dotnet skill runner.
	1. Run `script/docker-run-dev -python -l -p` for the python skill runner.
	1. Run `script/docker-run-dev -js -l -p` for the js skill runner.
1. Run `script/build` to build all the projects.
1. Run `script/server` to run the website (it will rebuild if you change)

Once the website is up and running, **if you're running any skill runners in Docker**, go to `https://localhost:4979/admin/runners` to configure the endpoints:

- .NET Endpoint
	- Url: `http://localhost:7071/api/SkillRunnerSecured`
	- Token: `skillkey`

- Python Endpoint:
	- Url: `http://localhost:7072/api/SkillRunner`
	- Token: `skillkey`

- JS Endpoint:
	- Url: `http://localhost:7073/api/SkillRunner`
	- Token: `skillkey`

### Fully local environment

After cloning the repository, run `script/bootstrap` from the root to set up prerequisites.
Run `script/build` to run the website and the proxy project.
Afterwards run `script/watch` to run all sites for end-to-end testing
If you just need to work on the website, you can run `script/server` instead.

### Hangfire Schema

Hangfire.io creates a Hangfire schema in the db. It seems we run into issues where there's not the correct permissions. Here's what I've run to fix it:

```sql
GRANT ALL ON SCHEMA hangfire TO abbotuser
GRANT ALL ON ALL TABLES IN SCHEMA hangfire TO abbotuser
GRANT ALL ON ALL SEQUENCES IN SCHEMA hangfire TO abbotuser
GRANT ALL ON ALL FUNCTIONS IN SCHEMA hangfire TO abbotuser
```

### Libraries

Abbot is broken into several libraries.

* `Abbot.Common` [README](src/product/Abbot.Common/README.md)
* `Abbot.Scripting.Interfaces` [README](src/product/Abbot.Scripting.Interfaces/README.md)
* `Abbot.Web.Library`
* `Serious.Library` - A serious set of useful code
* `Serious.Razor` [README](src/product/Serious.Razor/README.md)
[* `Serious.Slack.Messages` Types that represent Slack messages as well as a [Refit](https://github.com/reactiveui/refit) based Slack API client. This can be shared between Abbot.Functions and Abbot.Web.
]()
## Docs

Find [developer documentation in the `docs/` folder](docs/).

## Backlog

We try to keep our issue tracker clean. If there's an issue we don't think we'll get to any time soon, but think is worth keeping around, we label it with the `backlog` label and close it.

[__View our backlog__](https://github.com/aseriousbiz/abbot/issues?q=is%3Aissue+label%3Abacklog+is%3Aclosed).

## Updating Emojis

To map emoji names to the unicode character for the emoji, we use this [huge JSON file with all the emoji mappings](https://raw.githubusercontent.com/iamcal/emoji-data/master/emoji.json).

However, with the power of PowerShell, we strip it down to just what we need (thanks @dahlbyk!)

```psh
$json = Get-Content .\emoji-names.json | ConvertFrom-Json
$json | Select-Object name,unified,short_name,short_names | ConvertTo-Json -Compress | Out-File -Encoding ascii ./emoji-names.json
```
