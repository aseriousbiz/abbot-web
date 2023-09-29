using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Update;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Npgsql;
using NSubstitute;
using Serious.Abbot.Entities;

namespace Serious.TestHelpers
{
    public class FakeDbContextFactory<T> : IDbContextFactory<T> where T : DbContext
    {
        readonly IServiceProvider _serviceProvider;

        public FakeDbContextFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public T CreateDbContext()
        {
            return (T)_serviceProvider.GetService(typeof(T))!;
        }
    }

    public class FakeAbbotContextOptions
    {
        /// <summary>
        /// Specifies the database name to use. This should be unique for each test, but should be the same across all FakeAbbotContext instances used by the test.
        /// </summary>
        public string? DatabaseName { get; set; }
    }

    public class FakeAbbotContext : AbbotContext
    {
        public bool RaiseOnSaveChanges { get; set; }

        public FakeAbbotContext() : this(new FakeDataProtectionProvider(), IClock.System, Options.Create(new FakeAbbotContextOptions { DatabaseName = Path.GetRandomFileName() }))
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning));
            base.OnConfiguring(optionsBuilder);
        }

        public FakeAbbotContext(IDataProtectionProvider dataProtectionProvider, IClock clock, IOptions<FakeAbbotContextOptions> options) : base(CreateDbOptions(options), dataProtectionProvider, clock)
        {
            SavedChanges += (_, _) => {
                if (RaiseOnSaveChanges)
                {
                    throw new InvalidOperationException("Saved changes when RaiseOnSaveChanges was set!");
                }
            };
        }

        DbUpdateException? _dbUpdateException;

        public void ThrowUniqueConstraintViolationOnSave(string tableName, string constraintName)
        {
            var postgresException = new PostgresException(
                "Duplicate exception",
                "Severe",
                "Severe",
                PostgresErrorCodes.UniqueViolation,
                constraintName: constraintName,
                tableName: tableName);
            _dbUpdateException = new DbUpdateException(
                "Duplicate exception",
                postgresException,
                new List<IUpdateEntry>
                {
                    Substitute.For<IUpdateEntry>()
                });
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = new())
        {
            if (_dbUpdateException is not null)
            {
                throw _dbUpdateException;
            }
            return await base.SaveChangesAsync(cancellationToken);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Some customizations needed only when using the In-Memory DB

            // The In-Memory DB doesn't support JSON so it sees our JSON properties as relationships to be mapped.
            // This doesn't work because RoomSettings doesn't have a Key.
            // So we write a custom converter to serialize/deserialize the JSON.
            // This means we can't use the In-Memory DB for testing the JSON querying, but that's not surprising.
            modelBuilder.Entity<Room>()
                .Property(r => r.Settings)
                .HasConversion<string>(
                    p => JsonConvert.SerializeObject(p),
                    s => JsonConvert.DeserializeObject<RoomSettings>(s) ?? new());
            modelBuilder.Entity<Organization>()
                .Property(r => r.DefaultRoomSettings)
                .HasConversion<string>(
                    p => JsonConvert.SerializeObject(p),
                    s => JsonConvert.DeserializeObject<RoomSettings>(s) ?? new());
            modelBuilder.Entity<Organization>()
                .Property(e => e.Settings)
                .HasConversion<string>(
                    p => JsonConvert.SerializeObject(p),
                    s => JsonConvert.DeserializeObject<OrganizationSettings>(s) ?? new());
            modelBuilder.Entity<SkillExemplar>()
                .Property(e => e.Properties)
                .HasConversion<string>(
                    p => JsonConvert.SerializeObject(p),
                    s => JsonConvert.DeserializeObject<ExemplarProperties>(s) ?? new());
            modelBuilder.Entity<Skill>()
                .Property(e => e.Properties)
                .HasConversion<string>(
                    p => JsonConvert.SerializeObject(p),
                    s => JsonConvert.DeserializeObject<SkillProperties>(s) ?? new());
            modelBuilder.Entity<Conversation>()
                .Property(p => p.ThreadIds)
                .HasConversion(
                    // See https://github.com/dotnet/efcore/issues/11926
                    v => new ArrayWrapper(v),
                    v => v.Values.ToList());
            modelBuilder.Entity<Customer>()
                .Property(c => c.Properties)
                .HasConversion(
                    p => JsonConvert.SerializeObject(p),
                    s => JsonConvert.DeserializeObject<CustomerProperties>(s) ?? new());
            modelBuilder.Entity<Member>()
                .Property(c => c.Properties)
                .HasConversion(
                    p => JsonConvert.SerializeObject(p),
                    s => JsonConvert.DeserializeObject<MemberProperties>(s) ?? new());
            modelBuilder.Entity<TaskItem>()
                .Property(c => c.Properties)
                .HasConversion(
                    p => JsonConvert.SerializeObject(p),
                    s => JsonConvert.DeserializeObject<TaskProperties>(s) ?? new());
            modelBuilder.Entity<CustomerTag>()
                .Property(c => c.Properties)
                .HasConversion(
                    p => JsonConvert.SerializeObject(p),
                    s => JsonConvert.DeserializeObject<CustomerTagProperties>(s) ?? new());
            modelBuilder.Entity<Playbook>()
                .Property(c => c.Properties)
                .HasConversion(
                    p => JsonConvert.SerializeObject(p),
                    s => JsonConvert.DeserializeObject<PlaybookProperties>(s) ?? new());
            modelBuilder.Entity<PlaybookVersion>()
                .Property(c => c.Properties)
                .HasConversion(
                    p => JsonConvert.SerializeObject(p),
                    s => JsonConvert.DeserializeObject<PlaybookVersionProperties>(s)!);
            modelBuilder.Entity<PlaybookRun>()
                .Property(c => c.Properties)
                .HasConversion(
                    p => JsonConvert.SerializeObject(p),
                    s => JsonConvert.DeserializeObject<PlaybookRunProperties>(s)!);
            modelBuilder.Entity<PlaybookRunGroup>()
                .Property(c => c.Properties)
                .HasConversion(
                    p => JsonConvert.SerializeObject(p),
                    s => JsonConvert.DeserializeObject<PlaybookRunGroupProperties>(s)!);
        }

        struct ArrayWrapper
        {
            public ArrayWrapper(List<string> values)
                => Values = values;

            public List<string> Values { get; }
        }

        static DbContextOptions<AbbotContext> CreateDbOptions(IOptions<FakeAbbotContextOptions> options)
        {
            return new DbContextOptionsBuilder<AbbotContext>()
                .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.NavigationBaseIncludeIgnored))
                .UseInMemoryDatabase(options.Value.DatabaseName ?? Path.GetRandomFileName())
                .EnableSensitiveDataLogging()
                .Options;
        }

        public override void Dispose()
        {
            // Do nothing.
        }

        public override ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// Sets the <see cref="FakeAbbotContext"/> into a state where it will raise an exception if saved.
        /// </summary>
        /// <returns>An <see cref="IDisposable"/> that, when disposed, disables raising on <see cref="DbContext.SaveChangesAsync"/>.</returns>
        public IDisposable RaiseIfSaved()
        {
            RaiseOnSaveChanges = true;
            return Disposable.Create(() => RaiseOnSaveChanges = false);
        }
    }
}
