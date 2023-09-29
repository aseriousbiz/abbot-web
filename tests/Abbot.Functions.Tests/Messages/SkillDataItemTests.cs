using System.Collections.Generic;
using Serious.Abbot.Functions.Execution;
using Serious.Abbot.Functions.Messages;
using Serious.Abbot.Serialization;
using Serious.Abbot.Storage;
using Serious.TestHelpers;
using Xunit;

public class SkillDataItemTests
{
    public class TheValueProperty
    {
        [Fact]
        public void DeserializesJsonToDynamic()
        {
            var list = new List<string>();
            var json = AbbotJsonFormat.Default.Serialize(list);
            var item = new SkillDataItem("key", json, new BrainSerializer(new FakeSkillContextAccessor()));

            dynamic result = item.Value;
            result.Add("Test");

            Assert.Equal("Test", (string)result[0]);
        }
    }

    public class TheGetValueAsMethod
    {
        [Fact]
        public void DeserializesJsonToType()
        {
            var list = new List<string> { "Test" };
            var json = AbbotJsonFormat.Default.Serialize(list);
            var item = new SkillDataItem("key", json, new BrainSerializer(new FakeSkillContextAccessor()));

            var result = item.GetValueAs<List<string>>();

            Assert.NotNull(result);
            Assert.Equal("Test", result[0]);
        }
    }
}
