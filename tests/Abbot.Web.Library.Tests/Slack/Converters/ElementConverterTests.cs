using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serious.Payloads;
using Serious.Slack;
using Serious.Slack.Abstractions;
using Serious.Slack.BlockKit;
using Serious.Slack.Converters;
using Serious.Slack.Events;
using Serious.Slack.InteractiveMessages;
using Serious.Slack.Payloads;
using Serious.Slack.Tests;
using Serious.TestHelpers;
using Xunit;

public class ElementConverterTests
{
    public class TheReadJsonMethod
    {
        [Theory]
        [InlineData(typeof(IElement))]
        [InlineData(typeof(Element))]
        public void CanConvertTextElement(Type targetType)
        {
            var json = JsonConvert.SerializeObject(new {
                type = "text",
                text = "Hello World"
            });

            var converter = new ElementConverter();
            var reader = new JsonTextReader(new StringReader(json));

            var result = converter.ReadJson(
                reader,
                targetType,
                null,
                false,
                new JsonSerializer());

            var element = Assert.IsType<TextElement>(result);
            Assert.Equal("text", element.Type);
            Assert.Equal("Hello World", element.Text);
            Assert.Null(element.Style);
            Assert.Empty(((IPropertyBag)element).AdditionalProperties);
        }

        [Fact]
        public void CanConvertTextElementWithStyle()
        {
            var json = JsonConvert.SerializeObject(new {
                type = "text",
                text = "Hello World",
                style = new {
                    bold = true,
                    italic = true
                }
            });

            var converter = new ElementConverter();
            var reader = new JsonTextReader(new StringReader(json));

            var result = converter.ReadJson(
                reader,
                typeof(IElement),
                null,
                false,
                new JsonSerializer());

            var element = Assert.IsType<TextElement>(result);
            Assert.Equal("text", element.Type);
            Assert.Equal("Hello World", element.Text);
            Assert.NotNull(element.Style);
            Assert.True(element.Style.Bold);
            Assert.True(element.Style.Italic);
            Assert.False(element.Style.Strike);
            Assert.False(element.Style.Code);
        }

        [Fact]
        public void CanConvertUserMention()
        {
            var json = JsonConvert.SerializeObject(new {
                type = "user",
                user_id = "U12345678"
            });

            var converter = new ElementConverter();
            var reader = new JsonTextReader(new StringReader(json));

            var result = converter.ReadJson(
                reader,
                typeof(IElement),
                null,
                false,
                new JsonSerializer());

            var element = Assert.IsType<UserMention>(result);
            Assert.Equal("user", element.Type);
            Assert.Equal("U12345678", element.UserId);
        }

        [Fact]
        public void CanConvertUserGroupMention()
        {
            var json = JsonConvert.SerializeObject(new {
                type = "usergroup",
                user_group_id = "S12345678"
            });

            var converter = new ElementConverter();
            var reader = new JsonTextReader(new StringReader(json));

            var result = converter.ReadJson(
                reader,
                typeof(IElement),
                null,
                false,
                new JsonSerializer());

            var element = Assert.IsType<UserGroupMention>(result);
            Assert.Equal("usergroup", element.Type);
            Assert.Equal("S12345678", element.UserGroupId);
        }

        [Fact]
        public void CanConvertChannelMention()
        {
            var json = JsonConvert.SerializeObject(new {
                type = "channel",
                channel_id = "C12345678"
            });

            var converter = new ElementConverter();
            var reader = new JsonTextReader(new StringReader(json));

            var result = converter.ReadJson(
                reader,
                typeof(IElement),
                null,
                false,
                new JsonSerializer());

            var element = Assert.IsType<ChannelMention>(result);
            Assert.Equal("channel", element.Type);
            Assert.Equal("C12345678", element.ChannelId);
        }

        [Theory]
        [InlineData("channel", BroadcastRangeType.Channel, null)]
        [InlineData("channel|channel", BroadcastRangeType.Channel, "channel")]
        [InlineData("channel|poop", BroadcastRangeType.Channel, "poop")]
        [InlineData("here", BroadcastRangeType.Here, null)]
        [InlineData("here|here", BroadcastRangeType.Here, "here")]
        [InlineData("here|poop", BroadcastRangeType.Here, "poop")]
        [InlineData("everyone", BroadcastRangeType.Everyone, null)]
        [InlineData("everyone|everyone", BroadcastRangeType.Everyone, "everyone")]
        [InlineData("everyone|poop", BroadcastRangeType.Everyone, "poop")]
        public void CanConvertBroadcast(string range, BroadcastRangeType expectedType, string? expectedLabel)
        {
            var json = JsonConvert.SerializeObject(new {
                type = "broadcast",
                range
            });

            var converter = new ElementConverter();
            var reader = new JsonTextReader(new StringReader(json));

            var result = converter.ReadJson(
                reader,
                typeof(IElement),
                null,
                false,
                new JsonSerializer());

            var element = Assert.IsType<Broadcast>(result);
            Assert.Equal("broadcast", element.Type);
            Assert.Equal(expectedType, element.Range.Type);
            Assert.Equal(expectedLabel, element.Range.Label);
        }

        [Fact]
        public void CanConvertLink()
        {
            var json = JsonConvert.SerializeObject(new {
                type = "link",
                url = "https://app.ab.bot/",
                text = "a styled link",
                style = new {
                    bold = true,
                    italic = true
                }
            });

            var converter = new ElementConverter();
            var reader = new JsonTextReader(new StringReader(json));

            var result = converter.ReadJson(
                reader,
                typeof(IElement),
                null,
                false,
                new JsonSerializer());

            var element = Assert.IsType<LinkElement>(result);
            Assert.Equal("link", element.Type);
            Assert.Equal("https://app.ab.bot/", element.Url);
            Assert.NotNull(element.Style);
            Assert.True(element.Style.Bold);
            Assert.True(element.Style.Italic);
        }

        [Theory]
        [InlineData(typeof(IPayloadElement))]
        [InlineData(typeof(InteractiveElement))]
        public void CanConvertPayloadElement(Type targetType)
        {
            var json = JsonConvert.SerializeObject(new {
                type = "button",
                action_id = "system:app-home:help-button",
                block_id = "qeYzV",
                action_ts = "1641926102.149005",
                text = new {
                    type = "plain_text",
                    text = ":sparkles: hello world",
                    emoji = true
                }
            });
            var converter = new ElementConverter();
            var reader = new JsonTextReader(new StringReader(json));

            var result = converter.ReadJson(
                reader,
                targetType,
                null,
                false,
                new JsonSerializer());

            var element = Assert.IsAssignableFrom<IPayloadElement>(result);
            Assert.Equal("button", element.Type);
            Assert.Equal("qeYzV", element.BlockId);
            Assert.Equal("1641926102.149005", element.ActionTimestamp);
            var button = Assert.IsType<ButtonElement>(element);
            Assert.Equal("button", button.Type);
            Assert.Equal("system:app-home:help-button", button.ActionId);
            Assert.Equal(":sparkles: hello world", button.Text.Text);
            Assert.Null(button.Style);
        }

        [Fact]
        public async Task CanDeserializeUsersSelectPayload()
        {
            var json = await EmbeddedResourceHelper.ReadSerializationResource("actions.users_select.json");
            var converter = new ElementConverter();
            var reader = new JsonTextReader(new StringReader(json));

            var result = converter.ReadJson(
                reader,
                typeof(IPayloadElement),
                null,
                false,
                new JsonSerializer());

            var menu = Assert.IsType<UserSelectMenu>(result);
            Assert.Equal("U02EMN2AYGH", menu.SelectedValue);
        }

        [Fact]
        public async Task CanDeserializeEphemeralMessageBlockActionsPayload()
        {
            var json = await EmbeddedResourceHelper.ReadSerializationResource("block_actions.ephemeral.json");
            var converter = new ElementConverter();
            var reader = new JsonTextReader(new StringReader(json));

            var result = converter.ReadJson(
                reader,
                typeof(IElement),
                null,
                false,
                new JsonSerializer());

            var payload = Assert.IsType<MessageBlockActionsPayload>(result);
            Assert.Null(payload.Message);
            var buttonElement = Assert.IsType<ButtonElement>(Assert.Single(payload.Actions));
            Assert.Equal("test --force", buttonElement.Value);
            Assert.True(payload.Container.IsEphemeral);
            Assert.Equal(new Uri("https://example.com/stae"), payload.ResponseUrl);
        }

        [Fact]
        public async Task CanDeserializeBlockActionsPayloadWithStateDictionary()
        {
            var json = await EmbeddedResourceHelper.ReadSerializationResource("message.stateful.json");
            var converter = new ElementConverter();
            var reader = new JsonTextReader(new StringReader(json));

            var result = converter.ReadJson(
                reader,
                typeof(IElement),
                null,
                false,
                new JsonSerializer());

            var payload = Assert.IsType<MessageBlockActionsPayload>(result);
            Assert.NotNull(payload.State);
            var firstElement = payload.State.Values["s:27|MRpv"]["plain_text_input-action"];
            var plainTextInputElement = Assert.IsType<PlainTextInput>(firstElement);
            Assert.Equal("Test", plainTextInputElement.Value);
            var secondElement = payload.State.Values["s:27|gBCY"]["datepicker-action"];
            var datePicker = Assert.IsType<DatePicker>(secondElement);
            Assert.Equal("1990-04-13", datePicker.Value);
            Assert.Equal(new DateOnly(1990, 04, 13), datePicker.SelectedDate);
        }

        [Fact]
        public async Task CanDeserializeMultiConversationsSelectPayload()
        {
            var json = await EmbeddedResourceHelper.ReadSerializationResource("actions.multi_conversations_select.json");
            var converter = new ElementConverter();
            var reader = new JsonTextReader(new StringReader(json));

            var result = converter.ReadJson(
                reader,
                typeof(IPayloadElement),
                null,
                false,
                new JsonSerializer());

            var menu = Assert.IsType<ConversationsMultiSelectMenu>(result);
            Assert.Collection(menu.SelectedValues,
                c0 => Assert.Equal("C01A3DGTSP9", c0),
                c1 => Assert.Equal("C024TKPSYF9", c1));
        }

        [Fact]
        public async Task CanDeserializeViewBlockActionsPayload()
        {
            var json = await EmbeddedResourceHelper.ReadSerializationResource("block_actions.view-interaction.json");
            var converter = new ElementConverter();
            var reader = new JsonTextReader(new StringReader(json));

            var result = converter.ReadJson(
                reader,
                typeof(IElement),
                null,
                false,
                new JsonSerializer());

            var payload = Assert.IsType<ViewBlockActionsPayload>(result);
            Assert.NotNull(payload.User);
            Assert.Equal("U012LKJFG0P", payload.User.Id);
            Assert.Equal("V03BSED55UZ", payload.Container.ViewId);
            Assert.Equal("b:conversations", payload.View.CallbackId);
            var action = Assert.Single(payload.Actions);
            var multiUsers = Assert.IsType<UsersMultiSelectMenu>(action);
            Assert.Collection(multiUsers.SelectedValues,
                u0 => Assert.Equal("U01316DKDT5", u0),
                u1 => Assert.Equal("U02M9M339R9", u1));
            var initialUser = Assert.Single(multiUsers.InitialUsers);
            Assert.Equal("U01316DKDT5", initialUser);
        }

        [Fact]
        public async Task CanDeserializeViewSubmissionPayload()
        {
            var json = await EmbeddedResourceHelper.ReadSerializationResource("view_submission.json");
            var converter = new ElementConverter();
            var reader = new JsonTextReader(new StringReader(json));

            var result = converter.ReadJson(
                reader,
                typeof(IElement),
                null,
                false,
                new JsonSerializer());

            var payload = Assert.IsType<ViewSubmissionPayload>(result);
            Assert.NotNull(payload.User);
            Assert.Equal("view_submission", payload.Type);
            Assert.Equal("U012LKJFG0P", payload.User.Id);
            Assert.Equal("b:conversations", payload.View.CallbackId);
            var responseUrlInfo = Assert.Single(payload.ResponseUrls);
            Assert.Equal(new ResponseInfo("Mey", "9=wa", "C01A3DGTSP9", new Uri("https://hooks.slack.com/app/T013108BYLS/3403556715862/NKxKwUZsOSgGQdBMRgOqXyh8")), responseUrlInfo);
        }

        [Fact]
        public void CanConvertUnknownElement()
        {
            var json = JsonConvert.SerializeObject(new {
                type = "mystery",
                stuff = "Dark matter",
                detail = new {
                    interesting = true
                }
            });

            var converter = new ElementConverter();
            var reader = new JsonTextReader(new StringReader(json));

            var result = converter.ReadJson(
                reader,
                typeof(IElement),
                null,
                false,
                new JsonSerializer());

            var element = Assert.IsAssignableFrom<IElement>(result);
            Assert.Equal("mystery", element.Type);
            var container = Assert.IsAssignableFrom<IPropertyBag>(result);
            Assert.Equal("Dark matter", container.AdditionalProperties["stuff"]);
            Assert.False(container.AdditionalProperties.ContainsKey("type"));
            var detail = Assert.IsType<JObject>(container.AdditionalProperties["detail"]);
            Assert.True(detail["interesting"]?.ToObject<bool>());
        }

        [Theory]
        [InlineData(typeof(LayoutBlock))]
        [InlineData(typeof(ILayoutBlock))]
        public void CanConvertUnknownLayoutBlock(Type targetType)
        {
            var json = JsonConvert.SerializeObject(new {
                type = "mysterious",
                stuff = "Dark matter",
                block_id = "12345678",
                detail = new {
                    interesting = true
                }
            });

            var converter = new ElementConverter();
            var reader = new JsonTextReader(new StringReader(json));

            var result = converter.ReadJson(
                reader,
                targetType,
                null,
                false,
                new JsonSerializer());

            var layoutBlock = Assert.IsAssignableFrom<ILayoutBlock>(result);
            Assert.Equal("12345678", layoutBlock.BlockId);
            var container = Assert.IsAssignableFrom<IPropertyBag>(layoutBlock);
            Assert.Equal("mysterious", layoutBlock.Type);
            Assert.False(container.AdditionalProperties.ContainsKey("type"));
            Assert.Equal("Dark matter", container.AdditionalProperties["stuff"]);
            var detail = Assert.IsType<JObject>(container.AdditionalProperties["detail"]);
            Assert.True(detail["interesting"]?.ToObject<bool>());
        }

        [Theory]
        [InlineData(typeof(InteractiveElement))]
        [InlineData(typeof(IPayloadElement))]
        public void CanConvertUnknownBlockElement(Type targetType)
        {
            var json = JsonConvert.SerializeObject(new {
                type = "mysterious",
                stuff = "Dark matter",
                action_id = "A012345",
                action_ts = "12342344.149005",
                block_id = "12345678",
                detail = new {
                    interesting = true
                }
            });

            var converter = new ElementConverter();
            var reader = new JsonTextReader(new StringReader(json));

            var result = converter.ReadJson(
                reader,
                targetType,
                null,
                false,
                new JsonSerializer());

            var blockElement = Assert.IsAssignableFrom<InteractiveElement>(result);
            Assert.Equal("A012345", blockElement.ActionId);
            var payloadElement = Assert.IsAssignableFrom<IPayloadElement>(result);
            Assert.Equal("12345678", payloadElement.BlockId);
            Assert.Equal("12342344.149005", payloadElement.ActionTimestamp);
            var container = Assert.IsAssignableFrom<IPropertyBag>(blockElement);
            Assert.False(container.AdditionalProperties.ContainsKey("type"));
            Assert.Equal("mysterious", blockElement.Type);
            Assert.Equal("Dark matter", container.AdditionalProperties["stuff"]);
            var detail = Assert.IsType<JObject>(container.AdditionalProperties["detail"]);
            Assert.True(detail["interesting"]?.ToObject<bool>());
        }

        [Theory]
        [InlineData(typeof(IElement))]
        [InlineData(typeof(ILayoutBlock))]
        [InlineData(typeof(LayoutBlock))]
        public void CanConvertSection(Type targetType)
        {
            var json = JsonConvert.SerializeObject(new {
                type = "section",
                text = new {
                    type = "mrkdwn",
                    text = "<!date^1392734382^Posted {date_num} {time_secs}|Posted 2014-02-18 6:39:42 AM PST>"
                }
            });

            var converter = new ElementConverter();
            var reader = new JsonTextReader(new StringReader(json));

            var result = converter.ReadJson(
                reader,
                typeof(IElement),
                null,
                false,
                new JsonSerializer());

            var section = Assert.IsType<Section>(result);
            Assert.NotNull(section.Text);
            Assert.Equal("mrkdwn", section.Text.Type);
            Assert.Equal("<!date^1392734382^Posted {date_num} {time_secs}|Posted 2014-02-18 6:39:42 AM PST>",
                section.Text.Text);
        }

        [Theory]
        [InlineData(typeof(TextObject))]
        [InlineData(typeof(IElement))]
        public void CanConvertMarkdownTextObject(Type targetType)
        {
            var json = JsonConvert.SerializeObject(new {
                type = "mrkdwn",
                text = "*hello* :sparkles:"
            });

            var converter = new ElementConverter();
            var reader = new JsonTextReader(new StringReader(json));

            var result = converter.ReadJson(
                reader,
                targetType,
                null,
                false,
                new JsonSerializer());

            var markdownTextObject = Assert.IsType<MrkdwnText>(result);
            Assert.Equal("mrkdwn", markdownTextObject.Type);
            Assert.Equal("*hello* :sparkles:", markdownTextObject.Text);
        }

        [Theory]
        [InlineData("false", false)]
        [InlineData("true", true)]
        public void CanConvertPlainTextTextObject(string emoji, bool expectedEmoji)
        {
            var json = $"{{\"type\":\"plain_text\",\"text\":\"hello :sparkles:\",\"emoji\":{emoji}}}";
            var converter = new ElementConverter();
            var reader = new JsonTextReader(new StringReader(json));

            var result = converter.ReadJson(
                reader,
                typeof(TextObject),
                null,
                false,
                new JsonSerializer());

            var plainTextObject = Assert.IsType<PlainText>(result);
            Assert.Equal("plain_text", plainTextObject.Type);
            Assert.Equal("hello :sparkles:", plainTextObject.Text);
            Assert.Equal(expectedEmoji, plainTextObject.Emoji);
        }

        [Fact]
        public void CanConvertRichTextBlock()
        {
            var json = JsonConvert.SerializeObject(new {
                type = "rich_text",
                block_id = "PqxqV",
                elements = new object[]
                {
                    new
                    {
                        type = "rich_text_section",
                        elements = new object[]
                        {
                            new
                            {
                                type = "text",
                                text = "Hello World"
                            },
                            new
                            {
                                type = "user",
                                user_id = "U12345678"
                            }
                        }
                    }
                }
            });

            var converter = new ElementConverter();
            var reader = new JsonTextReader(new StringReader(json));

            var result = converter.ReadJson(
                reader,
                typeof(IElement),
                null,
                false,
                new JsonSerializer());

            var element = Assert.IsType<RichTextBlock>(result);
            var section = Assert.Single(element.Elements);
            var richTextSection = Assert.IsType<RichTextSection>(section);
            Assert.Equal(2, richTextSection.Elements.Count);
            var text = Assert.IsType<TextElement>(richTextSection.Elements[0]);
            Assert.Equal("Hello World", text.Text);
            var user = Assert.IsType<UserMention>(richTextSection.Elements[1]);
            Assert.Equal("U12345678", user.UserId);
        }

        [Fact]
        public void CanConvertImageBlock()
        {
            var json = JsonConvert.SerializeObject(new {
                type = "image",
                image_url = "https://app.ab.bot/image.png",
                title = new {
                    type = "plain_text",
                    text = "office",
                    emoji = true
                }
            });

            var converter = new ElementConverter();
            var reader = new JsonTextReader(new StringReader(json));

            var result = converter.ReadJson(
                reader,
                typeof(LayoutBlock),
                null,
                false,
                new JsonSerializer());

            var block = Assert.IsType<Image>(result);
            Assert.Equal("image", block.Type);
            Assert.Equal(new Uri("https://app.ab.bot/image.png"), block.ImageUrl);
            Assert.NotNull(block.Title);
            Assert.Equal("plain_text", block.Title?.Type);
            Assert.Equal("office", block.Title?.Text);
        }

        [Fact]
        public async Task DeserializesInstallEvent()
        {
            var json = await EmbeddedResourceHelper.ReadSerializationResource("bot_added.json");
            var converter = new ElementConverter();
            var reader = new JsonTextReader(new StringReader(json));

            var result = converter.ReadJson(
                reader,
                typeof(IEventEnvelope<EventBody>),
                null,
                false,
                new JsonSerializer());

            var botAddedEvent = Assert.IsType<BotAddedEvent>(result);
            Assert.Equal("bot_added", botAddedEvent.Type);
            Assert.Equal("B01U63BS0EL", botAddedEvent.Bot.Id);
        }

        [Fact]
        public async Task DeserializesEventEnvelope()
        {
            var json = await EmbeddedResourceHelper.ReadSerializationResource("app_uninstalled.json");

            var converter = new ElementConverter();
            var reader = new JsonTextReader(new StringReader(json));

            var result = converter.ReadJson(
                reader,
                typeof(IEventEnvelope<EventBody>),
                null,
                false,
                new JsonSerializer());

            var envelope = Assert.IsAssignableFrom<IEventEnvelope<AppUninstalledEvent>>(result);
            Assert.Equal("event_callback", envelope.Type);
            Assert.Equal("app_uninstalled", envelope.Event.Type);
        }

        [Fact]
        public async Task DeserializesUrlVerificationPayload()
        {
            var json = await EmbeddedResourceHelper.ReadSerializationResource("url_verification.json");
            var converter = new ElementConverter();
            var reader = new JsonTextReader(new StringReader(json));

            var result = converter.ReadJson(
                reader,
                typeof(IEventEnvelope<EventBody>),
                null,
                false,
                new JsonSerializer());

            var urlVerificationPayload = Assert.IsType<UrlVerificationEvent>(result);
            Assert.Equal("url_verification", urlVerificationPayload.Type);
            Assert.Equal("TOKEN123", urlVerificationPayload.Token);
            Assert.Equal("some-challenge", urlVerificationPayload.Challenge);
        }

        [Theory]
        [InlineData(@"""type"":""""")]
        [InlineData(@"""not-type"":""stuff""")]
        public void ReturnsNullWhenJsonHasNullOrMissingTypeAttribute(string jsonBody)
        {
            var json = $@"
{{
    {jsonBody}
}}";

            var converter = new ElementConverter();
            var reader = new JsonTextReader(new StringReader(json));

            var result = converter.ReadJson(
                reader,
                typeof(IEventEnvelope<EventBody>),
                null,
                false,
                new JsonSerializer());

            Assert.Null(result);
        }

        [Fact]
        public void HandlesNullReader()
        {
            var converter = new ElementConverter();
            var reader = new NullJsonReader();

            var result = converter.ReadJson(
                reader,
                typeof(IEventEnvelope<EventBody>),
                null,
                false,
                new JsonSerializer());

            Assert.Null(result);
        }

        [Fact]
        public async Task DeserializesMessage()
        {
            var json = await EmbeddedResourceHelper.ReadSerializationResource("message.json");
            var reader = new JsonTextReader(new StringReader(json));
            var converter = new ElementConverter();

            var result = converter.ReadJson(
                reader,
                typeof(IElement),
                null,
                false,
                new JsonSerializer());

            var envelope = Assert.IsType<EventEnvelope<MessageEvent>>(result);
            Assert.Equal("T013108BYLS", envelope.TeamId);
            Assert.Equal("event_callback", envelope.Type);
            Assert.Equal("Ev00000001", envelope.EventId);
            Assert.Equal(1640027021, envelope.EventTime);
            var message = envelope.Event;
            Assert.Equal("message", message.Type);
            Assert.Equal("U012LKJFG0P", message.User);
            Assert.Equal("C01A3DGTSP9", message.Channel);
            var block = Assert.Single(message.Blocks);
            var richTextBlock = Assert.IsType<RichTextBlock>(block);
            Assert.Equal(7, richTextBlock.Elements.Count);
            Assert.IsType<RichTextSection>(richTextBlock.Elements[0]);
            Assert.IsType<RichTextList>(richTextBlock.Elements[1]);
            Assert.IsType<RichTextSection>(richTextBlock.Elements[2]);
            Assert.IsType<RichTextQuote>(richTextBlock.Elements[3]);
            Assert.IsType<RichTextSection>(richTextBlock.Elements[4]);
            Assert.IsType<RichTextPreformatted>(richTextBlock.Elements[5]);
            Assert.IsType<RichTextSection>(richTextBlock.Elements[6]);
        }

        [Fact]
        public async Task DeserializesRenameMessage()
        {
            var json = await EmbeddedResourceHelper.ReadSerializationResource("message.channel_name.json");
            var reader = new JsonTextReader(new StringReader(json));
            var converter = new ElementConverter();

            var result = converter.ReadJson(
                reader,
                typeof(IElement),
                null,
                false,
                new JsonSerializer());

            var envelope = Assert.IsType<EventEnvelope<ChannelRenameMessageEvent>>(result);
            Assert.Equal("T013108BYLS", envelope.TeamId);
            Assert.Equal("event_callback", envelope.Type);
            Assert.Equal("Ev03QWBKFV70", envelope.EventId);
            Assert.Equal(1658857979, envelope.EventTime);
            var message = envelope.Event;
            Assert.Equal("message", message.Type);
            Assert.Equal("channel_name", message.SubType);
            Assert.Equal("U012LKJFG0P", message.User);
            Assert.Equal("C03FA1UBU20", message.Channel);
            Assert.Equal("haacked-dev-room", message.OldName);
            Assert.Equal("haacked-dev-test-1", message.Name);
        }

        [Fact]
        public async Task DeserializesBotMessage()
        {
            var json = await EmbeddedResourceHelper.ReadSerializationResource("message.bot_message.json");
            var reader = new JsonTextReader(new StringReader(json));
            var converter = new ElementConverter();

            var result = converter.ReadJson(
                reader,
                typeof(IElement),
                null,
                false,
                new JsonSerializer());

            var envelope = Assert.IsType<EventEnvelope<BotMessageEvent>>(result);
            Assert.Equal("T00012121", envelope.TeamId);
            Assert.Equal("event_callback", envelope.Type);
            Assert.Equal("Ev0000000006", envelope.EventId);
            Assert.Equal(1644777118, envelope.EventTime);
            var message = envelope.Event;
            Assert.Equal("message", message.Type);
            Assert.Equal("bot_message", message.SubType);
            Assert.Null(message.User);
            Assert.Equal("Abbot-Haacked-Dev", message.UserName);
            Assert.Equal("C50000000", message.Channel);
            Assert.Empty(message.Blocks);
        }

        [Fact]
        public async Task DeserializesFileShareMessage()
        {
            var json = await EmbeddedResourceHelper.ReadSerializationResource("message.file_share.json");
            var reader = new JsonTextReader(new StringReader(json));
            var converter = new ElementConverter();

            var result = converter.ReadJson(
                reader,
                typeof(IElement),
                null,
                false,
                new JsonSerializer());

            var envelope = Assert.IsType<EventEnvelope<FileShareMessageEvent>>(result);
            Assert.Equal("T013108BYLS", envelope.TeamId);
            Assert.Equal("event_callback", envelope.Type);
            var message = envelope.Event;
            Assert.Equal("message", message.Type);
            Assert.Equal("file_share", message.SubType);
            Assert.Equal("U012LKJFG0P", message.User);
            Assert.Equal("C03FA1UBU20", message.Channel);
            Assert.NotEmpty(message.Blocks);
            var file = Assert.Single(message.Files);
            Assert.Equal("F04186WHD6X", file.Id);
        }

        [Fact]
        public async Task DeserializesBotWorkflowMessage()
        {
            var json = await EmbeddedResourceHelper.ReadSerializationResource("message.bot_message.workflow.json");
            var reader = new JsonTextReader(new StringReader(json));
            var converter = new ElementConverter();

            var result = converter.ReadJson(
                reader,
                typeof(IElement),
                null,
                false,
                new JsonSerializer());

            var envelope = Assert.IsType<EventEnvelope<BotMessageEvent>>(result);
            Assert.Equal("T00012121", envelope.TeamId);
            Assert.Equal("event_callback", envelope.Type);
            Assert.Equal("Ev0000000006", envelope.EventId);
            Assert.Equal(1658946302, envelope.EventTime);
            var message = envelope.Event;
            Assert.Equal("message", message.Type);
            Assert.Equal("bot_message", message.SubType);
            Assert.Null(message.User);
            Assert.Equal("Ask A Haack", message.UserName);
            Assert.Equal("C50000000", message.Channel);
            Assert.NotEmpty(message.Blocks);
            Assert.NotNull(message.BotProfile);
            Assert.True(message.BotProfile.IsWorkflowBot);
            Assert.Equal("Testing a more complex workflow.", message.Text);
        }

        [Fact]
        public async Task DeserializesDeletedMessage()
        {
            var json = await EmbeddedResourceHelper.ReadSerializationResource("message.message_deleted.json");
            var reader = new JsonTextReader(new StringReader(json));
            var converter = new ElementConverter();

            var result = converter.ReadJson(
                reader,
                typeof(IElement),
                null,
                false,
                new JsonSerializer());

            var envelope = Assert.IsType<EventEnvelope<MessageDeletedEvent>>(result);
            Assert.Equal("T00012121", envelope.TeamId);
            Assert.Equal("event_callback", envelope.Type);
            Assert.Equal("Ev0000000006", envelope.EventId);
            Assert.Equal(1644777118, envelope.EventTime);
            var message = envelope.Event;
            Assert.True(message.Hidden);
            Assert.Equal("message", message.Type);
            Assert.Equal("message_deleted", message.SubType);
            Assert.Null(message.User);
            Assert.Equal("C50000000", message.Channel);
            Assert.NotEmpty(message.PreviousMessage.Blocks);
            Assert.Equal(message.DeletedTimestamp, message.PreviousMessage.Timestamp);
        }

        [Fact]
        public async Task DeserializesMessageChangedEvent()
        {
            // When a message is deleted in a shared channel by a foreign member, we get
            // a message_changed event instead of message_deleted event.
            var json = await EmbeddedResourceHelper.ReadSerializationResource("message.message_changed.json");
            var reader = new JsonTextReader(new StringReader(json));
            var converter = new ElementConverter();

            var result = converter.ReadJson(
                reader,
                typeof(IElement),
                null,
                false,
                new JsonSerializer());

            var envelope = Assert.IsType<EventEnvelope<MessageChangedEvent>>(result);
            Assert.Equal("T00012121", envelope.TeamId);
            Assert.Equal("event_callback", envelope.Type);
            Assert.Equal("Ev0000000006", envelope.EventId);
            Assert.Equal(1657199914, envelope.EventTime);
            var message = envelope.Event;
            Assert.False(message.Hidden);
            Assert.Equal("message", message.Type);
            Assert.Equal("message_changed", message.SubType);
            Assert.Null(message.Message.SubType);
            Assert.Null(message.User);
            Assert.Equal("C50000000", message.Channel);
            Assert.NotEmpty(message.PreviousMessage.Blocks);
            Assert.NotEmpty(message.Message.Attachments);
        }

        [Fact]
        public async Task DeserializesMessageChangedEventForDeletedMessage()
        {
            // When a message is deleted in a shared channel by a foreign member, we get
            // a message_changed event instead of message_deleted event.
            var json = await EmbeddedResourceHelper.ReadSerializationResource("message.message_changed.deleted.json");
            var reader = new JsonTextReader(new StringReader(json));
            var converter = new ElementConverter();

            var result = converter.ReadJson(
                reader,
                typeof(IElement),
                null,
                false,
                new JsonSerializer());

            var envelope = Assert.IsType<EventEnvelope<MessageChangedEvent>>(result);
            Assert.Equal("T00012121", envelope.TeamId);
            Assert.Equal("event_callback", envelope.Type);
            Assert.Equal("Ev0000000006", envelope.EventId);
            Assert.Equal(1657163867, envelope.EventTime);
            var message = envelope.Event;
            Assert.True(message.Hidden);
            Assert.Equal("message", message.Type);
            Assert.Equal("message_changed", message.SubType);
            Assert.Equal("tombstone", message.Message.SubType);
            Assert.Null(message.User);
            Assert.Equal("C50000000", message.Channel);
            Assert.NotEmpty(message.PreviousMessage.Blocks);
        }

        [Fact]
        public async Task DeserializesMessageWithFiles()
        {
            var json = await EmbeddedResourceHelper.ReadSerializationResource("message.with-files.json");
            var reader = new JsonTextReader(new StringReader(json));
            var converter = new ElementConverter();

            var result = converter.ReadJson(
                reader,
                typeof(IElement),
                null,
                false,
                new JsonSerializer());

            var envelope = Assert.IsType<EventEnvelope<FileShareMessageEvent>>(result);
            Assert.Equal("T00012121", envelope.TeamId);
            Assert.Equal("event_callback", envelope.Type);
            Assert.Equal("Ev0000000006", envelope.EventId);
            Assert.Equal(1642804744, envelope.EventTime);
            var message = envelope.Event;
            Assert.Equal("message", message.Type);
            Assert.Equal("U50100000", message.User);
            Assert.Equal("C50000000", message.Channel);
            var file = Assert.Single(message.Files);
            Assert.Equal("F02V4RF1N91", file.Id);
        }

        [Fact]
        public async Task DeserializesMessageWithLegacyAttachmentSelectAction()
        {
            var json = await EmbeddedResourceHelper.ReadSerializationResource("message.message_changed.with-legacy-attachment-select-action.json");
            var reader = new JsonTextReader(new StringReader(json));
            var converter = new ElementConverter();

            var result = converter.ReadJson(
                reader,
                typeof(IElement),
                null,
                false,
                new JsonSerializer());

            var envelope = Assert.IsType<EventEnvelope<MessageChangedEvent>>(result);
            var attachment = Assert.Single(envelope.Event.Message.Attachments);
            Assert.Equal(new[] { "resolve_dialog", "status", "assign" },
                attachment.Actions.Select(a => a.Name).ToArray());
            Assert.Equal(new[] { "button", "button", "select" },
                attachment.Actions.Select(a => a.Type).ToArray());

            var selectedOption = Assert.Single(attachment.Actions[2].SelectedOptions);
            Assert.Equal("user:1", selectedOption.Value);
            Assert.Equal("Person 1", selectedOption.Text?.Text);
            Assert.Equal("plain_text", selectedOption.Text?.Type);
        }

        [Fact]
        public async Task DeserializesBlockSuggestionPayload()
        {
            var json = await EmbeddedResourceHelper.ReadSerializationResource("block_suggestion.json");
            var reader = new JsonTextReader(new StringReader(json));
            var converter = new ElementConverter();

            var result = converter.ReadJson(
                reader,
                typeof(IElement),
                null,
                false,
                new JsonSerializer());

            var payload = Assert.IsType<BlockSuggestionPayload>(result);
            Assert.Equal("FirstResponders_BlockId", payload.BlockId);
            Assert.Equal("FirstResponders_ActionId", payload.ActionId);
            Assert.Equal("@p", payload.Value);
        }

        [Fact]
        public async Task DeserializesInteractionMessagePayload()
        {
            var json = await EmbeddedResourceHelper.ReadSerializationResource("interactive_message.json");
            var reader = new JsonTextReader(new StringReader(json));
            var converter = new ElementConverter();

            var result = converter.ReadJson(
                reader,
                typeof(Payload),
                null,
                false,
                new JsonSerializer());

            var payload = Assert.IsType<InteractiveMessagePayload>(result);
            Assert.Equal("A01TG9GPJQ3", payload.ApiAppId);
            Assert.Equal("T013108BYLS", payload.Team.Id);
            Assert.Equal("aseriousbiz", payload.Team.Domain);
            Assert.Equal("s:5", payload.CallbackId);
            Assert.Equal("C01A3DGTSP9", payload.Channel.Id);
            Assert.Equal("haacked-playground", payload.Channel.Name);
            Assert.NotNull(payload.User);
            Assert.Equal("U012LKJFG0P", payload.User?.Id);
            Assert.Equal("haacked", payload.User?.Name);
            Assert.Equal("1641666166.411887", payload.ActionTimestamp);
            Assert.Equal("1641665238.009700", payload.MessageTimestamp);
            Assert.Equal("2", payload.AttachmentId);
            Assert.Equal("TOKEN123", payload.Token);
            Assert.False(payload.IsAppUnfurl);
            Assert.False(payload.IsEnterpriseInstall);
            var originalMessage = payload.OriginalMessage;
            Assert.NotNull(originalMessage);
            Assert.Equal("message", originalMessage.Type);
            Assert.Equal("bot_message", originalMessage.SubType);
            Assert.Equal("Please choose a color.", originalMessage.Text);
            Assert.Equal("1641665238.009700", originalMessage.Timestamp);
            Assert.NotNull(originalMessage.Icons);
            Assert.Equal("https://example.com/icon.png", originalMessage.Icons.Image48);
            Assert.Equal("A01TG9GPJQ3", originalMessage.AppId);
            var attachments = originalMessage.Attachments;
            Assert.Equal(new Uri("https://app.ab.bot/img/abbot-full-wave.png"), attachments[0].ImageUrl);
            Assert.Equal(827, attachments[0].ImageWidth);
            Assert.Equal(1000, attachments[0].ImageHeight);
            Assert.Equal(121092, attachments[0].ImageBytes);
            Assert.Equal(1, attachments[0].Id);
            Assert.Equal("660000", attachments[0].Color);
            Assert.Equal("The big choice", attachments[0].Title);
            Assert.Equal("The big choice", attachments[0].Fallback);
            Assert.Equal(2, attachments[1].Id);
            Assert.Equal("1", attachments[1].Actions[0].Id);
            Assert.Equal("2", attachments[1].Actions[1].Id);
            Assert.Equal("choices", attachments[1].Actions[0].Name);
            Assert.Equal("choices", attachments[1].Actions[1].Name);
            Assert.Equal("Red", attachments[1].Actions[0].Text);
            Assert.Equal("Green", attachments[1].Actions[1].Text);
            Assert.Equal("button", attachments[1].Actions[0].Type);
            Assert.Equal("button", attachments[1].Actions[1].Type);
            Assert.Equal("pick red", attachments[1].Actions[0].Value);
            Assert.Equal("pick green", attachments[1].Actions[1].Value);
            Assert.Equal("primary", attachments[1].Actions[0].Style);
            Assert.Equal("default", attachments[1].Actions[1].Style);
            Assert.Equal(new Uri("https://hooks.slack.com/actions/T013108BYLS/BLABLA/BLABLA"), payload.ResponseUrl);
            Assert.Equal("FANCY.TRIGGER.ID", payload.TriggerId);
        }

        [Fact]
        public async Task DeserializesButtonClickBlockInteractionsPayload()
        {
            var json = await EmbeddedResourceHelper.ReadSerializationResource("block_actions.button_click.json");
            var reader = new JsonTextReader(new StringReader(json));
            var converter = new ElementConverter();

            var result = converter.ReadJson(
                reader,
                typeof(BlockActionsPayload),
                null,
                false,
                new JsonSerializer());

            var payload = Assert.IsType<MessageBlockActionsPayload>(result);
            Assert.NotNull(payload.User);
            Assert.Equal("1647316032.343199", payload.Container.MessageTimestamp);
            Assert.Equal("C01A3DGTSP9", payload.Container.ChannelId);
            Assert.Equal("T013108BYLS", payload.User.TeamId);
        }

        [Fact]
        public async Task DeserializesAppHomeBlockInteractionsPayload()
        {
            var json = await EmbeddedResourceHelper.ReadSerializationResource("block_actions.app_home_click.json");
            var reader = new JsonTextReader(new StringReader(json));
            var converter = new ElementConverter();

            var result = converter.ReadJson(
                reader,
                typeof(BlockActionsPayload),
                null,
                false,
                new JsonSerializer());

            var payload = Assert.IsType<ViewBlockActionsPayload>(result);
            Assert.Equal("block_actions", payload.Type);
            Assert.NotNull(payload.User);
            Assert.Equal("U012LKJFG0P", payload.User.Id);
            Assert.Equal("haacked", payload.User.UserName);
            Assert.Equal("haacked", payload.User.Name);
            Assert.Equal("T013108BYLS", payload.User.TeamId);
            Assert.Equal("A01TG9GPJQ3", payload.ApiAppId);
            Assert.Equal("TOKEN123", payload.Token);
            Assert.NotNull(payload.Container);
            Assert.Equal("V02TGKYF0ER", payload.Container.ViewId);
            Assert.Equal("Some.Trigger.Id", payload.TriggerId);
            Assert.Equal("T013108BYLS", payload.Team.Id);
            Assert.Equal("aseriousbiz", payload.Team.Domain);
            Assert.False(payload.IsEnterpriseInstall);
            var view = Assert.IsType<AppHomeView>(payload.View);
            Assert.Equal("V02TGKYF0ER", view.Id);
            Assert.Equal("T013108BYLS", view.TeamId);
            Assert.Equal("home", view.Type);
            // TODO: Assert the blocks
            Assert.Equal("this is private", view.PrivateMetadata);
            Assert.Equal("some-callback-id", view.CallbackId);
            Assert.Equal("1641926048.oGdZivh0", view.Hash);
            Assert.NotNull(view.Title);
            Assert.Equal("plain_text", view.Title.Type);
            Assert.Equal("View Title", view.Title.Text);
            var plainTextTitle = Assert.IsType<PlainText>(view.Title);
            Assert.True(plainTextTitle.Emoji);
            Assert.False(view.ClearOnClose);
            Assert.False(view.NotifyOnClose);
            Assert.Equal("V02TGKYF0ER", view.RootViewId);
            Assert.Equal("A01TG9GPJQ3", view.AppId);
            Assert.Equal("Some.External.Id", view.ExternalId);
            Assert.Equal("B01U63BS0EL", view.BotId);
            var action = Assert.Single(payload.Actions);
            var buttonPayload = Assert.IsAssignableFrom<IPayloadElement>(action);
            Assert.Equal("1641926102.149005", buttonPayload.ActionTimestamp);
            Assert.Equal("qeYzV", buttonPayload.BlockId);
            var sourceButton = Assert.IsType<ButtonElement>(buttonPayload);
            Assert.Equal("system:app-home:help-button", sourceButton.ActionId);
            Assert.Equal("button", sourceButton.Type);
            Assert.Equal(":question: Help", sourceButton.Text.Text);
            //TODO: Uncomment this when the rest is implemented.
            var button = Assert.IsType<ButtonElement>(action);
            Assert.Equal("plain_text", button.Text.Type);
            Assert.Equal(":question: Help", button.Text.Text);
            Assert.True(button.Text.Emoji);
        }

        [Fact]
        public async Task DeserializesViewClosedPayload()
        {
            var json = await EmbeddedResourceHelper.ReadSerializationResource("view_closed.json");
            var reader = new JsonTextReader(new StringReader(json));
            var converter = new ElementConverter();

            var result = converter.ReadJson(
                reader,
                typeof(BlockActionsPayload),
                null,
                false,
                new JsonSerializer());

            var payload = Assert.IsType<ViewClosedPayload>(result);
            Assert.Equal("T013108BYLS", payload.Team.Id);
            Assert.Equal("aseriousbiz", payload.Team.Domain);
            Assert.NotNull(payload.User);
            Assert.Equal("U012LKJFG0P", payload.User.Id);
            Assert.Equal("haacked", payload.User.Name);
            Assert.Equal("A01TG9GPJQ3", payload.ApiAppId);
            Assert.Equal("b:conversations", payload.View.CallbackId);
            Assert.True(payload.IsCleared);
        }

        [Fact]
        public async Task DeserializesMessageActionPayload()
        {
            var json = await EmbeddedResourceHelper.ReadSerializationResource("message_action.json");
            var reader = new JsonTextReader(new StringReader(json));
            var converter = new ElementConverter();

            var result = converter.ReadJson(
                reader,
                typeof(IPayload),
                null,
                false,
                new JsonSerializer());

            var payload = Assert.IsType<MessageActionPayload>(result);
            Assert.Equal("1654805018.330793", payload.ActionTimestamp);
            Assert.Equal("T013108BYLS", payload.Team.Id);
            Assert.Equal("aseriousbiz", payload.Team.Domain);
            Assert.Equal("U02M9M339R9", payload.User?.Id);
            Assert.Equal("C02MEN6N1EW", payload.Channel.Id);
            Assert.Equal("g:conversation.triage", payload.CallbackId);
            Assert.Equal("3647630125538.1103008406706.ac12e3c73940e4152113924386131cd8", payload.TriggerId);
            Assert.Equal("https://hooks.slack.com/app/T013108BYLS/3660284080129/lk8wiZBVgeTXWi2Hd3ljrBKG", payload.ResponseUrl.ToString());
            Assert.Equal("1654117846.289599", payload.MessageTimestamp);
            Assert.Equal("1654117846.289599", payload.Message.Timestamp);
            Assert.Equal("some text", payload.Message.Text);
            Assert.Equal("T013108BYLS", payload.Message.TeamId);
            Assert.Collection(payload.Message.Blocks,
                b => {
                    var rtb = Assert.IsType<RichTextBlock>(b);
                    Assert.Equal("rich_text", rtb.Type);
                    Assert.Equal("WuI3S", rtb.BlockId);
                    Assert.Collection(rtb.Elements,
                        e => {
                            var rte = Assert.IsType<RichTextSection>(e);
                            Assert.Collection(rte.Elements,
                                e2 => {
                                    var txt = Assert.IsType<TextElement>(e2);
                                    Assert.Equal("some text", txt.Text);
                                });
                        });
                });
        }

        [Theory]
        [InlineData("message")]
        [InlineData("app_mention")]
        public async Task DeserializesChatMessages(string eventType)
        {
            var json = await EmbeddedResourceHelper.ReadSerializationResource($"{eventType}.json");
            var converter = new ElementConverter();
            var reader = new JsonTextReader(new StringReader(json));

            var result = converter.ReadJson(
                reader,
                typeof(IEventEnvelope<EventBody>),
                null,
                false,
                new JsonSerializer());

            var envelope = Assert.IsAssignableFrom<IEventEnvelope<MessageEvent>>(result);
            Assert.Equal("event_callback", envelope.Type);
            var message = envelope.Event;
            Assert.Equal(eventType, message.Type);
            Assert.Equal(".ping --debug", message.Text);
            Assert.Equal("T013108BYLS", message.Team);
        }

        [Fact]
        public async Task DeserializesAppHomeOpenedEvent()
        {
            var json = await EmbeddedResourceHelper.ReadSerializationResource("app_home_opened.json");
            var converter = new ElementConverter();
            var reader = new JsonTextReader(new StringReader(json));

            var result = converter.ReadJson(
                reader,
                typeof(IEventEnvelope<>),
                null,
                false,
                new JsonSerializer());

            var envelope = Assert.IsType<EventEnvelope<AppHomeOpenedEvent>>(result);
            Assert.Equal("event_callback", envelope.Type);
            Assert.Equal("app_home_opened", envelope.Event.Type);
            Assert.Equal("app_home_opened", envelope.Event.Type);
            Assert.Equal("home", envelope.Event.Tab);
            Assert.Equal("D0LAN2Q65", envelope.Event.Channel);
        }

        [Fact]
        public async Task DeserializesUninstallEvent()
        {
            var json = await EmbeddedResourceHelper.ReadSerializationResource("app_uninstalled.json");
            var converter = new ElementConverter();
            var reader = new JsonTextReader(new StringReader(json));

            var result = converter.ReadJson(
                reader,
                typeof(IEventEnvelope<EventBody>),
                null,
                false,
                new JsonSerializer());

            var envelope = Assert.IsAssignableFrom<IEventEnvelope<EventBody>>(result);
            Assert.Equal("event_callback", envelope.Type);
            Assert.Equal("app_uninstalled", envelope.Event.Type);
        }

        [Fact]
        public async Task DeserializesTeamRenameEvent()
        {
            var json = await EmbeddedResourceHelper.ReadSerializationResource("team_rename.json");
            var converter = new ElementConverter();
            var reader = new JsonTextReader(new StringReader(json));

            var result = converter.ReadJson(
                reader,
                typeof(IEventEnvelope<EventBody>),
                null,
                false,
                new JsonSerializer());

            var envelope = Assert.IsType<EventEnvelope<TeamRenameEvent>>(result);
            Assert.Equal("event_callback", envelope.Type);
            Assert.Null(envelope.EnterpriseId);
            Assert.Equal("team_rename", envelope.Event.Type);
            Assert.Equal("A Serious Business", envelope.Event.Name);
            // As far as we know, we never receive a team_rename event for another organization.
            // But the event has a team_id in the event payload, so just in case, we make sure we handle it.
            Assert.Equal("T013108BYLS", envelope.TeamId);
            Assert.Equal("T000000001", envelope.Event.TeamId);
        }

        [Fact]
        public async Task DeserializesTeamInEnterpriseRenameEvent()
        {
            var json = await EmbeddedResourceHelper.ReadSerializationResource("team_rename.enterprise.json");
            var converter = new ElementConverter();
            var reader = new JsonTextReader(new StringReader(json));

            var result = converter.ReadJson(
                reader,
                typeof(IEventEnvelope<EventBody>),
                null,
                false,
                new JsonSerializer());

            var envelope = Assert.IsType<EventEnvelope<TeamRenameEvent>>(result);
            Assert.Equal("event_callback", envelope.Type);
            Assert.Equal("E02V6NXUF2Q", envelope.EnterpriseId);
            Assert.Equal("team_rename", envelope.Event.Type);
            Assert.Equal("Serious Sandbox 3", envelope.Event.Name);
            // As far as we know, we never receive a team_rename event for another organization.
            // But the event has a team_id in the event payload, so just in case, we make sure we handle it.
            Assert.Equal("T013108BYLS", envelope.TeamId);
            Assert.Equal("T046W0NCR4P", envelope.Event.TeamId);
        }

        [Fact]
        public async Task DeserializesTeamDomainChangeEventForEnterpriseGridOrganization()
        {
            var json = await EmbeddedResourceHelper.ReadSerializationResource("team_domain_change.enterprise.json");
            var converter = new ElementConverter();
            var reader = new JsonTextReader(new StringReader(json));

            var result = converter.ReadJson(
                reader,
                typeof(IEventEnvelope<EventBody>),
                null,
                false,
                new JsonSerializer());

            var envelope = Assert.IsType<EventEnvelope<TeamDomainChangeEvent>>(result);
            Assert.Equal("event_callback", envelope.Type);
            Assert.Equal("E02V6NXUF2Q", envelope.EnterpriseId);
            Assert.Equal("team_domain_change", envelope.Event.Type);
            Assert.Equal("aserioussandbox1", envelope.Event.Domain);
            // As far as we know, we never receive a team_rename event for another organization.
            // But the event has a team_id in the event payload, so just in case, we make sure we handle it.
            Assert.Equal("T013108BYLS", envelope.TeamId);
            Assert.Equal("T030E9MAV6U", envelope.Event.TeamId);
        }

        [Fact]
        public async Task DeserializesUserChangeOrTeamJoinEvent()
        {
            var json = await EmbeddedResourceHelper.ReadSerializationResource($"user_change.json");

            var converter = new ElementConverter();
            var reader = new JsonTextReader(new StringReader(json));

            var result = converter.ReadJson(
                reader,
                typeof(IEventEnvelope<EventBody>),
                null,
                false,
                new JsonSerializer());

            var envelope = Assert.IsType<EventEnvelope<UserChangeEvent>>(result);
            Assert.Equal(envelope.Type, "event_callback");
            var userChangeEvent = envelope.Event;
            Assert.Equal("user_change", userChangeEvent.Type);
            Assert.Equal(12345678, userChangeEvent.CacheTimestamp);
            Assert.Equal("1641492387.003100", userChangeEvent.EventTimestamp);
            var user = userChangeEvent.User;
            Assert.Equal("John Doe", user.Profile.DisplayName);
            Assert.Equal("https://example.com/image-24.jpg", user.Profile.Image24);
            Assert.False(user.Deleted);
        }

        [Fact]
        public async Task DeserializesUserChangeEventForGuestUser()
        {
            var json = await EmbeddedResourceHelper.ReadSerializationResource($"user_change.guest.json");

            var converter = new ElementConverter();
            var reader = new JsonTextReader(new StringReader(json));

            var result = converter.ReadJson(
                reader,
                typeof(IEventEnvelope<EventBody>),
                null,
                false,
                new JsonSerializer());

            var envelope = Assert.IsType<EventEnvelope<UserChangeEvent>>(result);
            Assert.Equal(envelope.Type, "event_callback");
            var userChangeEvent = envelope.Event;
            Assert.Equal("user_change", userChangeEvent.Type);
            Assert.True(userChangeEvent.User.IsRestricted);
        }

        [Fact]
        public async Task DeserializesTeamJoinEvent()
        {
            var json = await EmbeddedResourceHelper.ReadSerializationResource($"team_join.json");

            var converter = new ElementConverter();
            var reader = new JsonTextReader(new StringReader(json));

            var result = converter.ReadJson(
                reader,
                typeof(IEventEnvelope<EventBody>),
                null,
                false,
                new JsonSerializer());

            var envelope = Assert.IsType<EventEnvelope<TeamJoinEvent>>(result);
            Assert.Equal(envelope.Type, "event_callback");
            var userChangeEvent = envelope.Event;
            Assert.Equal("team_join", userChangeEvent.Type);
            Assert.Equal(12345678, userChangeEvent.CacheTimestamp);
            Assert.Equal("1641492387.003100", userChangeEvent.EventTimestamp);
            var user = userChangeEvent.User;
            Assert.Equal("John Doe", user.Profile.DisplayName);
            Assert.Equal("https://example.com/image-24.jpg", user.Profile.Image24);
            Assert.False(user.Deleted);
        }

        [Fact]
        public async Task DeserializesTokensRevokedEvent()
        {
            var json = await EmbeddedResourceHelper.ReadSerializationResource($"tokens_revoked.json");

            var converter = new ElementConverter();
            var reader = new JsonTextReader(new StringReader(json));

            var result = converter.ReadJson(
                reader,
                typeof(IEventEnvelope<EventBody>),
                null,
                false,
                new JsonSerializer());

            var envelope = Assert.IsAssignableFrom<IEventEnvelope<TokensRevokedEvent>>(result);
            Assert.Equal(envelope.Type, "event_callback");
            var tokenEvent = envelope.Event;
            Assert.Equal("tokens_revoked", tokenEvent.Type);
            Assert.Empty(tokenEvent.Tokens.OAuth);
            Assert.Equal("U01TG976JSW", Assert.Single(tokenEvent.Tokens.Bot));
        }

        [Fact]
        public async Task DeserializesUserChangeWithUserFromAnotherOrg()
        {
            var json = await EmbeddedResourceHelper.ReadSerializationResource(
                "user_change.shared_channel_enterprise_user.json");

            var converter = new ElementConverter();
            var reader = new JsonTextReader(new StringReader(json));

            var result = converter.ReadJson(
                reader,
                typeof(IEventEnvelope<EventBody>),
                null,
                false,
                new JsonSerializer());

            var envelope = Assert.IsType<EventEnvelope<UserChangeEvent>>(result);
            Assert.Equal(envelope.Type, "event_callback");
            var userChangeEvent = envelope.Event;
            Assert.Equal("T0123456", envelope.TeamId);
            Assert.Equal("E0123456", envelope.EnterpriseId);
            Assert.Equal("user_change", userChangeEvent.Type);
            var user = userChangeEvent.User;
            Assert.Null(userChangeEvent.User.TeamId);
            Assert.Equal("USER12324", user.Id);
            Assert.Equal("E0123456", user.EnterpriseId);
            Assert.Equal("E0123456", user.Profile.Team);
            var enterpriseUser = user.EnterpriseUser;
            Assert.NotNull(enterpriseUser);
            Assert.Equal("USER12324", enterpriseUser.Id);
            Assert.Equal("E0123456", enterpriseUser.EnterpriseId);
            Assert.Equal("Big Business", enterpriseUser.EnterpriseName);
            Assert.True(enterpriseUser.IsAdmin);
            Assert.False(enterpriseUser.IsOwner);
            var team = Assert.Single(enterpriseUser.Teams);
            Assert.Equal("T8675309", team);
        }

        [Fact]
        public async Task DeserializesReactionAddedEvent()
        {
            var json = await EmbeddedResourceHelper.ReadSerializationResource("reaction_added.json");
            var converter = new ElementConverter();
            var reader = new JsonTextReader(new StringReader(json));

            var result = converter.ReadJson(
                reader,
                typeof(IEventEnvelope<EventBody>),
                null,
                false,
                new JsonSerializer());

            var envelope = Assert.IsType<EventEnvelope<ReactionAddedEvent>>(result);
            Assert.Equal("event_callback", envelope.Type);
            Assert.Equal("reaction_added", envelope.Event.Type);
            Assert.Equal("eyes", envelope.Event.Reaction);
            Assert.Equal("T00012121", envelope.TeamId);
            Assert.Equal("message", envelope.Event.Item.Type);
            Assert.Equal("1644779732.750289", envelope.Event.Item.Timestamp);
            Assert.Equal("C50000000", envelope.Event.Item.Channel);
        }

        [Fact]
        public async Task DeserializesReactionRemovedEvent()
        {
            var json = await EmbeddedResourceHelper.ReadSerializationResource("reaction_removed.json");
            var converter = new ElementConverter();
            var reader = new JsonTextReader(new StringReader(json));

            var result = converter.ReadJson(
                reader,
                typeof(IEventEnvelope<EventBody>),
                null,
                false,
                new JsonSerializer());

            var envelope = Assert.IsType<EventEnvelope<ReactionRemovedEvent>>(result);
            Assert.Equal("event_callback", envelope.Type);
            Assert.Equal("reaction_removed", envelope.Event.Type);
            Assert.Equal("eyes", envelope.Event.Reaction);
            Assert.Equal("T00012121", envelope.TeamId);
        }

        [Fact]
        public async Task DeserializesEventEnvelopeUnknownEvent()
        {
            var json = await EmbeddedResourceHelper.ReadSerializationResource("unknown_event.json");

            var converter = new ElementConverter();
            var reader = new JsonTextReader(new StringReader(json));

            var result = converter.ReadJson(
                reader,
                typeof(IEventEnvelope<>),
                null,
                false,
                new JsonSerializer());

            var envelope = Assert.IsAssignableFrom<IEventEnvelope<EventBody>>(result);
            Assert.Equal("event_callback", envelope.Type);
            Assert.Equal("unknown_event", envelope.Event.Type);
            var container = Assert.IsAssignableFrom<IPropertyBag>(envelope);
            Assert.True(container.AdditionalProperties.ContainsKey("view"));
        }

        [Fact]
        public void DeserializesUnknownEventBody()
        {
            var json = JsonConvert.SerializeObject(new {
                type = "event_callback",
                @event = new {
                    type = "not-message",
                    text = "<@U01234567> here is <@U98765432>"
                },
                view = new {
                    stuff = "junk"
                }
            });

            var converter = new ElementConverter();
            var reader = new JsonTextReader(new StringReader(json));

            var result = converter.ReadJson(
                reader,
                typeof(IEventEnvelope<EventBody>),
                null,
                false,
                new JsonSerializer());

            var envelope = Assert.IsAssignableFrom<IEventEnvelope<EventBody>>(result);
            Assert.Equal("event_callback", envelope.Type);
            Assert.Equal("not-message", envelope.Event.Type);
            var container = Assert.IsAssignableFrom<IPropertyBag>(envelope);
            Assert.True(container.AdditionalProperties.ContainsKey("view"));
            var eventContainer = Assert.IsAssignableFrom<IPropertyBag>(envelope.Event);
            Assert.Equal("<@U01234567> here is <@U98765432>", eventContainer.AdditionalProperties["text"]);
        }

        [Fact]
        public async Task DeserializesChannelRename()
        {
            var json = await EmbeddedResourceHelper.ReadSerializationResource("channel_rename.json");
            var converter = new ElementConverter();
            var reader = new JsonTextReader(new StringReader(json));

            var result = converter.ReadJson(
                reader,
                typeof(IEventEnvelope<EventBody>),
                null,
                false,
                new JsonSerializer());

            var envelope = Assert.IsType<EventEnvelope<ChannelRenameEvent>>(result);
            Assert.Equal("event_callback", envelope.Type);
            Assert.Equal("channel_rename", envelope.Event.Type);
            Assert.Equal("anurse-playground-2", envelope.Event.Channel.Name);
        }

        [Fact]
        public async Task DeserializesMemberJoinedChannelEvent()
        {
            var json = await EmbeddedResourceHelper.ReadSerializationResource("member_joined_channel.json");
            var converter = new ElementConverter();
            var reader = new JsonTextReader(new StringReader(json));

            var result = converter.ReadJson(
                reader,
                typeof(IEventEnvelope<EventBody>),
                null,
                false,
                new JsonSerializer());

            var envelope = Assert.IsType<EventEnvelope<MemberJoinedChannelEvent>>(result);
            Assert.Equal("event_callback", envelope.Type);
            Assert.Equal("member_joined_channel", envelope.Event.Type);
            Assert.Equal("U02NU2HM11B", envelope.Event.User);
            Assert.Equal("C0343M2HF5J", envelope.Event.Channel);
            Assert.Equal("C", envelope.Event.ChannelType);
            Assert.Equal("T01CT0CT415", envelope.Event.Team);
            Assert.Equal("1650320062.001700", envelope.Event.EventTimestamp);
            Assert.Equal("Ev03BURGL30D", envelope.EventId);
        }

        [Fact]
        public async Task DeserializesMemberLeftChannelEvent()
        {
            var json = await EmbeddedResourceHelper.ReadSerializationResource("member_left_channel.json");
            var converter = new ElementConverter();
            var reader = new JsonTextReader(new StringReader(json));

            var result = converter.ReadJson(
                reader,
                typeof(IEventEnvelope<EventBody>),
                null,
                false,
                new JsonSerializer());

            var envelope = Assert.IsType<EventEnvelope<MemberLeftChannelEvent>>(result);
            Assert.Equal("event_callback", envelope.Type);
            Assert.Equal("member_left_channel", envelope.Event.Type);
            Assert.Equal("U02NU2HM11B", envelope.Event.User);
            Assert.Equal("C0343M2HF5J", envelope.Event.Channel);
            Assert.Equal("C", envelope.Event.ChannelType);
            Assert.Equal("T01CT0CT415", envelope.Event.Team);
            Assert.Equal("1650320096.001800", envelope.Event.EventTimestamp);
            Assert.Equal("Ev03CMEPBA1W", envelope.EventId);
        }

        [Theory]
        [InlineData("channel_left", typeof(ChannelLeftEvent))]
        [InlineData("channel_deleted", typeof(ChannelDeletedEvent))]
        [InlineData("channel_archive", typeof(ChannelArchiveEvent))]
        [InlineData("channel_unarchive", typeof(ChannelUnarchiveEvent))]
        [InlineData("group_left", typeof(GroupLeftEvent))]
        [InlineData("group_deleted", typeof(GroupDeletedEvent))]
        [InlineData("group_archive", typeof(GroupArchiveEvent))]
        [InlineData("group_unarchive", typeof(GroupUnarchiveEvent))]
        public async Task DeserializesChannelLifecycleEvent(string type, Type expectedEventType)
        {
            // Why not make RunChannelLifecycleEventTest a local function you say?
            // Well, local functions don't have stable names, so they're very hard to find in reflection.
            // Technically the name is deterministic because it's derived from the declaration order in the target function,
            // but that just seems unnecessarily complex.
            // See: https://sharplab.io/#v2:CYLg1APgAgTAjAWAFBQMwAJboMIBsCGAzoegN7LqWYZQAs6A8gK4AuApgE4AUAlGRVUF10AGQD2AY3y5e/JIMEBfAQsz0GLABadxUmX3LzV6ZUdW7pvANwqTyRUA
            var testMethod = this.GetType().GetMethod(nameof(RunChannelLifecycleEventTest), BindingFlags.NonPublic | BindingFlags.Instance);
            var specialized = testMethod!.MakeGenericMethod(expectedEventType);
            var task = (Task)(specialized.Invoke(this, new object[] { type })!);
            await task;
        }

        async Task RunChannelLifecycleEventTest<T>(string type) where T : ChannelLifecycleEvent
        {
            var str = await EmbeddedResourceHelper.ReadSerializationResource("channel_lifecycle_event.json");
            var json = JObject.Parse(str);
            json["event"]!["type"] = type;
            str = json.ToString(Formatting.Indented);

            var converter = new ElementConverter();
            var reader = new JsonTextReader(new StringReader(str));

            var result = converter.ReadJson(
                reader,
                typeof(IEventEnvelope<EventBody>),
                null,
                false,
                new JsonSerializer());
            var envelope = Assert.IsAssignableFrom<EventEnvelope<T>>(result);
            Assert.Equal("event_callback", envelope.Type);
            Assert.Equal(type, envelope.Event.Type);
            Assert.Equal("U02NU2HM11B", envelope.Event.User);
            Assert.Equal("C0343M2HF5J", envelope.Event.Channel);
        }
    }

    public class TheCanWriteProperty
    {
        [Fact]
        public void ReturnsFalse()
        {
            var converter = new ElementConverter();

            Assert.False(converter.CanWrite);
        }
    }

    public class TheCanReadProperty
    {
        [Fact]
        public void ReturnsTrue()
        {
            var converter = new ElementConverter();

            Assert.True(converter.CanRead);
        }
    }
}
