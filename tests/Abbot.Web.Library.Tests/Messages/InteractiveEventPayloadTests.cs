using System;
using System.Collections.Generic;
using Abbot.Common.TestHelpers.Fakes;
using Serious.Abbot.Events;
using Serious.Slack;
using Serious.Slack.BlockKit;
using Serious.Slack.InteractiveMessages;
using Serious.Slack.Payloads;
using Xunit;

public class InteractiveEventPayloadTests
{
    public class TheApiAppIdProperty
    {
        [Theory]
        [InlineData(null, "A1")]
        [InlineData("A2", "A2")]
        public void ReturnsMessageAppIdIfNotSet(string apiAppId, string expected)
        {
            var payload = new InteractiveMessagePayload
            {
                ApiAppId = apiAppId,
                OriginalMessage = new() { AppId = "A1" },
            };

            Assert.Equal(expected, payload.ApiAppId);
        }
    }

    public class TheFromSlackInteractiveMessagePayloadMethod
    {
        [Fact]
        public void PopulatesSkillIdFromCallbackIdFromSlackPayload()
        {
            var payload = new InteractiveMessagePayload
            {
                CallbackId = "s:42:",
                Channel = new ChannelInfo
                {
                    Id = "C01234567",
                },
                User = new UserIdentifier
                {
                    Id = "U01234567",
                },
                PayloadActions = new List<PayloadAction>
                {
                    new()
                    {
                        Name = "green",
                        Type = "button",
                        Value = "foo bar"
                    }
                },
                OriginalMessage = new()
                {
                    ThreadTimestamp = "thread.ts",
                }
            };

            var result = MessageEventInfo.FromSlackInteractiveMessagePayload(
                string.Empty,
                payload);

            Assert.NotNull(result?.InteractionInfo);
            var skillCallbackInfo = Assert.IsType<UserSkillCallbackInfo>(result.InteractionInfo.CallbackInfo);
            Assert.Equal(42, skillCallbackInfo.SkillId);
            Assert.Equal("foo bar", result.InteractionInfo.Arguments);
            Assert.Equal("C01234567", result.PlatformRoomId);
            Assert.Equal("U01234567", result.PlatformUserId);
            Assert.False(result.DirectMessage);
        }

        [Fact]
        public void PopulatesSkillIdAndSelectedChoiceWithEmptyArgumentsFromPayload()
        {
            var payload = new InteractiveMessagePayload
            {
                Channel = new ChannelInfo
                {
                    Id = "C01234567",
                },
                User = new UserIdentifier
                {
                    Id = "U01234567",
                },
                CallbackId = "s:23:",
                PayloadActions = new List<PayloadAction>
                {
                    new()
                    {
                        Name = "red",
                        Type = "button",
                        Value = string.Empty
                    }
                },
                OriginalMessage = new()
                {
                    ThreadTimestamp = "thread.ts",
                }
            };

            var result = MessageEventInfo.FromSlackInteractiveMessagePayload(
                string.Empty,
                payload);

            Assert.NotNull(result?.InteractionInfo);
            Assert.Equal(string.Empty, result.InteractionInfo.Arguments);
            var skillCallbackInfo = Assert.IsType<UserSkillCallbackInfo>(result.InteractionInfo.CallbackInfo);
            Assert.Equal(23, skillCallbackInfo.SkillId);
            Assert.Equal("C01234567", result.PlatformRoomId);
            Assert.Equal("U01234567", result.PlatformUserId);
            Assert.False(result.DirectMessage);
        }
    }

    public class TheFromSlackBlockActionsPayloadMethod
    {
        [Fact]
        public void PopulatesSkillIdFromBlockId()
        {
            var payload = new MessageBlockActionsPayload
            {
                Channel = new ChannelInfo
                {
                    Id = "C01234567",
                },
                Container = new MessageContainer("thread.ts", true, "C01234567"),
                User = new UserIdentifier
                {
                    Id = "U01234567",
                },
                Actions = new List<IPayloadElement> {
                    FakePayloadElement.Create(new ButtonElement { Value = "green" },  "s:42|customer-block-id", "foo bar")
                },
                Message = new()
                {
                    ThreadTimestamp = "thread.ts",
                },
                ResponseUrl = new Uri("https://example.com/slack/actions")
            };

            var result = MessageEventInfo.FromSlackBlockActionsPayload(
                string.Empty,
                payload);

            Assert.NotNull(result?.InteractionInfo);
            var skillCallbackInfo = Assert.IsType<UserSkillCallbackInfo>(result.InteractionInfo.CallbackInfo);
            Assert.Equal(42, skillCallbackInfo.SkillId);
            Assert.Equal("green", result.InteractionInfo.Arguments);
            Assert.NotNull(result.InteractionInfo?.ActionElement);
            var action = result.InteractionInfo.ActionElement;
            Assert.Equal("customer-block-id", action.BlockId);
            var buttonElement = Assert.IsType<ButtonElement>(action);
            Assert.Equal("green", buttonElement.Value);
            Assert.Equal("C01234567", result.PlatformRoomId);
            Assert.Equal("U01234567", result.PlatformUserId);
            Assert.False(result.DirectMessage);
            Assert.True(result.InteractionInfo.Ephemeral);
            Assert.Equal(new Uri("https://example.com/slack/actions"), result.InteractionInfo.ResponseUrl);
        }

        [Fact]
        public void PopulatesSkillIdAndSelectedChoiceWithEmptyArgumentsFromPayload()
        {
            var payload = new MessageBlockActionsPayload
            {
                Channel = new ChannelInfo
                {
                    Id = "C01234567",
                },
                Container = new MessageContainer("thread.ts", false, "C01234567"),
                User = new UserIdentifier
                {
                    Id = "U01234567",
                },
                Actions = new List<IPayloadElement>
                {
                    FakePayloadElement.Create(new ButtonElement {Value = ""}, "s:23", "red")
                },
                Message = new()
                {
                    ThreadTimestamp = "thread.ts",
                }
            };

            var result = MessageEventInfo.FromSlackBlockActionsPayload(
                string.Empty,
                payload);

            Assert.NotNull(result?.InteractionInfo);
            Assert.Equal(string.Empty, result.InteractionInfo.Arguments);
            var skillCallbackInfo = Assert.IsType<UserSkillCallbackInfo>(result.InteractionInfo.CallbackInfo);
            Assert.Equal(23, skillCallbackInfo.SkillId);
            Assert.Equal("C01234567", result.PlatformRoomId);
            Assert.Equal("U01234567", result.PlatformUserId);
            Assert.False(result.DirectMessage);
            Assert.NotNull(result.InteractionInfo?.ActionElement);
            Assert.IsType<ButtonElement>(result.InteractionInfo.ActionElement);
        }

        public static TheoryData<IPayloadElement, string> PayloadTheories => new()
        {
            { FakePayloadElement.Create(new ButtonElement { Value = "red" }, "s:21"), "red" },
            { FakePayloadElement.Create(new UserSelectMenu { SelectedValue = "U01234567" }, "s:21"), "U01234567" },
            { FakePayloadElement.Create(new ConversationSelectMenu { SelectedValue = "C01234567" }, "s:21"), "C01234567" },
            { FakePayloadElement.Create(new ChannelSelectMenu { SelectedValue = "C01234567" }, "s:21"), "C01234567" },
            { FakePayloadElement.Create(new ChannelsMultiSelectMenu { SelectedValues = new List<string> { "C01234567", "C09876454" } }, "s:21"), "C01234567 C09876454" }
        };

        [Theory]
        [MemberData(nameof(PayloadTheories))]
        public void PopulatesArgumentsFromPayload(IPayloadElement payloadElement, string expectedArguments)
        {
            var payload = new MessageBlockActionsPayload
            {
                Channel = new ChannelInfo
                {
                    Id = "C01234567",
                },
                Container = new MessageContainer("1234567.0123", false, "C01234567"),
                User = new UserIdentifier
                {
                    Id = "U01234567",
                },
                Actions = new List<IPayloadElement>
                {
                    payloadElement
                },
                Message = new()
                {
                    ThreadTimestamp = "thread.ts",
                }
            };

            var result = MessageEventInfo.FromSlackBlockActionsPayload(
                string.Empty,
                payload);

            Assert.NotNull(result?.InteractionInfo);
            Assert.Equal("1234567.0123", result.InteractionInfo.ActivityId);
            Assert.Equal(expectedArguments, result.InteractionInfo.Arguments);
            Assert.NotNull(result.InteractionInfo.ActionElement);
            Assert.Null(result.InteractionInfo.ActionElement.BlockId);
        }
    }
}
