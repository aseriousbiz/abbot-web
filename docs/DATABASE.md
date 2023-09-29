# Database Operations

## Azure

Production uses an [Azure Database for PostgreSQL flexible server](https://portal.azure.com/#blade/HubsExtension/BrowseResource/resourceType/Microsoft.DBforPostgreSQL%2FflexibleServers) with [Query Store](https://docs.microsoft.com/en-us/azure/postgresql/flexible-server/concepts-query-store) enabled.

## Extensions

Abbot depends on three Postgres [extensions](https://docs.microsoft.com/en-us/azure/postgresql/flexible-server/concepts-extensions):

- [uuid-ossp](https://www.postgresql.org/docs/13/uuid-ossp.html)
- [postgis](https://www.postgis.net/)
- [citext](https://www.postgresql.org/docs/13/citext.html)

Docker includes these via a [script](../docker/postgres/9-uuid.sh), but they need to be manually created in other environments. Migrations typically do not run with sufficient permissions.

## pgAdmin

The local development environment automatically deploys a Docker container running `pgAdmin`. You can connect to the pgAdmin tool at `http://localhost:8090` and log in using the following credentials:

* User Name: `postgres@a.com`
* Password: `pgadmin`

## Migrations

We automatically run all migrations on deployment. Take **extra caution** when writing migrations to ensure they don't corrupt data or fail deployments.

### Generating a new migration

After updating the EF Core model, you can generate a new migration using this command:

```bash
> script/add-ef-migration NameOfMigration
```

(Replace `NameOfMigration` with a descriptive name, like `AddDescriptionToFlarbles`)

The new migration will be placed in `src/product/Abbot.Web/Migrations`.

You can preview the migration script using `script/script-migrations`. This will dump the entire history of migrations as a SQL script to standard out, your new migration will be at the end.

### Migration Safety

Migrations **can be destructive**. Take care when writing migrations that affect existing data (renaming columns, changing column types, etc.). The safest way to make changes is a pattern called ["Expand/Contract" Migrations](https://www.prisma.io/dataguide/types/relational/expand-and-contract-pattern). In this pattern you follow a multi-step process to safely migrate database schema:

1. **Expand** the database schema to include the new schema, alongside the old. For example:
    * Instead of renaming a column/table, add a new one with the new name.
    * Instead of reformatting data or changing the data type in an existing column, add a new column for the reformatted data.
    * If you are simply removing a column/table, you skip this step.
1. **Update** the application to handle the new schema.
    * When renaming/reformatting a column/table, change the app to write to both old and new column/table.
    * When removing a column/table, you skip this step.
    * This can be done in the same deployment as the Expand migration.
1. **Migrate** existing data.
    * Use background processes/tasks to copy existing data from the old column/table to the new.
    * Since the app is updated, when this step finishes, you know the new column/table is fully up-to-date and will remain up-to-date.
    * When removing a column/table, you skip this step.
1. **Update** the application to ignore the old column/table.
    * Remove the old column/table from application queries.
    * Don't set the old column/table when inserting/updating data.
    * This can be done in the same deployment as the Contract migration (below).
1. **Contract** the database schema.
    * Remove the old column/table from the database (via a migration).

For complex changes with a lot of data, it's best to run each step as a separate deployment. For simpler changes, you can deploy the migration to expand the schema alongside the changes to the app to use both columns, and similarly you can deploy the migration to contract the schema alongside the changes to stop the app from using the old column.

When you are _extremely_ confident that a migration with potential for data-loss will be safe to execute, you can shortcut these steps. But if you're ever in doubt, it doesn't hurt to be cautious.
