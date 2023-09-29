using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Serious.Abbot.Exceptions;

/// <summary>
/// Provides additional Database specific information about a <see cref="DbUpdateException"/> thrown by EF Core.
/// </summary>
/// <param name="TableName">The table involved, if any.</param>
/// <param name="ConstraintName">The constraint involved, if any.</param>
/// <param name="Exception">The unwrapped database provider specific exception.</param>
public record DatabaseError(string? TableName, string? ConstraintName, Exception Exception);

/// <summary>
/// Provides additional Postgres specific information about a <see cref="DbUpdateException"/> thrown by EF Core.
/// This describes the case where the exception is a unique constraint violation.
/// </summary>
/// <param name="ColumnNames">The column names parsed from the constraint name assuming the constraint follows the "IX_{Table}_{Column1}_..._{ColumnN}" naming convention.</param>
/// <param name="TableName">The table involved, if any.</param>
/// <param name="ConstraintName">The constraint involved, if any.</param>
/// <param name="Exception">The unwrapped database provider specific exception.</param>
public record UniqueConstraintError(
    IReadOnlyList<string> ColumnNames,
    string? TableName,
    string? ConstraintName,
    Exception Exception) : DatabaseError(TableName, ConstraintName, Exception)
{
    /// <summary>
    /// Creates a <see cref="UniqueConstraintError"/> from a <see cref="PostgresException"/>.
    /// </summary>
    /// <param name="postgresException">The <see cref="PostgresException"/>.</param>
    /// <returns>A <see cref="UniqueConstraintError"/> with extra information about the unique constraint violation.</returns>
    public static UniqueConstraintError FromPostgresException(PostgresException postgresException)
    {
        var constraintName = postgresException.ConstraintName;
        var tableName = postgresException.TableName;
        var constrainPrefix = tableName is not null
            ? $"IX_{tableName}_"
            : null;

        var columnNames = constrainPrefix is not null
                  && constraintName is not null
                  && constraintName.StartsWith(constrainPrefix, StringComparison.Ordinal)
            ? constraintName[constrainPrefix.Length..].Split('_')
            : Array.Empty<string>();

        return new UniqueConstraintError(columnNames, tableName, constraintName, postgresException);
    }
}

/// <summary>
/// Extensions to <see cref="DbUpdateException"/> used to retrieve more database specific information about the thrown
/// exception.
/// </summary>
public static class DbUpdateExceptionExtensions
{
    /// <summary>
    /// Retrieves a <see cref="DatabaseError"/> with database specific error information from the
    /// <see cref="DbUpdateException"/> thrown by EF Core.
    /// </summary>
    /// <param name="exception">The <see cref="DbUpdateException"/> thrown.</param>
    /// <returns>A <see cref="DatabaseError"/> or derived class if the inner exception matches one of the supported types. Otherwise returns null.</returns>
    public static DatabaseError? GetDatabaseError(this DbUpdateException exception)
    {
        if (exception.InnerException is PostgresException postgresException)
        {
            return postgresException.SqlState switch
            {
                PostgresErrorCodes.UniqueViolation => UniqueConstraintError.FromPostgresException(postgresException),
                _ => new DatabaseError(
                    postgresException.TableName,
                    postgresException.ConstraintName,
                    postgresException)
            };
        }

        return null;
    }
}
