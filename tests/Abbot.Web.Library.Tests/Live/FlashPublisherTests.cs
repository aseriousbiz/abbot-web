using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Abbot.Common.TestHelpers;
using MassTransit;
using MassTransit.SignalR.Contracts;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Serious.Abbot.Live;

namespace Abbot.Web.Library.Tests.Live;

public class FlashPublisherTests
{
    public class ThePublishAsyncMethod
    {
        [Fact]
        public async Task PublishesGroupFlashHubToMassTransit()
        {
            var env = TestEnvironmentBuilder.Create()
                .ConfigureServices(s => {
                    s.AddSingleton<IHubProtocol, JsonHubProtocol>();
                    s.AddSingleton<IHubProtocol, TestHubProtocol>();
                })
                .Build();

            // Even though there's a fake FlashPublisher registered in the container,
            // Activate will always activate the requested type!
            var flashPublisher = env.Activate<FlashPublisher>();

            await flashPublisher.PublishAsync(
                FlashName.ConversationListUpdated,
                FlashGroup.Organization(env.TestData.Organization),
                "foo", 42, new { Bar = "bar" });

            var published = await env.BusTestHarness.Published.SelectAsync<Group<FlashHub>>()
                .ToListAsync();

            var message = Assert.Single(published);
            Assert.Equal("organization:1", message.Context.Message.GroupName);
            Assert.Collection(message.Context.Message.Messages.OrderBy(p => p.Key),
                pair => {
                    Assert.Equal("json", pair.Key);
                    // Yes, this is sorta coupled to the SignalR implementation of JsonHubProtocol.
                    // But I'm not worried about that changing.
                    // Don't miss the "\x1e" suffix, that's a required part of the Hub Protocol.
                    // But the raw syntax (""") doesn't support escapes, so we tack it on manually.
                    var expectedMessage =
                        """
                        {"type":1,"target":"dispatchFlash","arguments":[{"name":"conversation-list-updated","arguments":["foo",42,{"bar":"bar"}]}]}
                        """ + "\x1e";
                    Assert.Equal(expectedMessage, Encoding.UTF8.GetString(pair.Value));
                },
                pair => {
                    // Make sure all active protocols are serialized
                    Assert.Equal("test", pair.Key);
                    Assert.Equal("Yeet"u8.ToArray(), pair.Value);
                });
        }

        [Fact]
        public async Task PublishFailureDoesNotThrow()
        {
            var env = TestEnvironmentBuilder.Create()
                .Substitute<IPublishEndpoint>(out var publishEndpoint)
                .ConfigureServices(s => {
                    s.AddSingleton<IHubProtocol, JsonHubProtocol>();
                    s.AddSingleton<IHubProtocol, TestHubProtocol>();
                })
                .Build();

            publishEndpoint.Publish<Group<FlashHub>>(Arg.Any<Group<FlashHub>>())
                .ThrowsAsync(new InvalidOperationException("Boom"));

            // Even though there's a fake FlashPublisher registered in the container,
            // Activate will always activate the requested type!
            var flashPublisher = env.Activate<FlashPublisher>();

            await flashPublisher.PublishAsync(
                FlashName.ConversationListUpdated,
                FlashGroup.Organization(env.TestData.Organization),
                "foo", 42, new { Bar = "bar" });
        }

        public class TestHubProtocol : IHubProtocol
        {
            public bool TryParseMessage(ref ReadOnlySequence<byte> input, IInvocationBinder binder, [NotNullWhen(true)] out HubMessage? message)
            {
                throw new NotImplementedException();
            }

            public void WriteMessage(HubMessage message, IBufferWriter<byte> output)
            {
                output.Write("Yeet"u8);
            }

            public ReadOnlyMemory<byte> GetMessageBytes(HubMessage message)
            {
                return "Yeet"u8.ToArray();
            }

            public bool IsVersionSupported(int version)
            {
                return true;
            }

            public string Name => "test";
            public int Version => 1;
            public TransferFormat TransferFormat => TransferFormat.Binary;
        }
    }
}
