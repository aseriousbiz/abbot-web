# Development Environments

We have full development environments with their own website, database, vaults, storage and skill runners.

## dev1

- Website: `https://abbot-web-dev1.azurewebsites.net`
- Database:  `abbot-db-dev1.postgres.database.azure.com` (see 1password for login data)

The dev1 environment has all the components for slack and bot framework, with skill runners, slack app, auth0 connection, etc.

### Deploying to dev1

To deploy to the `dev1` environment, push to the `dev1` branch or run `gh workflow main_abbot.yml --ref [git branch to deploy] -f tag=dev1 -f deploy=1`


## dev2

- Website: `https://abbot-web-dev2.azurewebsites.net`
- Database:  `abbot-db-dev2.postgres.database.azure.com` (see 1password for login data, it's the same as dev1)

The dev2 environment has all the components for slack and bot framework, with skill runners, slack app, auth0 connection, etc.

### Deploying to dev2

To deploy to the `dev2` environment, push to the `dev2` branch or run `gh workflow main_abbot.yml --ref [git branch to deploy] -f tag=dev2 -f deploy=1`

#
