using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using Serious.Abbot.Entities;

#nullable disable

namespace Serious.Abbot.Migrations
{
    public partial class Base : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // CREATE EXTENSION calls require a super-user, and the app doesn't run with a super-user.
            // So we can't put them in migrations 😞.

            migrationBuilder.CreateTable(
                name: "DailyMetricsRollups",
                columns: table => new
                {
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ActiveUserCount = table.Column<int>(type: "integer", nullable: false),
                    InteractionCount = table.Column<int>(type: "integer", nullable: false),
                    SkillCreatedCount = table.Column<int>(type: "integer", nullable: false),
                    OrganizationCreatedCount = table.Column<int>(type: "integer", nullable: false),
                    UserCreatedCount = table.Column<int>(type: "integer", nullable: false),
                    MonthlyRecurringRevenue = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyMetricsRollups", x => x.Date);
                });

            migrationBuilder.CreateTable(
                name: "Organizations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Domain = table.Column<string>(type: "text", nullable: true),
                    StripeCustomerId = table.Column<string>(type: "text", nullable: true),
                    StripeSubscriptionId = table.Column<string>(type: "text", nullable: true),
                    PlanType = table.Column<int>(type: "integer", nullable: false),
                    Trial_Plan = table.Column<int>(type: "integer", nullable: true),
                    Trial_Expiry = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TrialEligible = table.Column<bool>(type: "boolean", nullable: false),
                    PlatformId = table.Column<string>(type: "text", nullable: false),
                    PlatformBotId = table.Column<string>(type: "text", nullable: true),
                    PlatformBotUserId = table.Column<string>(type: "text", nullable: true),
                    PlatformType = table.Column<int>(type: "integer", nullable: false),
                    ApiToken = table.Column<string>(type: "text", nullable: true),
                    Avatar = table.Column<string>(type: "text", nullable: false),
                    BotAppId = table.Column<string>(type: "text", nullable: true),
                    BotAppName = table.Column<string>(type: "text", nullable: true),
                    BotName = table.Column<string>(type: "text", nullable: true),
                    BotAvatar = table.Column<string>(type: "text", nullable: true),
                    BotResponseAvatar = table.Column<string>(type: "text", nullable: true),
                    AutoApproveUsers = table.Column<bool>(type: "boolean", nullable: false),
                    DotNetEndpoint = table.Column<string>(type: "text", nullable: true),
                    PythonEndpoint = table.Column<string>(type: "text", nullable: true),
                    JavaScriptEndpoint = table.Column<string>(type: "text", nullable: true),
                    InkEndpoint = table.Column<string>(type: "text", nullable: true),
                    Scopes = table.Column<string>(type: "text", nullable: true),
                    ShortcutCharacter = table.Column<char>(type: "character(1)", nullable: false),
                    ApiEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    FallbackResponderEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    Enterprise = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultRoomSettings = table.Column<RoomSettings>(type: "jsonb", nullable: true),
                    DefaultTimeToRespond_Warning = table.Column<TimeSpan>(type: "interval", nullable: true),
                    DefaultTimeToRespond_Critical = table.Column<TimeSpan>(type: "interval", nullable: true),
                    UserSkillsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LastPlatformUpdate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Organizations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SlackEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EventId = table.Column<string>(type: "text", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    TeamId = table.Column<string>(type: "text", nullable: false),
                    JobId = table.Column<string>(type: "text", nullable: true),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Completed = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlackEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SlackEventsRollups",
                columns: table => new
                {
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    TeamId = table.Column<string>(type: "text", nullable: false),
                    SuccessCount = table.Column<int>(type: "integer", nullable: false),
                    ErrorCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlackEventsRollups", x => new { x.Date, x.EventType, x.TeamId });
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlatformUserId = table.Column<string>(type: "text", nullable: false),
                    NameIdentifier = table.Column<string>(type: "text", nullable: true),
                    SlackTeamId = table.Column<string>(type: "text", nullable: true),
                    UserName = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: true),
                    Avatar = table.Column<string>(type: "text", nullable: true),
                    IsBot = table.Column<bool>(type: "boolean", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Integrations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    Settings = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Integrations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Integrations_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Rooms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: true),
                    PlatformRoomId = table.Column<string>(type: "text", nullable: false),
                    ConversationId = table.Column<string>(type: "text", nullable: false),
                    ManagedConversationsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Persistent = table.Column<bool>(type: "boolean", nullable: false),
                    Shared = table.Column<bool>(type: "boolean", nullable: true),
                    RoomType = table.Column<string>(type: "text", nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: true),
                    Archived = table.Column<bool>(type: "boolean", nullable: true),
                    BotIsMember = table.Column<bool>(type: "boolean", nullable: true),
                    Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TimeToRespond_Warning = table.Column<TimeSpan>(type: "interval", nullable: true),
                    TimeToRespond_Critical = table.Column<TimeSpan>(type: "interval", nullable: true),
                    Settings = table.Column<RoomSettings>(type: "jsonb", nullable: true),
                    LastPlatformUpdate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rooms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rooms_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Aliases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TargetSkill = table.Column<string>(type: "text", nullable: false),
                    TargetArguments = table.Column<string>(type: "text", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedById = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "citext", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Aliases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Aliases_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Aliases_Users_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Aliases_Users_ModifiedById",
                        column: x => x.ModifiedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuditEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EntityId = table.Column<int>(type: "integer", nullable: true),
                    Identifier = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuid_generate_v4()"),
                    ParentIdentifier = table.Column<Guid>(type: "uuid", nullable: true),
                    TraceId = table.Column<string>(type: "text", nullable: true),
                    ActorId = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    Details = table.Column<string>(type: "text", nullable: true),
                    Discriminator = table.Column<string>(type: "text", nullable: false),
                    PlanType = table.Column<int>(type: "integer", nullable: true),
                    SubscriptionId = table.Column<string>(type: "text", nullable: true),
                    CustomerId = table.Column<string>(type: "text", nullable: true),
                    BillingEmail = table.Column<string>(type: "text", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    SkillName = table.Column<string>(type: "text", nullable: true),
                    Arguments = table.Column<string>(type: "text", nullable: true),
                    Room = table.Column<string>(type: "text", nullable: true),
                    RoomId = table.Column<string>(type: "text", nullable: true),
                    LinkType = table.Column<string>(type: "text", nullable: true),
                    ExternalId = table.Column<string>(type: "text", nullable: true),
                    OldTitle = table.Column<string>(type: "text", nullable: true),
                    NewTitle = table.Column<string>(type: "text", nullable: true),
                    SkillId = table.Column<int>(type: "integer", nullable: true),
                    Language = table.Column<int>(type: "integer", nullable: true),
                    FirstSkillVersionId = table.Column<int>(type: "integer", nullable: true),
                    Code = table.Column<string>(type: "text", nullable: true),
                    EditCount = table.Column<int>(type: "integer", nullable: true),
                    Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ChangeType = table.Column<string>(type: "text", nullable: true),
                    ChangeDescription = table.Column<string>(type: "text", nullable: true),
                    Secrets = table.Column<string>(type: "text", nullable: true),
                    Signal = table.Column<string>(type: "text", nullable: true),
                    Headers = table.Column<string>(type: "text", nullable: true),
                    ResponseHeaders = table.Column<string>(type: "text", nullable: true),
                    ResponseContentType = table.Column<string>(type: "text", nullable: true),
                    StatusCode = table.Column<int>(type: "integer", nullable: true),
                    Response = table.Column<string>(type: "text", nullable: true),
                    Url = table.Column<string>(type: "text", nullable: true),
                    CronSchedule = table.Column<string>(type: "text", nullable: true),
                    TimeZoneId = table.Column<string>(type: "text", nullable: true),
                    SecretId = table.Column<int>(type: "integer", nullable: true),
                    SecretName = table.Column<string>(type: "text", nullable: true),
                    Command = table.Column<string>(type: "text", nullable: true),
                    ResponseSource = table.Column<int>(type: "integer", nullable: true),
                    ViewedIdentifier = table.Column<Guid>(type: "uuid", nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditEvents_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AuditEvents_Users_ActorId",
                        column: x => x.ActorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Members",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AccessRequestDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    Welcomed = table.Column<bool>(type: "boolean", nullable: false),
                    PlatformAdmin = table.Column<bool>(type: "boolean", nullable: false),
                    BillingEmail = table.Column<string>(type: "text", nullable: true),
                    Location = table.Column<Point>(type: "geometry (point)", nullable: true),
                    FormattedAddress = table.Column<string>(type: "text", nullable: true),
                    TimeZoneId = table.Column<string>(type: "text", nullable: true),
                    WorkingHours_Start = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    WorkingHours_End = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    IsGuest = table.Column<bool>(type: "boolean", nullable: false),
                    IsDefaultFirstResponder = table.Column<bool>(type: "boolean", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Members", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Members_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Members_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Memories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedById = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Memories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Memories_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Memories_Users_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Memories_Users_ModifiedById",
                        column: x => x.ModifiedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProxyLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Token = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    ExternalUrl = table.Column<string>(type: "text", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedById = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProxyLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProxyLinks_Users_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProxyLinks_Users_ModifiedById",
                        column: x => x.ModifiedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Settings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "citext", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    Scope = table.Column<string>(type: "text", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedById = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Settings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Settings_Users_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Settings_Users_ModifiedById",
                        column: x => x.ModifiedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserLists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedById = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "citext", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserLists", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserLists_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserLists_Users_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserLists_Users_ModifiedById",
                        column: x => x.ModifiedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Announcements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Text = table.Column<string>(type: "text", nullable: true),
                    ScheduledDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DateStartedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DateCompletedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SourceMessageId = table.Column<string>(type: "text", nullable: false),
                    SourceRoomId = table.Column<int>(type: "integer", nullable: false),
                    ScheduledJobId = table.Column<string>(type: "text", nullable: true),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedById = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Announcements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Announcements_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Announcements_Rooms_SourceRoomId",
                        column: x => x.SourceRoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Announcements_Users_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Announcements_Users_ModifiedById",
                        column: x => x.ModifiedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApiKey",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    OwnerId = table.Column<int>(type: "integer", nullable: false),
                    Token = table.Column<string>(type: "text", nullable: false),
                    ExpiresIn = table.Column<int>(type: "integer", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiKey", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApiKey_Members_OwnerId",
                        column: x => x.OwnerId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Conversations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoomId = table.Column<int>(type: "integer", nullable: false),
                    FirstMessageId = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    ImportedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastMessagePostedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FirstResponseOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TimeToRespondWarningNotificationSent = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ArchivedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastStateChangeOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    StartedById = table.Column<int>(type: "integer", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Conversations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Conversations_Members_StartedById",
                        column: x => x.StartedById,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Conversations_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Conversations_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LinkedIdentities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MemberId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<string>(type: "citext", nullable: false),
                    ExternalId = table.Column<string>(type: "text", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LinkedIdentities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LinkedIdentities_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LinkedIdentities_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MemberFacts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Content = table.Column<string>(type: "text", nullable: false),
                    SubjectId = table.Column<int>(type: "integer", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedById = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberFacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemberFacts_Members_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MemberFacts_Users_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MemberFacts_Users_ModifiedById",
                        column: x => x.ModifiedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MemberRole",
                columns: table => new
                {
                    MemberId = table.Column<int>(type: "integer", nullable: false),
                    RoleId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemberRole", x => new { x.MemberId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_MemberRole_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MemberRole_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RoomAssignments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoomId = table.Column<int>(type: "integer", nullable: false),
                    MemberId = table.Column<int>(type: "integer", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedById = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoomAssignments_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoomAssignments_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoomAssignments_Users_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoomAssignments_Users_ModifiedById",
                        column: x => x.ModifiedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserListEntry",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ListId = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedById = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserListEntry", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserListEntry_UserLists_ListId",
                        column: x => x.ListId,
                        principalTable: "UserLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserListEntry_Users_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserListEntry_Users_ModifiedById",
                        column: x => x.ModifiedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AnnouncementMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    SentDateUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ErrorMessage = table.Column<string>(type: "text", nullable: true),
                    RoomId = table.Column<int>(type: "integer", nullable: false),
                    AnnouncementId = table.Column<int>(type: "integer", nullable: false),
                    MessageId = table.Column<string>(type: "text", nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnnouncementMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnnouncementMessages_Announcements_AnnouncementId",
                        column: x => x.AnnouncementId,
                        principalTable: "Announcements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AnnouncementMessages_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConversationLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConversationId = table.Column<int>(type: "integer", nullable: false),
                    LinkType = table.Column<string>(type: "text", nullable: false),
                    ExternalId = table.Column<string>(type: "text", nullable: false),
                    CreatedById = table.Column<int>(type: "integer", nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversationLinks_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConversationLinks_Members_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "Members",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ConversationLinks_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConversationMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConversationId = table.Column<int>(type: "integer", nullable: false),
                    MemberId = table.Column<int>(type: "integer", nullable: false),
                    JoinedConversationAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastPostedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversationMembers_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConversationMembers_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MetricObservations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Metric = table.Column<string>(type: "text", nullable: false),
                    ConversationId = table.Column<int>(type: "integer", nullable: false),
                    RoomId = table.Column<int>(type: "integer", nullable: false),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false),
                    Value = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MetricObservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MetricObservations_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MetricObservations_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MetricObservations_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ConversationEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ConversationId = table.Column<int>(type: "integer", nullable: false),
                    MemberId = table.Column<int>(type: "integer", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    LinkId = table.Column<int>(type: "integer", nullable: true),
                    MessageId = table.Column<string>(type: "text", nullable: true),
                    MessageUrl = table.Column<string>(type: "text", nullable: true),
                    ExternalSource = table.Column<string>(type: "text", nullable: true),
                    ExternalMessageId = table.Column<string>(type: "text", nullable: true),
                    ExternalAuthorId = table.Column<string>(type: "text", nullable: true),
                    ExternalAuthor = table.Column<string>(type: "text", nullable: true),
                    OldState = table.Column<string>(type: "text", nullable: true),
                    NewState = table.Column<string>(type: "text", nullable: true),
                    Implicit = table.Column<bool>(type: "boolean", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversationEvents_ConversationLinks_LinkId",
                        column: x => x.LinkId,
                        principalTable: "ConversationLinks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConversationEvents_Conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "Conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ConversationEvents_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Packages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Language = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Readme = table.Column<string>(type: "text", nullable: false),
                    UsageText = table.Column<string>(type: "text", nullable: false),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false),
                    SkillId = table.Column<int>(type: "integer", nullable: false),
                    Listed = table.Column<bool>(type: "boolean", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedById = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Packages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Packages_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Packages_Users_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Packages_Users_ModifiedById",
                        column: x => x.ModifiedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PackageVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ReleaseNotes = table.Column<string>(type: "text", nullable: false),
                    MajorVersion = table.Column<int>(type: "integer", nullable: false),
                    MinorVersion = table.Column<int>(type: "integer", nullable: false),
                    PatchVersion = table.Column<int>(type: "integer", nullable: false),
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    PackageId = table.Column<int>(type: "integer", nullable: false),
                    CodeCacheKey = table.Column<string>(type: "text", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PackageVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PackageVersions_Packages_PackageId",
                        column: x => x.PackageId,
                        principalTable: "Packages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PackageVersions_Users_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Skills",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Language = table.Column<int>(type: "integer", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    CacheKey = table.Column<string>(type: "text", nullable: false),
                    UsageText = table.Column<string>(type: "text", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    SourcePackageVersionId = table.Column<int>(type: "integer", nullable: true),
                    Restricted = table.Column<bool>(type: "boolean", nullable: false),
                    Scope = table.Column<string>(type: "text", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedById = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "citext", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Skills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Skills_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Skills_PackageVersions_SourcePackageVersionId",
                        column: x => x.SourcePackageVersionId,
                        principalTable: "PackageVersions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Skills_Users_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Skills_Users_ModifiedById",
                        column: x => x.ModifiedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    MemberId = table.Column<int>(type: "integer", nullable: false),
                    SkillId = table.Column<int>(type: "integer", nullable: false),
                    Capability = table.Column<int>(type: "integer", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => new { x.MemberId, x.SkillId });
                    table.ForeignKey(
                        name: "FK_Permissions_Members_MemberId",
                        column: x => x.MemberId,
                        principalTable: "Members",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Permissions_Skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Permissions_Users_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SignalSubscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    SkillId = table.Column<int>(type: "integer", nullable: false),
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignalSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SignalSubscriptions_Skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SignalSubscriptions_Users_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SkillData",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Key = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: false),
                    SkillId = table.Column<int>(type: "integer", nullable: false),
                    Scope = table.Column<string>(type: "text", nullable: false),
                    ContextId = table.Column<string>(type: "text", nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedById = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkillData", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SkillData_Skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SkillData_Users_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SkillData_Users_ModifiedById",
                        column: x => x.ModifiedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SkillPatterns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    SkillId = table.Column<int>(type: "integer", nullable: false),
                    PatternType = table.Column<int>(type: "integer", nullable: false),
                    Pattern = table.Column<string>(type: "text", nullable: false),
                    CaseSensitive = table.Column<bool>(type: "boolean", nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedById = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkillPatterns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SkillPatterns_Skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SkillPatterns_Users_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SkillPatterns_Users_ModifiedById",
                        column: x => x.ModifiedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SkillSecrets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(127)", maxLength: 127, nullable: false),
                    KeyVaultSecretName = table.Column<string>(type: "character varying(127)", maxLength: 127, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    SkillId = table.Column<int>(type: "integer", nullable: false),
                    OrganizationId = table.Column<int>(type: "integer", nullable: false),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedById = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkillSecrets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SkillSecrets_Organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "Organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SkillSecrets_Skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SkillSecrets_Users_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SkillSecrets_Users_ModifiedById",
                        column: x => x.ModifiedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SkillTriggers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: false),
                    RoomId = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    ConversationId = table.Column<string>(type: "text", nullable: false),
                    SkillId = table.Column<int>(type: "integer", nullable: false),
                    TriggerType = table.Column<string>(type: "text", nullable: false),
                    ApiToken = table.Column<string>(type: "text", nullable: true),
                    CronSchedule = table.Column<string>(type: "text", nullable: true),
                    TimeZoneId = table.Column<string>(type: "text", nullable: true),
                    Arguments = table.Column<string>(type: "text", nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    Modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ModifiedById = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkillTriggers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SkillTriggers_Skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SkillTriggers_Users_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SkillTriggers_Users_ModifiedById",
                        column: x => x.ModifiedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SkillVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    UsageText = table.Column<string>(type: "text", nullable: true),
                    Code = table.Column<string>(type: "text", nullable: true),
                    SkillId = table.Column<int>(type: "integer", nullable: false),
                    CreatorId = table.Column<int>(type: "integer", nullable: false),
                    Restricted = table.Column<bool>(type: "boolean", nullable: true),
                    Scope = table.Column<string>(type: "text", nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SkillVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SkillVersions_Skills_SkillId",
                        column: x => x.SkillId,
                        principalTable: "Skills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SkillVersions_Users_CreatorId",
                        column: x => x.CreatorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Aliases_CreatorId",
                table: "Aliases",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_Aliases_ModifiedById",
                table: "Aliases",
                column: "ModifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_Aliases_Name_OrganizationId",
                table: "Aliases",
                columns: new[] { "Name", "OrganizationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Aliases_OrganizationId",
                table: "Aliases",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_AnnouncementMessages_AnnouncementId",
                table: "AnnouncementMessages",
                column: "AnnouncementId");

            migrationBuilder.CreateIndex(
                name: "IX_AnnouncementMessages_RoomId_AnnouncementId",
                table: "AnnouncementMessages",
                columns: new[] { "RoomId", "AnnouncementId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_CreatorId",
                table: "Announcements",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_ModifiedById",
                table: "Announcements",
                column: "ModifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_OrganizationId",
                table: "Announcements",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_SourceRoomId",
                table: "Announcements",
                column: "SourceRoomId");

            migrationBuilder.CreateIndex(
                name: "IX_ApiKey_OwnerId",
                table: "ApiKey",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_ActorId",
                table: "AuditEvents",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_Identifier",
                table: "AuditEvents",
                column: "Identifier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditEvents_OrganizationId",
                table: "AuditEvents",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationEvents_ConversationId",
                table: "ConversationEvents",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationEvents_LinkId",
                table: "ConversationEvents",
                column: "LinkId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationEvents_MemberId",
                table: "ConversationEvents",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationLinks_ConversationId",
                table: "ConversationLinks",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationLinks_CreatedById",
                table: "ConversationLinks",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationLinks_OrganizationId_LinkType_ExternalId",
                table: "ConversationLinks",
                columns: new[] { "OrganizationId", "LinkType", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMembers_ConversationId",
                table: "ConversationMembers",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationMembers_MemberId",
                table: "ConversationMembers",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_OrganizationId",
                table: "Conversations",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_RoomId_FirstMessageId",
                table: "Conversations",
                columns: new[] { "RoomId", "FirstMessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Conversations_StartedById",
                table: "Conversations",
                column: "StartedById");

            migrationBuilder.CreateIndex(
                name: "IX_Integrations_OrganizationId_Type",
                table: "Integrations",
                columns: new[] { "OrganizationId", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LinkedIdentities_MemberId_OrganizationId_Type",
                table: "LinkedIdentities",
                columns: new[] { "MemberId", "OrganizationId", "Type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LinkedIdentities_OrganizationId_Type_ExternalId",
                table: "LinkedIdentities",
                columns: new[] { "OrganizationId", "Type", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MemberFacts_CreatorId",
                table: "MemberFacts",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberFacts_ModifiedById",
                table: "MemberFacts",
                column: "ModifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_MemberFacts_SubjectId",
                table: "MemberFacts",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_MemberRole_RoleId",
                table: "MemberRole",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Members_OrganizationId",
                table: "Members",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Members_UserId_OrganizationId",
                table: "Members",
                columns: new[] { "UserId", "OrganizationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Memories_CreatorId",
                table: "Memories",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_Memories_ModifiedById",
                table: "Memories",
                column: "ModifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_Memories_OrganizationId",
                table: "Memories",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_MetricObservations_ConversationId",
                table: "MetricObservations",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_MetricObservations_Metric_Timestamp",
                table: "MetricObservations",
                columns: new[] { "Metric", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_MetricObservations_OrganizationId",
                table: "MetricObservations",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_MetricObservations_RoomId",
                table: "MetricObservations",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_PlatformId",
                table: "Organizations",
                column: "PlatformId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Organizations_PlatformType",
                table: "Organizations",
                column: "PlatformType");

            migrationBuilder.CreateIndex(
                name: "IX_Packages_CreatorId",
                table: "Packages",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_Packages_ModifiedById",
                table: "Packages",
                column: "ModifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_Packages_OrganizationId",
                table: "Packages",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Packages_SkillId",
                table: "Packages",
                column: "SkillId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Packages_SkillId_OrganizationId",
                table: "Packages",
                columns: new[] { "SkillId", "OrganizationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PackageVersions_CreatorId",
                table: "PackageVersions",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_PackageVersions_PackageId",
                table: "PackageVersions",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_CreatorId",
                table: "Permissions",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_Permissions_SkillId",
                table: "Permissions",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_ProxyLinks_CreatorId",
                table: "ProxyLinks",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_ProxyLinks_ModifiedById",
                table: "ProxyLinks",
                column: "ModifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_ProxyLinks_Token",
                table: "ProxyLinks",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Roles_Name",
                table: "Roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoomAssignments_CreatorId",
                table: "RoomAssignments",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomAssignments_MemberId",
                table: "RoomAssignments",
                column: "MemberId");

            migrationBuilder.CreateIndex(
                name: "IX_RoomAssignments_ModifiedById",
                table: "RoomAssignments",
                column: "ModifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_RoomAssignments_RoomId_Role_MemberId",
                table: "RoomAssignments",
                columns: new[] { "RoomId", "Role", "MemberId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_OrganizationId_PlatformRoomId",
                table: "Rooms",
                columns: new[] { "OrganizationId", "PlatformRoomId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Settings_CreatorId",
                table: "Settings",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_Settings_ModifiedById",
                table: "Settings",
                column: "ModifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_Settings_Scope_Name",
                table: "Settings",
                columns: new[] { "Scope", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SignalSubscriptions_CreatorId",
                table: "SignalSubscriptions",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_SignalSubscriptions_Name_SkillId",
                table: "SignalSubscriptions",
                columns: new[] { "Name", "SkillId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SignalSubscriptions_SkillId",
                table: "SignalSubscriptions",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_SkillData_CreatorId",
                table: "SkillData",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_SkillData_Key_SkillId_Scope_ContextId",
                table: "SkillData",
                columns: new[] { "Key", "SkillId", "Scope", "ContextId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SkillData_ModifiedById",
                table: "SkillData",
                column: "ModifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_SkillData_SkillId",
                table: "SkillData",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_SkillPatterns_CreatorId",
                table: "SkillPatterns",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_SkillPatterns_ModifiedById",
                table: "SkillPatterns",
                column: "ModifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_SkillPatterns_Name_SkillId",
                table: "SkillPatterns",
                columns: new[] { "Name", "SkillId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SkillPatterns_SkillId",
                table: "SkillPatterns",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_SkillPatterns_Slug_SkillId",
                table: "SkillPatterns",
                columns: new[] { "Slug", "SkillId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Skills_CreatorId",
                table: "Skills",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_Skills_ModifiedById",
                table: "Skills",
                column: "ModifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_Skills_Name_OrganizationId",
                table: "Skills",
                columns: new[] { "Name", "OrganizationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Skills_OrganizationId",
                table: "Skills",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Skills_SourcePackageVersionId",
                table: "Skills",
                column: "SourcePackageVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_SkillSecrets_CreatorId",
                table: "SkillSecrets",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_SkillSecrets_KeyVaultSecretName",
                table: "SkillSecrets",
                column: "KeyVaultSecretName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SkillSecrets_ModifiedById",
                table: "SkillSecrets",
                column: "ModifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_SkillSecrets_Name_SkillId",
                table: "SkillSecrets",
                columns: new[] { "Name", "SkillId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SkillSecrets_OrganizationId",
                table: "SkillSecrets",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_SkillSecrets_SkillId",
                table: "SkillSecrets",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_SkillTriggers_ApiToken",
                table: "SkillTriggers",
                column: "ApiToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SkillTriggers_CreatorId",
                table: "SkillTriggers",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_SkillTriggers_ModifiedById",
                table: "SkillTriggers",
                column: "ModifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_SkillTriggers_RoomId_SkillId_TriggerType",
                table: "SkillTriggers",
                columns: new[] { "RoomId", "SkillId", "TriggerType" },
                unique: true,
                filter: "\"RoomId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SkillTriggers_SkillId",
                table: "SkillTriggers",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_SkillVersions_CreatorId",
                table: "SkillVersions",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_SkillVersions_SkillId",
                table: "SkillVersions",
                column: "SkillId");

            migrationBuilder.CreateIndex(
                name: "IX_SlackEvents_EventId",
                table: "SlackEvents",
                column: "EventId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserListEntry_CreatorId",
                table: "UserListEntry",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_UserListEntry_ListId",
                table: "UserListEntry",
                column: "ListId");

            migrationBuilder.CreateIndex(
                name: "IX_UserListEntry_ModifiedById",
                table: "UserListEntry",
                column: "ModifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_UserLists_CreatorId",
                table: "UserLists",
                column: "CreatorId");

            migrationBuilder.CreateIndex(
                name: "IX_UserLists_ModifiedById",
                table: "UserLists",
                column: "ModifiedById");

            migrationBuilder.CreateIndex(
                name: "IX_UserLists_Name_OrganizationId",
                table: "UserLists",
                columns: new[] { "Name", "OrganizationId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserLists_OrganizationId",
                table: "UserLists",
                column: "OrganizationId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_NameIdentifier",
                table: "Users",
                column: "NameIdentifier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_PlatformUserId",
                table: "Users",
                column: "PlatformUserId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Packages_Skills_SkillId",
                table: "Packages",
                column: "SkillId",
                principalTable: "Skills",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Packages_Organizations_OrganizationId",
                table: "Packages");

            migrationBuilder.DropForeignKey(
                name: "FK_Skills_Organizations_OrganizationId",
                table: "Skills");

            migrationBuilder.DropForeignKey(
                name: "FK_Packages_Users_CreatorId",
                table: "Packages");

            migrationBuilder.DropForeignKey(
                name: "FK_Packages_Users_ModifiedById",
                table: "Packages");

            migrationBuilder.DropForeignKey(
                name: "FK_PackageVersions_Users_CreatorId",
                table: "PackageVersions");

            migrationBuilder.DropForeignKey(
                name: "FK_Skills_Users_CreatorId",
                table: "Skills");

            migrationBuilder.DropForeignKey(
                name: "FK_Skills_Users_ModifiedById",
                table: "Skills");

            migrationBuilder.DropForeignKey(
                name: "FK_Packages_Skills_SkillId",
                table: "Packages");

            migrationBuilder.DropTable(
                name: "Aliases");

            migrationBuilder.DropTable(
                name: "AnnouncementMessages");

            migrationBuilder.DropTable(
                name: "ApiKey");

            migrationBuilder.DropTable(
                name: "AuditEvents");

            migrationBuilder.DropTable(
                name: "ConversationEvents");

            migrationBuilder.DropTable(
                name: "ConversationMembers");

            migrationBuilder.DropTable(
                name: "DailyMetricsRollups");

            migrationBuilder.DropTable(
                name: "Integrations");

            migrationBuilder.DropTable(
                name: "LinkedIdentities");

            migrationBuilder.DropTable(
                name: "MemberFacts");

            migrationBuilder.DropTable(
                name: "MemberRole");

            migrationBuilder.DropTable(
                name: "Memories");

            migrationBuilder.DropTable(
                name: "MetricObservations");

            migrationBuilder.DropTable(
                name: "Permissions");

            migrationBuilder.DropTable(
                name: "ProxyLinks");

            migrationBuilder.DropTable(
                name: "RoomAssignments");

            migrationBuilder.DropTable(
                name: "Settings");

            migrationBuilder.DropTable(
                name: "SignalSubscriptions");

            migrationBuilder.DropTable(
                name: "SkillData");

            migrationBuilder.DropTable(
                name: "SkillPatterns");

            migrationBuilder.DropTable(
                name: "SkillSecrets");

            migrationBuilder.DropTable(
                name: "SkillTriggers");

            migrationBuilder.DropTable(
                name: "SkillVersions");

            migrationBuilder.DropTable(
                name: "SlackEvents");

            migrationBuilder.DropTable(
                name: "SlackEventsRollups");

            migrationBuilder.DropTable(
                name: "UserListEntry");

            migrationBuilder.DropTable(
                name: "Announcements");

            migrationBuilder.DropTable(
                name: "ConversationLinks");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DropTable(
                name: "UserLists");

            migrationBuilder.DropTable(
                name: "Conversations");

            migrationBuilder.DropTable(
                name: "Members");

            migrationBuilder.DropTable(
                name: "Rooms");

            migrationBuilder.DropTable(
                name: "Organizations");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Skills");

            migrationBuilder.DropTable(
                name: "PackageVersions");

            migrationBuilder.DropTable(
                name: "Packages");
        }
    }
}
