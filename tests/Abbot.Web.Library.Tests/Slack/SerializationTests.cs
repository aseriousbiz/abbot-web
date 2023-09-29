using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Refit;
using Serious;
using Serious.Slack;
using Serious.Slack.BlockKit;
using Serious.Slack.Manifests;
using Serious.Slack.Tests;
using Xunit;

public class SerializationTests
{
    // These tests use the Content serializer configured for ISlackApiClient via
    // Refit.
    static Func<T, Task<string>> GetSerializer<T>()
    {
        var services = new ServiceCollection();
        services.AddSlackApiClient();
        var provider = services.BuildServiceProvider(false);

        var settings = provider.GetService<SettingsFor<ISlackApiClient>>();
        Assert.NotNull(settings?.Settings);
        var serializer = settings.Settings.ContentSerializer;

        async Task<string> Serialize(T item)
        {
            Assert.NotNull(serializer);
            var httpContent = serializer.ToHttpContent(item);
            return await httpContent.ReadAsStringAsync();
        }

        return Serialize;
    }

    [Fact]
    public async Task CanSerializeMessageRequestWithBlocks()
    {
        var serialize = GetSerializer<MessageRequest>();
        var message = new MessageRequest("C0123456", "Hello world")
        {
            Blocks = new[]
            {
                new Section {
                    Text = new MrkdwnText("Hello world")
                }
            }
        };
        var jsonString = await serialize(message);

        Assert.Equal("{\"channel\":\"C0123456\",\"blocks\":[{\"text\":{\"text\":\"Hello world\",\"type\":\"mrkdwn\"},\"type\":\"section\"}],\"attachments\":[],\"text\":\"Hello world\"}", jsonString);
    }

    [Fact]
    public async Task CanSerializeMessageRequestWithActionsBlocks()
    {
        var serialize = GetSerializer<MessageRequest>();
        var message = new MessageRequest("C0123456", "Hello world")
        {
            Blocks = new ILayoutBlock[]
            {
                new Section
                {
                    Text = new MrkdwnText("Hello world")
                },
                new Actions
                {
                    BlockId = $"block_id",
                    Elements = new List<IActionElement>
                    {
                        new ButtonElement
                        {
                            Text = new PlainText("Yes"),
                            ActionId = "confirm"
                        }
                    }
                }
            }
        };
        var jsonString = await serialize(message);

        Assert.Equal("{\"channel\":\"C0123456\",\"blocks\":[{\"text\":{\"text\":\"Hello world\",\"type\":\"mrkdwn\"},\"type\":\"section\"},{\"elements\":[{\"text\":{\"emoji\":true,\"text\":\"Yes\",\"type\":\"plain_text\"},\"action_id\":\"confirm\",\"type\":\"button\"}],\"block_id\":\"block_id\",\"type\":\"actions\"}],\"attachments\":[],\"text\":\"Hello world\"}", jsonString);
    }

    [Fact]
    public async Task CanSerializeMessageRequest()
    {
        var serialize = GetSerializer<MessageRequest>();
        var message = new MessageRequest
        {
            Channel = "C0123456",
            Text = "Hello world",
            UserName = "abbot-bot",
            Blocks = new[]
            {
                new Section
                {
                    Text = new MrkdwnText("Hello world")
                }
            }
        };
        var jsonString = await serialize(message);
        Assert.Equal("{\"channel\":\"C0123456\",\"blocks\":[{\"text\":{\"text\":\"Hello world\",\"type\":\"mrkdwn\"},\"type\":\"section\"}],\"attachments\":[],\"text\":\"Hello world\",\"username\":\"abbot-bot\"}", jsonString);
    }

    [Fact]
    public async Task CanSerializeCopyOfViewUpdatePayload()
    {
        var view = new ModalView { Id = "V0123", Title = "Modal Title" };
        var payload = ViewUpdatePayload.Copy(view);
        var serializer = GetSerializer<ViewUpdatePayload>();

        var jsonString = await serializer(payload);

        Assert.Equal("{\"title\":{\"emoji\":true,\"text\":\"Modal Title\",\"type\":\"plain_text\"},\"blocks\":[],\"type\":\"modal\"}", jsonString);
    }

    [Theory]
    [InlineData(TriggerAction.OnCharacterEntered, "on_character_entered")]
    [InlineData(TriggerAction.OnEnterPressed, "on_enter_pressed")]
    public async Task CanSerializePlainTextInputElement(TriggerAction triggerAction, string expected)
    {
        var plainTextInput = new PlainTextInput
        {
            DispatchConfiguration = new DispatchConfiguration(triggerAction)
        };
        var serialize = GetSerializer<PlainTextInput>();

        var jsonString = await serialize(plainTextInput);

        Assert.Equal($"{{\"dispatch_action_config\":{{\"trigger_actions_on\":[\"{expected}\"]}},\"type\":\"plain_text_input\"}}", jsonString);
    }

    public static TheoryData<object, string> Elements => new()
    {
        {
            new Header("Header Text"),
            "{\"text\":{\"emoji\":true,\"text\":\"Header Text\",\"type\":\"plain_text\"},\"type\":\"header\"}"
        },
        {
            new PlainText("Hello world"),
            "{\"emoji\":true,\"text\":\"Hello world\",\"type\":\"plain_text\"}"
        },
        {
            new PlainText("Hello world", false),
            "{\"text\":\"Hello world\",\"type\":\"plain_text\"}"
        },
        {
            new MrkdwnText("Hello world"),
            "{\"text\":\"Hello world\",\"type\":\"mrkdwn\"}"
        },
        {
            new ImageElement("https://example.com/image.png", "An image"),
            "{\"image_url\":\"https://example.com/image.png\",\"alt_text\":\"An image\",\"type\":\"image\"}"
        },
        {
            new Section(new MrkdwnText("Hello world")),
            "{\"text\":{\"text\":\"Hello world\",\"type\":\"mrkdwn\"},\"type\":\"section\"}"
        },
        {
            new ButtonElement("Hello world")
            {
                Value = "click_me_123",
                ActionId = "action_123"
            },
            "{\"text\":{\"emoji\":true,\"text\":\"Hello world\",\"type\":\"plain_text\"},\"value\":\"click_me_123\",\"action_id\":\"action_123\",\"type\":\"button\"}"
        },
        {
            new TimePicker { InitialTime = "12:00", Value = "01:00" },
            "{\"initial_time\":\"12:00\",\"selected_time\":\"01:00\",\"type\":\"timepicker\"}"
        },
        {
            new Section
            {
                Text = new MrkdwnText("Hello world"),
                Accessory = new ImageElement
                {
                    ImageUrl = "https://example.com/image.png",
                    AltText = "An image"
                }
            },
            "{\"text\":{\"text\":\"Hello world\",\"type\":\"mrkdwn\"},\"accessory\":{\"image_url\":\"https://example.com/image.png\",\"alt_text\":\"An image\",\"type\":\"image\"},\"type\":\"section\"}"
        },
        {
            new Option("Hello world", "click_me_123"),
            "{\"text\":{\"emoji\":true,\"text\":\"Hello world\",\"type\":\"plain_text\"},\"value\":\"click_me_123\"}"
        },
        {
            new CheckOption("Hello world", "click_me_123", "Cool thing"),
            "{\"description\":{\"emoji\":true,\"text\":\"Cool thing\",\"type\":\"plain_text\"},\"text\":{\"emoji\":true,\"text\":\"Hello world\",\"type\":\"plain_text\"},\"value\":\"click_me_123\"}"
        },
        {
            new OverflowOption("Hello world", "click_me_123", new Uri("https://ab.bot")),
            "{\"url\":\"https://ab.bot\",\"text\":{\"emoji\":true,\"text\":\"Hello world\",\"type\":\"plain_text\"},\"value\":\"click_me_123\"}"
        },
        {
            new UsersMultiSelectMenu(),
            "{\"type\":\"multi_users_select\"}"
        },
        {
            new ChannelsMultiSelectMenu(),
            "{\"type\":\"multi_channels_select\"}"
        }
    };

    [Theory]
    [MemberData(nameof(Elements))]
    public async Task RoundTripBunchOfTypes(object element, string expectedJson)
    {
        var serialize = GetSerializer<object>();
        var jsonString = await serialize(element);
        Assert.Equal(expectedJson, jsonString);
        var deserialized = JsonConvert.DeserializeObject<object>(jsonString);
        Assert.NotNull(deserialized);
        var roundTripped = await serialize(deserialized);
        Assert.Equal(expectedJson, roundTripped);
    }

    public static TheoryData<Manifest> Manifests => new()
    {
        // Minimal
        new Manifest
        {
            DisplayInformation = new("Name!"),
        },
        // Minimal with Interactivity
        new Manifest
        {
            DisplayInformation = new("Name!"),
            Settings = new()
            {
                Interactivity = new(),
            }
        },
        // Everything
        new Manifest
        {
            DisplayInformation = new("Name!",
                Description: "Description!",
                LongDescription: "Long Description!",
                BackgroundColor: "#abcdef"),
            Features = new()
            {
                AppHome = new(true, true, false),
                BotUser = new("Bot!", AlwaysOnline: true),
                Shortcuts = new()
                {
                    new ManifestShortcut("Message!", ManifestShortcutType.Message, "m", "M"),
                    new ManifestShortcut("Global!", ManifestShortcutType.Global, "g", "G"),
                },
                SlashCommands = new()
                {
                    new ManifestSlashCommand("/c1", "d1", Url: "https://example.com/c1"),
                    new ManifestSlashCommand("/c1", "d1", false, "https://example.com/c2"),
                    new ManifestSlashCommand("/c2", "d2", true, "https://example.com/c3", "/tset too"),
                },
                UnfurlDomains = new()
                {
                    "example.com",
                    "example.org",
                },
                WorkflowSteps = new()
                {
                    new ManifestWorkflowStep("w1", "w:Uno"),
                    new ManifestWorkflowStep("w2", "w:Dos"),
                },
            },
            OAuthConfig = new()
            {
                RedirectUrls = new()
                {
                    "https://example.com/slack/auth",
                    "https://example.org/slack/auth",
                },
                Scopes = new()
                {
                    Bot = new() { "b1", "b2" },
                    User = new() { "u1", "u2" },
                },
            },
            Settings = new()
            {
                AllowedIpAddressRanges = new() { "ip1", "ip2" },
                EventSubscriptions = new()
                {
                    RequestUrl = "https://example.com/slack",
                    BotEvents = new() { "be1", "be2" },
                    UserEvents = new() { "ue1", "ue2" },
                },
                Interactivity = new()
                {
                    IsEnabled = true,
                    RequestUrl = "https://example.com/slack/interactivity",
                    MessageMenuOptionsUrl = "https://example.com/slack/menu",
                },
                OrgDeployEnabled = true,
                SocketModeEnabled = false,
                TokenRotationEnabled = true,
            },
        },
        // bool? sanity check
        new Manifest
        {
            DisplayInformation = new("Name!"),
            Features = new()
            {
                AppHome = new(false, null, true),
                BotUser = new("Bot!", AlwaysOnline: false),
            },
            Settings = new()
            {
                OrgDeployEnabled = null,
                SocketModeEnabled = true,
                TokenRotationEnabled = false,
            },
        },
    };

    [Theory]
    [MemberData(nameof(Manifests))]
    public async Task RoundTripManifests(Manifest manifest)
    {
        var serialize = GetSerializer<Manifest>();
        var serialized = await serialize(manifest);
        var deserialized = SlackSerializer.Deserialize<Manifest>(serialized);
        Assert.NotNull(deserialized);
        var roundTripped = await serialize(deserialized);
        Assert.Equal(serialized, roundTripped);
    }

    [Fact]
    public async Task RoundTripManifestJson()
    {
        var manifestJson = await EmbeddedResourceHelper.ReadSerializationResource("manifest.json");
        var deserialized = SlackSerializer.Deserialize<Manifest>(manifestJson);
        Assert.NotNull(deserialized);
        var indented = SlackSerializer.Serialize(deserialized, Formatting.Indented);
        Assert.Equal(Normalize(manifestJson), Normalize(indented));

        string Normalize(string json) => json.Replace("\r\n", "\n").Trim();
    }
}
