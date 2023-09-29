using System.Runtime.CompilerServices;
using Argon;
using Microsoft.EntityFrameworkCore;
using Serious;
using Serious.Abbot.Entities;
using Serious.Cryptography;
using Serious.TestHelpers;

namespace Abbot.Common.TestHelpers.Verify;

public static class VerifyConfig
{
    [ModuleInitializer]
    public static void Initialize()
    {
        VerifyDiffPlex.Initialize();
        VerifierSettings.AddExtraSettings(s => {
            s.TypeNameHandling = TypeNameHandling.Auto;

            s.Converters.Add(new IdConverter());
            s.Converters.Add(new OpenAIConverter());
            s.Converters.Add(new NewtonsoftJTokenConverter());
        });

        // We use a clock abstraction, so there's no need to scrub times.
        VerifierSettings.DontScrubDateTimes();
        VerifierSettings.ScrubInlineGuids();
        VerifierSettings.AddScrubber(
            sb => {
                if (System.Diagnostics.Activity.Current is { Id: not null or "" } activity)
                {
                    sb.Replace(activity.Id, "00-TraceId-ParentId-00");
                }
            });

        VerifyMassTransit.Initialize();

        // This is a hack, but it's the hack they suggest :)
        var dbOptions = new DbContextOptionsBuilder<AbbotContext>();
        dbOptions.UseNpgsql("fake",
            npgsqlOptions => {
                npgsqlOptions.UseNetTopologySuite();
            });
        var model = new AbbotContext(
            dbOptions.Options,
            new FakeDataProtectionProvider(),
            IClock.System).Model;

        VerifyEntityFramework.IgnoreNavigationProperties(model);

        // Properties defined on base classes don't get excluded by the above :(
        VerifierSettings.IgnoreMember<OrganizationEntityBase<AuditEventBase>>(b => b.Organization);
        VerifierSettings.IgnoreMember<OrganizationEntityBase<Conversation>>(b => b.Organization);
        VerifierSettings.IgnoreMember<OrganizationEntityBase<ConversationLink>>(b => b.Organization);
        VerifierSettings.IgnoreMember<OrganizationEntityBase<Room>>(b => b.Organization);
        VerifierSettings.IgnoreMember<TrackedEntityBase>(b => b.Creator);
        VerifierSettings.IgnoreMember<TrackedEntityBase>(b => b.ModifiedBy);

        // Nor do automatic many-to-many navigations
        VerifierSettings.IgnoreMember<Conversation>(c => c.Assignees);

        // Deserialized Properties diffs better
        VerifierSettings.IgnoreMember<Conversation>(c => c.SerializedProperties);

        // Our FakeDataProtectionProvider is consistent _within_ a test, but SecretString uses a random nonce.
        // This means you can't really snapshot the encrypted value.
        VerifierSettings.IgnoreMember<SecretString>(s => s.ProtectedValue);

        // No need to verify a copy of the definition
        VerifierSettings.IgnoreMember<PlaybookRun>(r => r.SerializedDefinition);
    }
}
