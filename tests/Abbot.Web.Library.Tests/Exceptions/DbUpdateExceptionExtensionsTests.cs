using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Update;
using Npgsql;
using NSubstitute;
using Serious.Abbot.Exceptions;
using Xunit;

public class DbUpdateExceptionExtensionsTests
{
    public class TheGetDatabaseErrorMethod
    {
        [Fact]
        public void ReturnsUniqueConstraintErrorForUniqueConstraintViolation()
        {
            var postgresException = new PostgresException(
                "Duplicate exception",
                "Severe",
                "Severe",
                PostgresErrorCodes.UniqueViolation,
                constraintName: "IX_MyCoolTable_InterestingColumn",
                tableName: "MyCoolTable");
            var dbUpdateException = new DbUpdateException(
                "Duplicate exception",
                postgresException,
                new List<IUpdateEntry>
                {
                    Substitute.For<IUpdateEntry>()
                });

            var result = dbUpdateException.GetDatabaseError();

            var uniqueConstraintError = Assert.IsType<UniqueConstraintError>(result);
            Assert.Equal("MyCoolTable", uniqueConstraintError.TableName);
            Assert.Equal("IX_MyCoolTable_InterestingColumn", uniqueConstraintError.ConstraintName);
            var columnName = Assert.Single(uniqueConstraintError.ColumnNames);
            Assert.Equal("InterestingColumn", columnName);
            Assert.Same(postgresException, uniqueConstraintError.Exception);
        }

        [Fact]
        public void ReturnsPostgresErrorForGenericPostgresException()
        {
            var postgresException = new PostgresException(
                "Collation exception",
                "Severe",
                "Severe",
                PostgresErrorCodes.CollationMismatch,
                constraintName: "IX_MyCoolTable_InterestingColumn",
                tableName: "MyCoolTable");
            var dbUpdateException = new DbUpdateException(
                "Collation exception",
                postgresException,
                new List<IUpdateEntry>
                {
                    Substitute.For<IUpdateEntry>()
                });

            var result = dbUpdateException.GetDatabaseError();

            Assert.IsNotType<UniqueConstraintError>(result);
            var uniqueConstraintError = Assert.IsType<DatabaseError>(result);
            Assert.Equal("MyCoolTable", uniqueConstraintError.TableName);
            Assert.Equal("IX_MyCoolTable_InterestingColumn", uniqueConstraintError.ConstraintName);
            Assert.Same(postgresException, uniqueConstraintError.Exception);
        }

        [Fact]
        public void ReturnsNullForNonPostgresException()
        {
            var postgresException = new InvalidOperationException();
            var dbUpdateException = new DbUpdateException(
                "Random exception",
                postgresException,
                new List<IUpdateEntry>
                {
                    Substitute.For<IUpdateEntry>()
                });

            var result = dbUpdateException.GetDatabaseError();

            Assert.Null(result);
        }
    }
}
