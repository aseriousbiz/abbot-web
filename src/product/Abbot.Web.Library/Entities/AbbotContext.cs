using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Serious.Cryptography;
using Serious.EntityFrameworkCore;
using Serious.EntityFrameworkCore.ValueConverters;

namespace Serious.Abbot.Entities;

/// <summary>
/// The database context for Abbot. This is how all the data is stored and
/// retrieved.
/// </summary>
public class AbbotContext : DbContext
{
    readonly IDataProtectionProvider _dataProtectionProvider;
    readonly IClock _clock;
    public const string ConnectionStringName = "DbAlpha";

    public AbbotContext(
        DbContextOptions<AbbotContext> options,
        IDataProtectionProvider dataProtectionProvider,
        IClock clock)
        : this((DbContextOptions)options, dataProtectionProvider, clock)
    {
    }

    protected AbbotContext(
        DbContextOptions options,
        IDataProtectionProvider dataProtectionProvider,
        IClock clock) : base(options)
    {
        _dataProtectionProvider = dataProtectionProvider;
        _clock = clock;
    }

    public DbSet<User> Users { get; set; } = null!;

    public DbSet<ApiKey> ApiKeys { get; set; } = null!;

    public DbSet<Member> Members { get; set; } = null!;
    public DbSet<Role> Roles { get; set; } = null!;
    public DbSet<Organization> Organizations { get; set; } = null!;

    public DbSet<Customer> Customers { get; set; } = null!;
    public DbSet<CustomerTag> CustomerTags { get; set; } = null!;
    public DbSet<Skill> Skills { get; set; } = null!;
    public DbSet<SkillPattern> SkillPatterns { get; set; } = null!;
    public DbSet<SignalSubscription> SignalSubscriptions { get; set; } = null!;
    public DbSet<SkillVersion> SkillVersions { get; set; } = null!;
    public DbSet<Package> Packages { get; set; } = null!;
    public DbSet<PackageVersion> PackageVersions { get; set; } = null!;
    public DbSet<SkillData> SkillData { get; set; } = null!;
    public DbSet<SkillSecret> SkillSecrets { get; set; } = null!;
    public DbSet<SkillHttpTrigger> SkillHttpTriggers { get; set; } = null!;
    public DbSet<SkillTrigger> SkillTriggers { get; set; } = null!;
    public DbSet<SkillScheduledTrigger> SkillScheduledTriggers { get; set; } = null!;
    public DbSet<UserList> UserLists { get; set; } = null!;
    public DbSet<Alias> Aliases { get; set; } = null!;
    public DbSet<Memory> Memories { get; set; } = null!;
    public DbSet<MemberFact> MemberFacts { get; set; } = null!;
    public DbSet<AuditEventBase> AuditEvents { get; set; } = null!;
    public DbSet<Permission> Permissions { get; set; } = null!;
    public DbSet<DailyMetricsRollup> DailyMetricsRollups { get; set; } = null!;
    public DbSet<SlackEventsRollup> SlackEventsRollups { get; set; } = null!;
    public DbSet<Cohort> Cohorts { get; set; } = null!;
    public DbSet<Setting> Settings { get; set; } = null!;
    public DbSet<Room> Rooms { get; set; } = null!;
    public DbSet<Conversation> Conversations { get; set; } = null!;
    public DbSet<ConversationMember> ConversationMembers { get; set; } = null!;
    public DbSet<RoomAssignment> RoomAssignments { get; set; } = null!;
    public DbSet<ConversationEvent> ConversationEvents { get; set; } = null!;
    public DbSet<SlackEvent> SlackEvents { get; set; } = null!;
    public DbSet<MetricObservation> MetricObservations { get; set; } = null!;
    public DbSet<Integration> Integrations { get; set; } = null!;
    public DbSet<Announcement> Announcements { get; set; } = null!;
    public DbSet<AnnouncementMessage> AnnouncementMessages { get; set; } = null!;
    public DbSet<AnnouncementCustomerSegment> AnnouncementCustomerSegments { get; set; } = null!;
    public DbSet<LinkedIdentity> LinkedIdentities { get; set; } = null!;
    public DbSet<ConversationLink> ConversationLinks { get; set; } = null!;
    public DbSet<RoomLink> RoomLinks { get; set; } = null!;
    public DbSet<Form> Forms { get; set; } = null!;
    public DbSet<Hub> Hubs { get; set; } = null!;
    public DbSet<Tag> Tags { get; set; } = null!;
    public DbSet<TaskItem> Tasks { get; set; } = null!;
    public DbSet<SkillExemplar> SkillExemplars { get; set; } = null!;

    public DbSet<MetadataField> MetadataFields { get; set; } = null!;
    public DbSet<Playbook> Playbooks { get; set; } = null!;
    public DbSet<PlaybookVersion> PlaybookVersions { get; set; } = null!;
    public DbSet<PlaybookRun> PlaybookRuns { get; set; } = null!;
    public DbSet<PlaybookRunGroup> PlaybookRunGroups { get; set; } = null!;

    public DbSet<PendingMemberNotification> PendingMemberNotifications { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        //
        // IMPORTANT! Custom crap goes AFTER the call to base.OnModelCreating
        //

        modelBuilder.HasPostgresExtension("postgis");
        modelBuilder.HasPostgresExtension("citext");

        // Use Optimistic Concurrency when editing SlackEvent.
        // https://www.npgsql.org/efcore/modeling/concurrency.html?tabs=data-annotations
        modelBuilder.Entity<SlackEvent>().UseXminAsConcurrencyToken();

        // Workaround for bug where Views cause tables to be created: https://docs.microsoft.com/en-us/ef/core/what-is-new/ef-core-5.0/breaking-changes#toview
        modelBuilder.Entity<Cohort>().ToView(null);
        modelBuilder.Entity<Cohort>().ToTable(nameof(Cohort), t => t.ExcludeFromMigrations());

        modelBuilder.Entity<Organization>()
            .HasIndex(i => new { i.PlatformId })
            .IsUnique();
        modelBuilder.Entity<Organization>()
            .HasIndex(i => new { i.PlatformType });
        modelBuilder.Entity<Organization>()
            .OwnsOne<Threshold<TimeSpan>>(o => o.DefaultTimeToRespond);

        // This one was created with a non-plural name.
        modelBuilder.Entity<ApiKey>()
            .ToTable("ApiKey");

        modelBuilder.Entity<User>()
            .HasIndex(i => i.NameIdentifier)
            .IsUnique();
        modelBuilder.Entity<User>()
            .HasIndex(i => i.PlatformUserId)
            .IsUnique();

        modelBuilder.Entity<Member>()
            .HasMany(u => u.Facts)
            .WithOne(f => f.Subject);
        modelBuilder.Entity<Member>()
            .HasIndex(u => new { u.UserId, u.OrganizationId })
            .IsUnique();

        modelBuilder.Entity<Member>()
            .OwnsOne(m => m.WorkingHours);

        modelBuilder.Entity<Skill>()
            .HasIndex(i => new { i.Name, i.OrganizationId })
            .IsUnique();
        modelBuilder.Entity<Skill>()
            .HasOne(s => s.Package)
            .WithOne(p => p.Skill)
            .HasForeignKey<Package>(p => p.SkillId);

        modelBuilder.Entity<SkillData>()
            .HasIndex(i => new { i.Key, i.SkillId, i.Scope, i.ContextId })
            .IsUnique();

        modelBuilder.Entity<Package>()
            .HasIndex(i => new { i.SkillId, i.OrganizationId })
            .IsUnique();

        modelBuilder.Entity<Alias>()
            .HasIndex(i => new { i.Name, i.OrganizationId })
            .IsUnique();

        modelBuilder.Entity<UserList>()
            .HasIndex(i => new { i.Name, i.OrganizationId })
            .IsUnique();

        modelBuilder.Entity<SignalSubscription>()
            .HasIndex(e => new { e.Name, e.SkillId })
            .IsUnique();

        modelBuilder.Entity<SkillPattern>()
            .HasIndex(e => new { e.Name, e.SkillId })
            .IsUnique();
        modelBuilder.Entity<SkillPattern>()
            .HasIndex(e => new { e.Slug, e.SkillId })
            .IsUnique();

        modelBuilder.Entity<SkillSecret>()
            .HasIndex(i => new { i.Name, i.SkillId })
            .IsUnique();
        modelBuilder.Entity<SkillSecret>()
            .HasIndex(i => i.KeyVaultSecretName)
            .IsUnique();
        modelBuilder.Entity<SkillTrigger>()
            .HasIndex(e => new { e.RoomId, e.SkillId, e.TriggerType })
            .HasFilter(@"""RoomId"" IS NOT NULL")
            .IsUnique();
        modelBuilder.Entity<SkillHttpTrigger>()
            .HasIndex(e => e.ApiToken)
            .IsUnique();
        modelBuilder.Entity<SkillTrigger>()
            .HasDiscriminator<string>(nameof(SkillTrigger.TriggerType));

        modelBuilder.Entity<Role>()
            .HasIndex(i => i.Name)
            .IsUnique();

        modelBuilder.HasPostgresExtension("uuid-ossp")
            .Entity<AuditEventBase>()
            .Property(e => e.Identifier)
            .HasDefaultValueSql("uuid_generate_v4()");

        modelBuilder.Entity<AnnouncementEvent>()
            .HasBaseType<LegacyAuditEvent>();

        modelBuilder.Entity<AuditEventBase>()
            .HasIndex(i => i.Identifier)
            .IsUnique();

        modelBuilder.Entity<AuditEventBase>()
            .HasIndex(i => i.ParentIdentifier);

        modelBuilder.Entity<AuditEventBase>()
            .HasDiscriminator(e => e.Discriminator);
        modelBuilder.Entity<AuditEventBase>()
            .HasIndex(e => e.Discriminator);

        modelBuilder.Entity<AuditEvent>()
            .HasBaseType<AuditEventBase>()
            .HasIndex(e => e.Type);
        modelBuilder.Entity<AuditEvent>()
            .Property(e => e.Type)
            .HasConversion<string>(
                t => t.ToString(),
                s => AuditEventType.Parse(s));

        modelBuilder.Entity<LegacyAuditEvent>()
            .HasBaseType<AuditEventBase>();

        modelBuilder.Entity<BillingEvent>()
            .HasBaseType<LegacyAuditEvent>();
        modelBuilder.Entity<BuiltInSkillRunEvent>()
            .HasBaseType<LegacyAuditEvent>();
        modelBuilder.Entity<ConversationTitleChangedEvent>()
            .HasBaseType<LegacyAuditEvent>();
        modelBuilder.Entity<FormAuditEvent>()
            .HasBaseType<LegacyAuditEvent>();
        modelBuilder.Entity<HubAuditEvent>()
            .HasBaseType<LegacyAuditEvent>();
        modelBuilder.Entity<SettingAuditEvent>()
            .HasBaseType<LegacyAuditEvent>();
        modelBuilder.Entity<StaffViewedCodeAuditEvent>()
            .HasBaseType<StaffAuditEvent>();
        modelBuilder.Entity<StaffViewedSlackEventContent>()
            .HasBaseType<StaffAuditEvent>();
        modelBuilder.Entity<StaffAuditEvent>()
            .HasBaseType<LegacyAuditEvent>();
        modelBuilder.Entity<SkillNotFoundEvent>()
            .HasBaseType<LegacyAuditEvent>();
        modelBuilder.Entity<SkillAuditEvent>()
            .HasBaseType<LegacyAuditEvent>();
        modelBuilder.Entity<PackageEvent>()
            .HasBaseType<SkillAuditEvent>();
        modelBuilder.Entity<SkillEditSessionAuditEvent>()
            .HasBaseType<SkillAuditEvent>();
        modelBuilder.Entity<SkillSecretEvent>()
            .HasBaseType<SkillAuditEvent>();
        modelBuilder.Entity<SkillInfoChangedAuditEvent>()
            .HasBaseType<SkillAuditEvent>();
        modelBuilder.Entity<SkillRunAuditEvent>()
            .HasBaseType<SkillAuditEvent>();
        modelBuilder.Entity<TriggerRunEvent>()
            .HasBaseType<SkillRunAuditEvent>();
        modelBuilder.Entity<HttpTriggerRunEvent>()
            .HasBaseType<TriggerRunEvent>();
        modelBuilder.Entity<PlaybookActionSkillRunEvent>()
            .HasBaseType<TriggerRunEvent>();
        modelBuilder.Entity<ScheduledTriggerRunEvent>()
            .HasBaseType<TriggerRunEvent>();
        modelBuilder.Entity<TriggerChangeEvent>()
            .HasBaseType<SkillAuditEvent>();
        modelBuilder.Entity<ScheduledTriggerChangeEvent>()
            .HasBaseType<TriggerChangeEvent>();
        modelBuilder.Entity<HttpTriggerChangeEvent>()
            .HasBaseType<TriggerChangeEvent>();

        modelBuilder.Entity<AdminAuditEvent>()
            .HasBaseType<LegacyAuditEvent>();
        modelBuilder.Entity<InstallationEvent>()
            .HasBaseType<AdminAuditEvent>();

        modelBuilder.Entity<ConversationLinkedEvent>()
            .Property(e => e.ExternalId).HasColumnName("ExternalId");
        modelBuilder.Entity<ConversationLinkedEvent>()
            .Property(e => e.LinkType).HasColumnName("LinkType");
        modelBuilder.Entity<ConversationLinkedEvent>()
            .HasBaseType<LegacyAuditEvent>();

        modelBuilder.Entity<RoomRespondersChangedEvent>()
            .HasBaseType<AdminAuditEvent>();
        modelBuilder.Entity<RoomResponseTimesChangedEvent>()
            .HasBaseType<AdminAuditEvent>();

        modelBuilder.Entity<RoomLinkedEvent>()
            .Property(e => e.ExternalId).HasColumnName("ExternalId");
        modelBuilder.Entity<RoomLinkedEvent>()
            .Property(e => e.LinkType).HasColumnName("LinkType");
        modelBuilder.Entity<RoomLinkedEvent>()
            .HasBaseType<LegacyAuditEvent>();

        modelBuilder.Entity<RoomUnlinkedEvent>()
            .Property(e => e.ExternalId).HasColumnName("ExternalId");
        modelBuilder.Entity<RoomUnlinkedEvent>()
            .Property(e => e.LinkType).HasColumnName("LinkType");
        modelBuilder.Entity<RoomUnlinkedEvent>()
            .HasBaseType<LegacyAuditEvent>();

        modelBuilder.Entity<ConversationEvent>()
            .HasDiscriminator<string>("Type")
            .HasValue<MessagePostedEvent>("MessagePosted")
            .HasValue<ExternalLinkEvent>("ExternalLink")
            .HasValue<StateChangedEvent>("StateChanged")
            .HasValue<UnknownConversationEvent>("ForeignLink") // Don't break on old data.
            .HasValue<UnknownConversationEvent>(string.Empty);
        modelBuilder.Entity<MessagePostedEvent>()
            .HasBaseType<ConversationEvent>();
        modelBuilder.Entity<NotificationEvent>()
            .HasBaseType<ConversationEvent>();
        modelBuilder.Entity<StateChangedEvent>()
            .HasBaseType<ConversationEvent>();
        modelBuilder.Entity<ExternalLinkEvent>()
            .HasBaseType<ConversationEvent>();
        modelBuilder.Entity<SlackImportEvent>()
            .HasBaseType<ConversationEvent>();
        modelBuilder.Entity<AttachedToHubEvent>()
            .HasBaseType<ConversationEvent>();
#pragma warning disable CS0618
        modelBuilder.Entity<ConversationSummarizedEvent>()
#pragma warning restore CS0618
            .HasBaseType<ConversationEvent>();
        modelBuilder.Entity<ConversationClassifiedEvent>()
            .HasBaseType<ConversationEvent>();
#pragma warning disable CS0618
        modelBuilder.Entity<ConversationMatchedEvent>()
#pragma warning restore CS0618
            .HasBaseType<ConversationEvent>();

        modelBuilder.Entity<ConversationMember>()
            .HasIndex(e => new {
                e.ConversationId,
                e.MemberId
            })
            .IsUnique();

        modelBuilder.Entity<MemberRole>()
            .HasKey(t => new { UserId = t.MemberId, t.RoleId });

        modelBuilder.Entity<Permission>()
            .HasKey(p => new { p.MemberId, p.SkillId });

        modelBuilder.Entity<Setting>()
            .HasIndex(e => new { e.Scope, e.Name })
            .IsUnique();

        modelBuilder.Entity<SlackEvent>()
            .HasIndex(e => e.EventId)
            .IsUnique();

        // Part of the Soft Deletion implementation. This determines if the entity is IRecoverableEntity,
        // and if so, set the default for "IsDeleted" to false on creation.
        modelBuilder.SetQueryFilterOnAllEntities<IRecoverableEntity>(
            e => !e.IsDeleted);
        modelBuilder.Entity<Permission>()
            .HasQueryFilter(e => !e.Skill.IsDeleted);
        modelBuilder.Entity<SkillData>()
            .HasQueryFilter(e => !e.Skill.IsDeleted);

        modelBuilder.Entity<Room>()
            .HasIndex(e => new { e.OrganizationId, e.PlatformRoomId })
            .IsUnique();

        modelBuilder.Entity<Room>()
            .OwnsOne<Threshold<TimeSpan>>(r => r.TimeToRespond);

        modelBuilder.Entity<Room>()
            .HasOne(r => r.Hub)
            .WithMany(h => h.AttachedRooms)
            .HasForeignKey(r => r.HubId)
            .HasPrincipalKey(h => h.Id);

        modelBuilder.Entity<Conversation>()
            .HasIndex(e => new {
                e.RoomId,
                e.FirstMessageId,
            }).IsUnique();

        modelBuilder.Entity<Conversation>()
            .HasOne(p => p.StartedBy);

        modelBuilder.Entity<Conversation>()
            .HasMany(c => c.Assignees)
            .WithMany(a => a.AssignedConversations);

        modelBuilder.Entity<Member>()
            .HasMany(c => c.AssignedConversations)
            .WithMany(ac => ac.Assignees);

        modelBuilder.Entity<RoomAssignment>()
            .HasIndex(e => new { e.RoomId, e.Role, e.MemberId })
            .IsUnique();

        modelBuilder.Entity<RoomAssignment>()
            .Property(a => a.Role)
            .HasConversion(new EnumMemberValueConverter<RoomRole>());

        modelBuilder.Entity<AnnouncementMessage>()
            .HasIndex(e => new { e.RoomId, e.AnnouncementId })
            .IsUnique();

        modelBuilder.Entity<AnnouncementCustomerSegment>()
            .HasIndex(e => new { e.AnnouncementId, e.CustomerTagId })
            .IsUnique();

        modelBuilder.Entity<SlackEventsRollup>()
            .HasKey(e => new { e.Date, e.EventType, e.TeamId });

        modelBuilder.Entity<MetricObservation>()
            .HasIndex(m => new {
                // In Postgres, it's best to put any columns that will mostly be compared using '==' first.
                // https://www.postgresql.org/docs/current/indexes-multicolumn.html
                m.Metric,
                m.Timestamp
            });

        modelBuilder.Entity<Integration>()
            .HasIndex(i => new {
                i.OrganizationId,
                i.Type
            });

        modelBuilder.Entity<Integration>()
            .HasIndex(i => new {
                i.Type,
                i.ExternalId
            })
            .IsUnique();

        modelBuilder.Entity<LinkedIdentity>()
            .HasIndex(i => new { i.MemberId, i.OrganizationId, i.Type })
            .IsUnique();
        modelBuilder.Entity<LinkedIdentity>()
            .HasIndex(i => new { i.OrganizationId, i.Type, i.ExternalId })
            .IsUnique();

        modelBuilder.Entity<ConversationLink>()
            .HasIndex(l => new { l.OrganizationId, l.LinkType, l.ExternalId })
            .IsUnique();

        modelBuilder.Entity<Form>()
            .HasIndex(l => new { l.OrganizationId, l.Key })
            .IsUnique();

        modelBuilder.Entity<Hub>()
            .HasIndex(l => new { l.OrganizationId, l.RoomId })
            .IsUnique();

        modelBuilder.Entity<Hub>()
            .HasOne<Room>(h => h.Room)
            .WithOne() // No navigation from Room back to the Hub for which it is the control room.
            .HasForeignKey<Hub>(h => h.RoomId)
            .HasPrincipalKey<Room>(r => r.Id);

        modelBuilder.Entity<Tag>()
            .HasIndex(t => new { t.OrganizationId, t.Name })
            .IsUnique();

        modelBuilder.Entity<CustomerTag>()
            .HasIndex(t => new { t.OrganizationId, t.Name })
            .IsUnique();

        modelBuilder.Entity<Conversation>()
            .HasMany(c => c.Tags)
            .WithOne(ct => ct.Conversation);

        modelBuilder.Entity<Conversation>()
            .HasIndex(c => c.ThreadIds)
            .HasMethod("GIN");

        modelBuilder.Entity<Tag>()
            .HasMany(c => c.Conversations)
            .WithOne(ct => ct.Tag);

        modelBuilder.Entity<ConversationTag>()
            .HasKey(ct => new {
                ct.ConversationId,
                ct.TagId
            });

        modelBuilder.Entity<SlackEvent>().HasIndex(e => e.Created);
        modelBuilder.Entity<SlackEventsRollup>().HasIndex(e => e.Date);
        modelBuilder.Entity<MetadataField>()
            .HasIndex(mf => new {
                mf.OrganizationId,
                mf.Type,
                mf.Name
            })
            .IsUnique();

        modelBuilder.Entity<MetadataField>()
            .Property(m => m.Type)
            .HasConversion(new EnumMemberValueConverter<MetadataFieldType>());

        modelBuilder.Entity<RoomMetadataField>()
            .HasIndex(mf => new {
                mf.RoomId,
                mf.MetadataFieldId
            })
            .IsUnique();

        modelBuilder.Entity<Customer>()
            .HasIndex(c => new {
                c.OrganizationId,
                c.Name,
            })
            .IsUnique();

        // Playbook slugs must be unique in an organization.
        modelBuilder.Entity<Playbook>()
            .HasIndex(pv => new {
                pv.OrganizationId,
                pv.Slug,
            })
            .IsUnique();

        // Playbook versions must be unique for a given playbook.
        modelBuilder.Entity<PlaybookVersion>()
            .HasIndex(pv => new {
                pv.PlaybookId,
                pv.Version,
            })
            .IsUnique();

        modelBuilder.Entity<PlaybookRun>()
            .UseXminAsConcurrencyToken()
            .HasIndex(s => s.CorrelationId)
            .IsUnique();

        modelBuilder.Entity<PlaybookRunGroup>()
            .HasIndex(s => s.CorrelationId)
            .IsUnique();

        // Do this at the end to capture all properties added by other things above here.
        var secretStringValueConverter = new SecretStringValueConverter(_dataProtectionProvider);
        modelBuilder.SetValueConverterOnPropertiesOfType<SecretString>(secretStringValueConverter);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // This tells EF that all properties of type SecretString should be converted to a string and not
        // treated like a navigation property. We can't specify the SecureStringValueConverter here because
        // EF does not use DI to create instances of the converters and this method doesn't allow us to
        // pass in a converter.
        configurationBuilder.Properties<SecretString>().HaveConversion<string>();
    }

    public override int SaveChanges()
    {
        throw new InvalidOperationException("Use SaveChangesAsync.");
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        UpdateEntitiesBeforeSaving();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    // Updates entities before saving them. Returns a
    // list of followup actions
    void UpdateEntitiesBeforeSaving()
    {
        var now = _clock.UtcNow;
        foreach (var entry in ChangeTracker.Entries())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    UpdateCreated(entry, now);
                    break;
                case EntityState.Modified:
                    UpdateModified(entry, now);
                    break;
                case EntityState.Deleted:
                    UpdateDeleted(entry, now);
                    break;
                case EntityState.Detached:
                case EntityState.Unchanged:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(entry.State), $"The state {entry.State} is out of range.");
            }
        }
    }

    static void UpdateCreated(EntityEntry entry, DateTime now)
    {
        if (entry.Entity is not IEntity entity)
        {
            return;
        }

        if (entity.Created == DateTime.MinValue)
        {
            entity.Created = now;
        }

        if (entity is ITrackedEntity trackedEntity)
        {
            if (trackedEntity.Modified == DateTime.MinValue)
            {
                trackedEntity.Modified = now;
            }
        }

        if (entity is IRecoverableEntity)
        {
            entry.CurrentValues[nameof(IRecoverableEntity.IsDeleted)] = false;
        }
    }

    static void UpdateModified(EntityEntry entry, DateTime now)
    {
        if (entry.Entity is ITrackedEntity trackedEntity
            // If the entity is a skill, we don't update the modified date if only the cache key is modified.
            // This fixes https://github.com/aseriousbiz/abbot/issues/3746
            && (entry.Entity is not Skill
                || entry.Properties.Where(p => p.IsModified).Any(p => p.Metadata.Name is not nameof(Skill.CacheKey))))
        {
            trackedEntity.Modified = now;
        }
    }

    static void UpdateDeleted(EntityEntry entry, DateTime now)
    {
        if (entry.Entity is IRecoverableEntity)
        {
            entry.State = EntityState.Modified;
            entry.CurrentValues[nameof(IRecoverableEntity.IsDeleted)] = true;

            if (entry.Entity is INamedEntity)
            {
                entry.CurrentValues[nameof(INamedEntity.Name)] += "_DELETED-" + TokenCreator.CreateRandomString(16);
            }

            if (entry.Entity is ITrackedEntity trackedEntity)
            {
                trackedEntity.Modified = now;
            }
        }
    }
}
