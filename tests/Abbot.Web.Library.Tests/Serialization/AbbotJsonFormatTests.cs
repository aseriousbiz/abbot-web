using System.Reflection;
using System.Text;
using DiffPlex.DiffBuilder;
using Microsoft.AspNetCore.Http;
using Microsoft.Bot.Schema;
using Newtonsoft.Json.Linq;
using Serious.Abbot.Entities;
using Serious.Abbot.Messages;
using Serious.Abbot.Models;
using Serious.Abbot.Serialization;
using Serious.Slack;
using ChangeType = DiffPlex.DiffBuilder.Model.ChangeType;

/// <summary>
/// Tests for <see cref="AbbotJsonFormat"/>.
/// When we add support for System.Text.Json, these tests can help ensure that the serialization is identical.
/// </summary>
public class AbbotJsonFormatTests
{
    public class TheConvertMethod
    {
        [Fact]
        public void CanConvertBoolean()
        {
            const string json = """
                {
                    "value": true
                }
            """;
            var dict = AbbotJsonFormat.Default.Deserialize<IDictionary<string, object>>(json).Require();
            var val = AbbotJsonFormat.Default.Convert<bool>(dict["value"]);
            Assert.True(val);
        }

        [Fact]
        public void CanConvertFloat()
        {
            const string json = """
                {
                    "value": 4.2
                }
            """;
            var dict = AbbotJsonFormat.Default.Deserialize<IDictionary<string, object>>(json).Require();
            var val = AbbotJsonFormat.Default.Convert<double>(dict["value"]);
            Assert.Equal(4.2, val);
        }

        [Fact]
        public void CanConvertInteger()
        {
            const string json = """
                {
                    "value": 42
                }
            """;
            var dict = AbbotJsonFormat.Default.Deserialize<IDictionary<string, object>>(json).Require();
            var val = AbbotJsonFormat.Default.Convert<long>(dict["value"]);
            Assert.Equal(42, val);
        }

        [Fact]
        public void CanConvertString()
        {
            const string json = """
                {
                    "value": "foo"
                }
            """;
            var dict = AbbotJsonFormat.Default.Deserialize<IDictionary<string, object>>(json).Require();
            var val = AbbotJsonFormat.Default.Convert<string>(dict["value"]);
            Assert.Equal("foo", val);
        }

        [Fact]
        public void CanConvertDictionaryToObject()
        {
            const string json = """
                {"stringProp":"foo", "numProp": 42}
            """;
            var dict = AbbotJsonFormat.Default.Deserialize<IDictionary<string, object?>>(json);
            var val = AbbotJsonFormat.Default.Convert<TestObj>(dict);
            Assert.NotNull(val);
            Assert.Equal("foo", val.StringProp);
            Assert.Equal(42, val.NumProp);
        }

        [Fact]
        public void CanConvertJObjectToObject()
        {
            const string json = """
                {
                    "value": {"stringProp":"foo", "numProp": 42}
                }
            """;
            var dict = AbbotJsonFormat.Default.Deserialize<IDictionary<string, object?>>(json).Require();
            var jobj = dict["value"].Require<JObject>();
            var val = AbbotJsonFormat.Default.Convert<TestObj>(jobj);
            Assert.NotNull(val);
            Assert.Equal("foo", val.StringProp);
            Assert.Equal(42, val.NumProp);
        }

        [Fact]
        public void CanConvertListOfDictionariesToListOfObjects()
        {
            const string json = """
                [
                    {"stringProp":"foo", "numProp": 42},
                    {"stringProp":"bar", "numProp": 24}
                ]
            """;
            var dict = AbbotJsonFormat.Default.Deserialize<IReadOnlyList<IDictionary<string, object>>>(json).Require();
            Assert.All(dict, o => Assert.IsType<Dictionary<string, object?>>(o));

            var val = AbbotJsonFormat.Default.Convert<IReadOnlyList<TestObj>>(dict);
            Assert.NotNull(val);
            Assert.Collection(val,
                a1 => {
                    Assert.Equal("foo", a1.StringProp);
                    Assert.Equal(42, a1.NumProp);
                },
                a2 => {
                    Assert.Equal("bar", a2.StringProp);
                    Assert.Equal(24, a2.NumProp);
                });
        }

        [Fact]
        public void CanConvertJArrayToListOfObjects()
        {
            const string json = """
                {
                    "value": [
                        {"stringProp":"foo", "numProp": 42},
                        {"stringProp":"bar", "numProp": 24}
                    ]
                }
            """;
            var dict = AbbotJsonFormat.Default.Deserialize<IDictionary<string, object>>(json).Require();
            var jarr = Assert.IsType<JArray>(dict["value"]);
            Assert.All(jarr, o => Assert.IsType<JObject>(o));

            var val = AbbotJsonFormat.Default.Convert<IReadOnlyList<TestObj>>(jarr);
            Assert.NotNull(val);
            Assert.Collection(val,
                a1 => {
                    Assert.Equal("foo", a1.StringProp);
                    Assert.Equal(42, a1.NumProp);
                },
                a2 => {
                    Assert.Equal("bar", a2.StringProp);
                    Assert.Equal(24, a2.NumProp);
                });
        }

        [Fact]
        public void CanConvertEnumString()
        {
            const string json = """
                {
                    "value": "foo"
                }
            """;
            var dict = AbbotJsonFormat.Default.Deserialize<IDictionary<string, object>>(json).Require();
            var val = AbbotJsonFormat.Default.Convert<TestEnum>(dict["value"]);
            Assert.Equal(TestEnum.Foo, val);
        }

        [Fact]
        public void CanConvertEnumNumber()
        {
            const string json = """
                {
                    "value": 22
                }
            """;
            var dict = AbbotJsonFormat.Default.Deserialize<IDictionary<string, object>>(json).Require();
            var val = AbbotJsonFormat.Default.Convert<TestEnum>(dict["value"]);
            Assert.Equal(TestEnum.Bar, val);
        }

        public enum TestEnum
        {
            Foo = 11,
            Bar = 22,
            Baz = 33,
        }

        public record TestObj
        {
            public required string StringProp { get; init; }
            public required int NumProp { get; init; }
        }
    }

    [Theory]
    [MemberData(nameof(JsonFormats))]
    public async Task CanRoundTripPlatformUser(AbbotJsonFormat format)
    {
        var user = new PlatformUser(
            "U001",
            "cloud",
            "Cloud Strife",
            "cstrife@ava.lanche",
            "America/Vancouver",
            "185 Shinra Drive, Sector 4, Midgar, A1B 2C3",
            12.42,
            42.12);

        await RunRoundTripTestAsync("PlatformUser.baseline.json", format, user);
    }

    [Theory]
    [MemberData(nameof(JsonFormats))]
    public async Task CanRoundTripRegularArgument(AbbotJsonFormat format)
    {
        var argument = new Argument("The Value", "\"The Value\"");
        await RunRoundTripTestAsync("Argument.baseline.json", format, argument);
    }

    [Theory]
    [MemberData(nameof(JsonFormats))]
    public async Task CanRoundTripMentionArgument(AbbotJsonFormat format)
    {
        var argument = new MentionArgument(
            "<@U1234>",
            "<@U1234>",
            new PlatformUser(
                "U1234",
                "ciri",
                "Ciri",
                null,
                null,
                "Idris"));

        await RunRoundTripTestAsync("MentionArgument.baseline.json", format, argument);
    }

    [Theory]
    [MemberData(nameof(JsonFormats))]
    public async Task CanRoundTripRoomArgument(AbbotJsonFormat format)
    {
        var argument = new RoomArgument(
            "<#C01234|room-name>",
            "<#C01234|room-name>",
            new PlatformRoom("C01234", "room-name"));

        await RunRoundTripTestAsync("RoomArgument.baseline.json", format, argument);
    }

    [Theory]
    [MemberData(nameof(JsonFormats))]
    public async Task CanRoundTripRoom(AbbotJsonFormat format)
    {
        var room = new PlatformRoom("C01234", "room-name");
        await RunRoundTripTestAsync("Room.baseline.json", format, room);
    }

    [Theory]
    [MemberData(nameof(JsonFormats))]
    public async Task CanRoundTripChatConversation(AbbotJsonFormat format)
    {
        var conversation = new ChatConversation(
            "123",
            "1111.4444",
            "Convo Title",
            new Uri("https://app.ab.bot/conversations/123"),
            new PlatformRoom("C999", "midgar"),
            new PlatformUser("U888",
                "you",
                "You",
                "you@ab.bot",
                "America/Vancouver",
                "123 Jump St.",
                42.24,
                24.42),
            new DateTimeOffset(2022, 01, 01, 01, 02, 03, TimeSpan.FromHours(8)),
            new DateTimeOffset(2022, 01, 01, 01, 02, 03, TimeSpan.FromHours(8)),
            new List<IChatUser>()
            {
                new PlatformUser("U888",
                    "you",
                    "You",
                    "you@ab.bot",
                    "America/Vancouver",
                    "123 Jump St.",
                    42.24,
                    24.42)
            });
        await RunRoundTripTestAsync("ChatConversation.baseline.json", format, conversation);
    }

    [Theory]
    [MemberData(nameof(JsonFormats))]
    public async Task CanRoundTripChatAddress(AbbotJsonFormat format)
    {
        var room = new ChatAddress(ChatAddressType.Room, "C01234", "1234.5678");
        await RunRoundTripTestAsync("ChatAddress.WithThread.baseline.json", format, room);
    }

    [Theory]
    [MemberData(nameof(JsonFormats))]
    public async Task CanRoundTripChatAddressWithoutThread(AbbotJsonFormat format)
    {
        var room = new ChatAddress(ChatAddressType.Room, "C01234");
        await RunRoundTripTestAsync("ChatAddress.NoThread.baseline.json", format, room);
    }

    [Theory]
    [MemberData(nameof(JsonFormats))]
    public async Task CanDeserializeProactiveBotMessageWithNumericEnums(AbbotJsonFormat format)
    {
        // Skill runners may use numeric enums, that should still work.
        await RunDeserializeTestAsync<ProactiveBotMessage>("ProactiveBotMessage.NumericEnums.baseline.json",
            format,
            actual => {
                Assert.Equal(new ChatAddress(ChatAddressType.Room, "C123", "9999.9999"), actual.Options?.To);
            }
        );
    }

    [Theory]
    [MemberData(nameof(JsonFormats))]
    public async Task CanRoundTripProactiveBotMessage(AbbotJsonFormat format)
    {
        var message = new ProactiveBotMessage()
        {
            Message = "A Message",
            Attachments = new List<MessageAttachment>()
            {
                new()
                {
                    Buttons = new List<ButtonMessage>()
                    {
                        new()
                        {
                            Arguments = "args",
                            Style = "C O O L",
                            Title = "Click Me",
                        }
                    },
                    ButtonsLabel = "The label",
                    Color = "#beefcafe",
                    ImageUrl = "https://images.example.com/cool.png",
                    Title = "A title",
                    TitleUrl = "https://example.com/cool"
                }
            },
            ConversationReference = new ConversationReference()
            {
                ActivityId = "a-b-c",
                Conversation = new ConversationAccount()
                {
                    Id = "B1:T1:C1"
                }
            },
            Schedule = 42,
            SkillId = 24,
            Options = new ProactiveBotMessageOptions()
            {
                To = new ChatAddress(ChatAddressType.Room, "C123", "9999.9999")
            }
        };

        await RunRoundTripTestAsync("ProactiveBotMessage.baseline.json",
            format,
            message,
            actual => {
                Assert.Equal("A Message", actual.Message);
                Assert.Equal(42, actual.Schedule);
                Assert.Equal(24, actual.SkillId);
                Assert.Equal("a-b-c", actual.ConversationReference.ActivityId);
                Assert.Equal("B1:T1:C1", actual.ConversationReference.Conversation?.Id);
                Assert.Equal(new ChatAddress(ChatAddressType.Room, "C123", "9999.9999"), actual.Options?.To);
                Assert.NotNull(actual.Attachments);
                Assert.Collection(actual.Attachments,
                    actualAttachment => {
                        Assert.Equal("The label", actualAttachment.ButtonsLabel);
                        Assert.Equal("#beefcafe", actualAttachment.Color);
                        Assert.Equal("https://images.example.com/cool.png", actualAttachment.ImageUrl);
                        Assert.Equal("A title", actualAttachment.Title);
                        Assert.Equal("https://example.com/cool", actualAttachment.TitleUrl);
                        Assert.NotNull(actualAttachment.Buttons);
                        Assert.Collection(actualAttachment.Buttons,
                            actualButton => {
                                Assert.Equal("args", actualButton.Arguments);
                                Assert.Equal("C O O L", actualButton.Style);
                                Assert.Equal("Click Me", actualButton.Title);
                            });
                    });
            });
    }

    [Theory]
    [MemberData(nameof(JsonFormats))]
    public async Task CanDeserializeUserProfileResponse(AbbotJsonFormat format)
    {
        var baseline = await ReadBaselineAsync("users.profile.get.json");
        var deserialized = format.Deserialize<UserProfileResponse>(baseline);
        Assert.NotNull(deserialized);
        Assert.True(deserialized.Ok);
        Assert.Equal("Ashley Stanton-Nurse", deserialized.Body.RealName);

        var fields = deserialized.Body.Fields.Require()
            .OrderBy(p => p.Key)
            .Select(p => (p.Key, p.Value.Value!, p.Value.Alt!)).ToArray();
        Assert.Equal(new[]
        {
            ("Xf044FJYCLMV", "Yoo", ""),
            ("Xf045KRT165N", "Hi", "Alt"),
        }, fields);
    }

    [Theory]
    [MemberData(nameof(JsonFormats))]
    public async Task CanRoundTripSkillMessage(AbbotJsonFormat format)
    {
        var message = new SkillMessage
        {
            SkillInfo = new SkillInfo
            {
                PlatformId = "T123",
                Arguments = "<@U123> skill <#room-id|room-name>",
                CommandText = "skill <#room-id|room-name>",
                Pattern = new PatternMessage
                {
                    CaseSensitive = true,
                    Description = "A pattern",
                    Name = "PatternName",
                    Pattern = "[A-Z]*",
                    PatternType = PatternType.RegularExpression
                },
                Bot = new PlatformUser("U999",
                    "abbot",
                    "Abbot",
                    "me@ab.bot",
                    "America/Vancouver",
                    "123 Jump St.",
                    42.24,
                    24.42),
                From = new PlatformUser("U888",
                    "you",
                    "You",
                    "you@ab.bot",
                    "America/Vancouver",
                    "123 Jump St.",
                    42.24,
                    24.42),
                Mentions = new List<PlatformUser>
                {
                    new()
                    {
                        Id = "U123",
                        UserName = "Paul Nakata"
                    }
                },
                Request = new HttpTriggerRequest()
                {
                    ContentType = "application/json",
                    Form = new Dictionary<string, string[]>()
                    {
                        {
                            "Foo", new[]
                            {
                                "Bar",
                                "Baz"
                            }
                        }
                    },
                    Headers = new Dictionary<string, string[]>()
                    {
                        {
                            "Authorize", new[]
                            {
                                "Bar",
                                "Baz"
                            }
                        }
                    },
                    Query = new Dictionary<string, string[]>()
                    {
                        {
                            "s", new[]
                            {
                                "Bar",
                                "Baz"
                            }
                        }
                    },
                    Url = "https://example.com",
                    HttpMethod = HttpMethods.Connect,
                    IsForm = true,
                    IsJson = true,
                    RawBody = "RAW BODY",
                },
                TokenizedArguments = new List<Argument>
                {
                    new MentionArgument
                    {
                        Mentioned = new PlatformUser(
                            "U123",
                            "user",
                            "user",
                            "example@example.com",
                            "America/Los_Angeles",
                            "420 elm street",
                            42,
                            23),
                        Value = "<@U123>",
                        OriginalText = "<@U123>"
                    },
                    new()
                    {
                        Value = "skill",
                        OriginalText = "skill"
                    },
                    new RoomArgument
                    {
                        Value = "<#room-id|room-name>",
                        OriginalText = "<#room-id|room-name>",
                        Room = new PlatformRoom("room-id",
                            "room-name")
                    }
                },
                IsChat = true,
                IsInteraction = true,
                IsRequest = true,
                IsSignal = true,
#pragma warning disable CS0618
                MessageId = "1234.5678",
                MessageUrl = new Uri("https://example.com/foo/bar"),
#pragma warning restore CS0618
                Message = new SourceMessageInfo
                {
                    MessageId = "1234.5678",
                    Text = "some text",
                    ThreadId = null,
                    MessageUrl = new Uri("https://example.com/foo/bar"),
                    Author = new PlatformUser("U456",
                        "cloud",
                        "Cloud Strife"),
                },
                Room = new PlatformRoom("C123",
                    "room"),
                SkillName = "skillname",
                SkillUrl = new Uri("http://example.com/skill"),
                Customer = null,
                IsPlaybook = false,
            },
            RunnerInfo = new SkillRunnerInfo()
            {
                Code = "CODE",
                Language = CodeLanguage.Ink,
                Timestamp = 99999,
                AuditIdentifier = Guid.Empty,
                MemberId = 53,
                SkillId = 99,
                UserId = 420
            },
            SignalInfo = new SignalMessage()
            {
                Arguments = "a b c",
                Name = "signal",
                Source = new SignalSourceMessage()
                {
                    Arguments = "d e f"
                }
            },
            ConversationInfo = new ChatConversation(
                "123",
                "1111.4444",
                "Convo Title",
                new Uri("https://app.ab.bot/conversations/123"),
                new PlatformRoom("C999", "midgar"),
                new PlatformUser("U888",
                    "you",
                    "You",
                    "you@ab.bot",
                    "America/Vancouver",
                    "123 Jump St.",
                    42.24,
                    24.42),
               new DateTimeOffset(2022, 01, 01, 01, 02, 03, TimeSpan.FromHours(8)),
               new DateTimeOffset(2022, 01, 01, 01, 02, 03, TimeSpan.FromHours(8)),
               new List<IChatUser>()
               {
                    new PlatformUser("U888",
                       "you",
                       "You",
                       "you@ab.bot",
                       "America/Vancouver",
                       "123 Jump St.",
                       42.24,
                       24.42)
               }),
        };

        await RunRoundTripTestAsync("SkillMessage.baseline.json",
            format,
            message,
            parsed => {
                Assert.Equal("T123", parsed.SkillInfo.PlatformId);
                Assert.Equal("<@U123> skill <#room-id|room-name>", parsed.SkillInfo.Arguments);
                Assert.Equal(true, parsed.SkillInfo.Pattern?.CaseSensitive);
                Assert.Equal("A pattern", parsed.SkillInfo.Pattern?.Description);
                Assert.Equal("PatternName", parsed.SkillInfo.Pattern?.Name);
                Assert.Equal("[A-Z]*", parsed.SkillInfo.Pattern?.Pattern);
                Assert.Equal(PatternType.RegularExpression, parsed.SkillInfo.Pattern?.PatternType);
                Assert.Equal(
                    new PlatformUser("U999",
                        "abbot",
                        "Abbot",
                        "me@ab.bot",
                        "America/Vancouver",
                        "123 Jump St.",
                        42.24,
                        24.42),
                    parsed.SkillInfo.Bot);

                Assert.Equal(new PlatformUser("U888",
                        "you",
                        "You",
                        "you@ab.bot",
                        "America/Vancouver",
                        "123 Jump St.",
                        42.24,
                        24.42),
                    parsed.SkillInfo.From);

                Assert.Equal(new List<PlatformUser>
                    {
                        new()
                        {
                            Id = "U123",
                            UserName = "Paul Nakata"
                        }
                    },
                    parsed.SkillInfo.Mentions);

                Assert.Equal("application/json", parsed.SkillInfo.Request?.ContentType);
                Assert.Equal(new Dictionary<string, string[]>()
                    {
                        {"Foo", new[] {"Bar", "Baz"}}
                    },
                    parsed.SkillInfo.Request?.Form);

                Assert.Equal(new Dictionary<string, string[]>()
                    {
                        {"Authorize", new[] {"Bar", "Baz"}}
                    },
                    parsed.SkillInfo.Request?.Headers);

                Assert.Equal(new Dictionary<string, string[]>()
                    {
                        {"s", new[] {"Bar", "Baz"}}
                    },
                    parsed.SkillInfo.Request?.Query);

                Assert.Equal("https://example.com", parsed.SkillInfo.Request?.Url);
                Assert.Equal(HttpMethods.Connect, parsed.SkillInfo.Request?.HttpMethod);
                Assert.Equal(true, parsed.SkillInfo.Request?.IsForm);
                Assert.Equal(true, parsed.SkillInfo.Request?.IsJson);
                Assert.Equal("RAW BODY", parsed.SkillInfo.Request?.RawBody);
                Assert.Equal(new List<Argument>
                    {
                        new MentionArgument
                        {
                            Mentioned = new PlatformUser(
                                "U123",
                                "user",
                                "user",
                                "example@example.com",
                                "America/Los_Angeles",
                                "420 elm street",
                                42,
                                23),
                            Value = "<@U123>",
                            OriginalText = "<@U123>"
                        },
                        new()
                        {
                            Value = "skill",
                            OriginalText = "skill"
                        },
                        new RoomArgument
                        {
                            Value = "<#room-id|room-name>",
                            OriginalText = "<#room-id|room-name>",
                            Room = new PlatformRoom("room-id", "room-name")
                        }
                    },
                    parsed.SkillInfo.TokenizedArguments);

                Assert.Equal(true, parsed.SkillInfo.IsChat);
                Assert.Equal(true, parsed.SkillInfo.IsInteraction);
                Assert.Equal(true, parsed.SkillInfo.IsRequest);
                Assert.Equal(true, parsed.SkillInfo.IsSignal);
                Assert.Equal("1234.5678", parsed.SkillInfo.Message?.MessageId);
                Assert.Equal(new PlatformRoom("C123", "room"), parsed.SkillInfo.Room);
                Assert.Equal("skillname", parsed.SkillInfo.SkillName);
                Assert.Equal(new Uri("http://example.com/skill"), parsed.SkillInfo.SkillUrl);
                Assert.Equal("CODE", parsed.RunnerInfo.Code);
                Assert.Equal(CodeLanguage.Ink, parsed.RunnerInfo.Language);
                Assert.Equal(99999, parsed.RunnerInfo.Timestamp);
                Assert.Equal(Guid.Empty, parsed.RunnerInfo.AuditIdentifier);
                Assert.Equal(53, parsed.RunnerInfo.MemberId);
                Assert.Equal(99, parsed.RunnerInfo.SkillId);
                Assert.Equal(420, parsed.RunnerInfo.UserId);
                Assert.Equal("a b c", parsed.SignalInfo?.Arguments);
                Assert.Equal("signal", parsed.SignalInfo?.Name);
                Assert.Equal("d e f", parsed.SignalInfo?.Source.Arguments);
                Assert.Equal("123", parsed.ConversationInfo?.Id);
                Assert.Equal(new DateTimeOffset(2022, 01, 01, 01, 02, 03, TimeSpan.FromHours(8)), parsed.ConversationInfo?.Created);
                Assert.Equal(new DateTimeOffset(2022, 01, 01, 01, 02, 03, TimeSpan.FromHours(8)), parsed.ConversationInfo?.LastMessagePostedOn);
                Assert.Equal(
                    new PlatformUser("U888",
                        "you",
                        "You",
                        "you@ab.bot",
                        "America/Vancouver",
                        "123 Jump St.",
                        42.24,
                        24.42),
                    parsed.ConversationInfo?.StartedBy);
                Assert.Equal(new List<IChatUser>()
                    {
                        new PlatformUser("U888",
                            "you",
                            "You",
                            "you@ab.bot",
                            "America/Vancouver",
                            "123 Jump St.",
                            42.24,
                            24.42)
                    },
                    parsed.ConversationInfo?.Members);
                Assert.Equal(new PlatformRoom("C999", "midgar"), parsed.ConversationInfo?.Room);
                Assert.Equal("Convo Title", parsed.ConversationInfo?.Title);
                Assert.Equal("1111.4444", parsed.ConversationInfo?.FirstMessageId);
            });
    }

    static string Diff(string baseline, string actual)
    {
        var diff = InlineDiffBuilder.Diff(baseline, actual);
        var builder = new StringBuilder();
        foreach (var line in diff.Lines)
        {
            var prefix = line.Type switch
            {
                ChangeType.Inserted => "+ ",
                ChangeType.Deleted => "- ",
                _ => "  ",
            };

            builder.AppendLine($"{prefix}{line.Text}");
        }

        return builder.ToString();
    }

    public static IEnumerable<object[]> JsonFormats
    {
        get {
            yield return new object[] { AbbotJsonFormat.NewtonsoftJson };
        }
    }

    static async Task RunSerializeTestAsync<T>(string baselineName, AbbotJsonFormat format, T inp)
    {
        var baseline = (await ReadBaselineAsync(baselineName)).ReplaceLineEndings();

        var actual = format.Serialize(inp, true).ReplaceLineEndings();
        if (baseline != actual)
        {
            var diff = Diff(baseline, actual);
            var message = @$"JSON Serialization did not match the baseline.
Diff:
{diff}

Expected:
{baseline}

Actual:
{actual}";

            Assert.True(false, message);
        }
    }

    static async Task RunDeserializeTestAsync<T>(string baselineName, AbbotJsonFormat format, Action<T> matcher)
    {
        var baseline = await ReadBaselineAsync(baselineName);
        var deserialized = format.Deserialize<T>(baseline);
        Assert.NotNull(deserialized);
        matcher(deserialized);
    }

    static async Task RunRoundTripTestAsync<T>(string baselineName, AbbotJsonFormat format, T inp,
        Action<T>? matcher = null)
    {
        matcher ??= t => Assert.Equal(inp, t);
        await RunSerializeTestAsync(baselineName, format, inp);
        await RunDeserializeTestAsync(baselineName, format, matcher);
    }

    static async Task<string> ReadBaselineAsync(string baselineName) =>
        (await Assembly.GetExecutingAssembly().ReadResourceAsync("Serialization", baselineName)).Trim();
}
