using System.Collections.Generic;
using System.Threading.Tasks;
using Serious.Abbot.Functions.Messages;
using Serious.Abbot.Messages;
using Serious.Abbot.Models;
using Serious.Abbot.Scripting;
using Serious.TestHelpers;
using Xunit;

public class HttpRequestExtensionsTests
{
    public class TheReadAsSkillMessageAsyncTests
    {
        [Fact]
        public async Task CanReadMentionArguments()
        {
            var message = new SkillMessage
            {
                SkillInfo = new SkillInfo
                {
                    TokenizedArguments = new List<Argument>
                    {
                        new MentionArgument
                        {
                            Mentioned = new PlatformUser(
                                "U1234567",
                                "user",
                                "user",
                                "example@example.com",
                                "America/Los_Angeles",
                                "420 elm street",
                                42, 23),
                            Value = "<@U1234567>",
                            OriginalText = "<@U1234567>"
                        },
                        new() {Value = "skill", OriginalText = "skill"},
                        new RoomArgument
                        {
                            Value = "<#room-id|room-name>",
                            OriginalText = "<#room-id|room-name>",
                            Room = new PlatformRoom("room-id", "room-name")
                        }
                    },
                    Mentions = new List<PlatformUser>
                    {
                        new() { Id = "U1234567", UserName = "Paul Nakata" }
                    }
                }
            };
            var request = await FakeHttpRequestData.CreateAsync(message);

            var result = await request.ReadAsSkillMessageAsync();

            var skillInfo = result.SkillInfo;
            var mention = Assert.IsAssignableFrom<IMentionArgument>(skillInfo.TokenizedArguments[0]);
            Assert.Equal("U1234567", mention.Mentioned.Id);
            Assert.Equal("420 elm street", mention.Mentioned.Location?.FormattedAddress);
            Assert.NotNull(mention.Mentioned.Location?.Coordinate);
            Assert.Equal(42, mention.Mentioned.Location?.Coordinate.Latitude);
            Assert.Equal(23, mention.Mentioned.Location?.Coordinate.Longitude);
            Assert.NotNull(mention.Mentioned.TimeZone);
            Assert.Equal("America/Los_Angeles", mention.Mentioned.TimeZone?.Id);
            Assert.IsAssignableFrom<IArgument>(skillInfo.TokenizedArguments[1]);
            Assert.IsNotType<MentionArgument>(skillInfo.TokenizedArguments[1]);
            Assert.Equal("skill", skillInfo.TokenizedArguments[1].Value);
            var roomArgument = Assert.IsAssignableFrom<IRoomArgument>(skillInfo.TokenizedArguments[2]);
            Assert.Equal("<#room-id|room-name>", roomArgument.Value);
            Assert.Equal("room-id", roomArgument.Room.Id);
            Assert.Equal("room-name", roomArgument.Room.Name);
        }

        [Fact]
        public async Task CanReadMentionArgumentsWithNoTimeZoneOrLocation()
        {
            var message = new SkillMessage
            {
                SkillInfo = new SkillInfo
                {
                    TokenizedArguments = new List<Argument>
                    {
                        new MentionArgument
                        {
                            Mentioned = new PlatformUser(
                                "U1234567",
                                "user",
                                "user",
                                "example@example.com",
                                null,
                                null,
                                null,
                                null),
                            Value = "<@U1234567>",
                            OriginalText = "<@U1234567>"
                        },
                        new() {Value = "skill", OriginalText = "skill"},
                    },
                    Mentions = new List<PlatformUser>
                    {
                        new() { Id = "U1234567", UserName = "Paul Nakata" }
                    }
                }
            };
            var request = await FakeHttpRequestData.CreateAsync(message);

            var result = await request.ReadAsSkillMessageAsync();

            var skillInfo = result.SkillInfo;
            var mention = Assert.IsAssignableFrom<IMentionArgument>(skillInfo.TokenizedArguments[0]);
            Assert.Equal("U1234567", mention.Mentioned.Id);
            Assert.Null(mention.Mentioned.Location?.FormattedAddress);
            Assert.Null(mention.Mentioned.Location?.Coordinate);
            Assert.Null(mention.Mentioned.TimeZone);
            Assert.IsAssignableFrom<IArgument>(skillInfo.TokenizedArguments[1]);
            Assert.IsNotType<MentionArgument>(skillInfo.TokenizedArguments[1]);
            Assert.Equal("skill", skillInfo.TokenizedArguments[1].Value);
        }

        [Fact]
        public async Task CanReadPattern()
        {
            var message = new SkillMessage
            {
                SkillInfo = new SkillInfo
                {
                    Pattern = new PatternMessage
                    {
                        Name = "my pattern",
                        Description = "A cool pattern",
                        PatternType = PatternType.EndsWith,
                        CaseSensitive = true
                    },
                    TokenizedArguments = new List<Argument> { new() { Value = "skill", OriginalText = "skill" } },
                    Mentions = new List<PlatformUser>()
                }
            };
            var request = await FakeHttpRequestData.CreateAsync(message);

            var result = await request.ReadAsSkillMessageAsync();

            var skillInfo = result.SkillInfo;
            Assert.NotNull(skillInfo.Pattern);
            Assert.Equal("my pattern", skillInfo.Pattern.Name);
            Assert.Equal("A cool pattern", skillInfo.Pattern.Description);
            Assert.Equal(PatternType.EndsWith, skillInfo.Pattern.PatternType);
            Assert.True(skillInfo.Pattern.CaseSensitive);
        }
    }

    public class TheGetTraceParentMethod
    {
        [Fact]
        public void RetrievesFirstTraceParentHeader()
        {
            var request = new FakeHttpRequestData
            {
                Headers =
                {
                    {"traceparent", "the-trace-parent"},
                    {"traceparent", "some-other-trace-parent"}
                }
            };

            var result = request.GetTraceParent();

            Assert.Equal("the-trace-parent", result);
        }

        [Fact]
        public void ReturnsNullIfHeaderMissing()
        {
            var request = new FakeHttpRequestData();

            var result = request.GetTraceParent();

            Assert.Null(result);
        }
    }

    public class TheGetTraceStateMethod
    {
        [Fact]
        public void RetrievesFirstTraceParentHeader()
        {
            var request = new FakeHttpRequestData
            {
                Headers =
                {
                    {"tracestate", "the-trace-state"},
                    {"tracestate", "some-other-trace-state"}
                }
            };

            var result = request.GetTraceState();

            Assert.Equal("the-trace-state", result);
        }

        [Fact]
        public void ReturnsNullIfHeaderMissing()
        {
            var request = new FakeHttpRequestData();

            var result = request.GetTraceState();

            Assert.Null(result);
        }
    }
}
