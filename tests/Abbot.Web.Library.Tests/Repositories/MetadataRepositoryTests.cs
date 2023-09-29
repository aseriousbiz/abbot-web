using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abbot.Common.TestHelpers;
using Serious.Abbot.Entities;
using Serious.Abbot.Repositories;
using Xunit;

public class MetadataRepositoryTests
{
    public class TheUpdateRoomMetadataAsyncMethod
    {
        [Fact]
        public async Task UpdatesRoomMetadataToMatchDictionary()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var actor = env.TestData.Member;
            var room = await env.CreateRoomAsync();
            var repository = env.Activate<MetadataRepository>();
            await repository.CreateAsync(new MetadataField { Type = MetadataFieldType.Room, Name = "field-1", Organization = organization }, actor.User);
            await repository.CreateAsync(new MetadataField { Type = MetadataFieldType.Room, Name = "field-2", Organization = organization }, actor.User);
            await repository.CreateAsync(new MetadataField { Type = MetadataFieldType.Room, Name = "field-3", Organization = organization }, actor.User);

            var metadata = new Dictionary<string, string?>
            {
                ["field-1"] = "value-one",
                ["field-2"] = null,
                ["field-3"] = "value-three",
            };
            await repository.UpdateRoomMetadataAsync(room, metadata, actor);

            await env.ReloadAsync(room);
            Assert.Collection(room.Metadata.OrderBy(m => m.MetadataField.Name),
                m => Assert.Equal(("field-1", "value-one"), (m.MetadataField.Name, m.Value)),
                m => Assert.Equal(("field-3", "value-three"), (m.MetadataField.Name, m.Value)));

            var metadataUpdate = new Dictionary<string, string?>
            {
                ["field-1"] = null,
                ["field-2"] = "value-two",
                ["field-3"] = "value-blah",
            };
            await repository.UpdateRoomMetadataAsync(room, metadataUpdate, actor);

            await env.ReloadAsync(room);
            Assert.Collection(room.Metadata.OrderBy(m => m.MetadataField.Name),
                m => Assert.Equal(("field-2", "value-two"), (m.MetadataField.Name, m.Value)),
                m => Assert.Equal(("field-3", "value-blah"), (m.MetadataField.Name, m.Value)));
        }
    }

    public class TheResolveValuesForRoomAsyncMethod
    {
        [Fact]
        public async Task UsesRoomMetadataAndFallbackValues()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var actor = env.TestData.Member;
            var room = await env.CreateRoomAsync();
            var repository = env.Activate<MetadataRepository>();
            await repository.CreateAsync(new MetadataField { Type = MetadataFieldType.Room, Name = "field-1", DefaultValue = "one", Organization = organization }, actor.User);
            await repository.CreateAsync(new MetadataField { Type = MetadataFieldType.Room, Name = "field-2", Organization = organization }, actor.User);
            await repository.CreateAsync(new MetadataField { Type = MetadataFieldType.Room, Name = "field-3", DefaultValue = "three", Organization = organization }, actor.User);
            await repository.CreateAsync(new MetadataField { Type = MetadataFieldType.Room, Name = "field-4", DefaultValue = "four", Organization = organization }, actor.User);

            var metadata = new Dictionary<string, string?>
            {
                ["field-1"] = null,
                ["field-2"] = null,
                ["field-3"] = "value-three",
            };
            await repository.UpdateRoomMetadataAsync(room, metadata, actor);

            var result = await repository.ResolveValuesForRoomAsync(room);

            Assert.Collection(result.OrderBy(k => k.Key),
                k => Assert.Equal(("field-1", "one"), (k.Key, k.Value)),
                k => Assert.Equal(("field-2", null), (k.Key, k.Value)),
                k => Assert.Equal(("field-3", "value-three"), (k.Key, k.Value)),
                k => Assert.Equal(("field-4", "four"), (k.Key, k.Value)));
        }
    }
}
