using System.Collections.Generic;
using System.Text.Json;
using Serious.Abbot.Messages;
using Serious.Abbot.Models;
using Serious.Abbot.Scripting;
using Xunit;

public class SignalRequestTests
{
    public class TheType
    {
        [Fact]
        public void IsSerializableAndDeserializable()
        {
            var request = new SignalRequest
            {
                Name = "signal",
                Arguments = "some args",
                Source = new SignalSourceMessage
                {
                    Pattern = new PatternMessage
                    {
                        Pattern = ".*",
                        Description = "some description",
                        PatternType = PatternType.RegularExpression
                    },
                    Request = new HttpTriggerRequest
                    {
                        RawBody = "The Raw Body"
                    },
                    Mentions = new List<PlatformUser>
                    {
                        new("id",
                            "the-red-baron",
                            "Snoopy")
                    }
                },
                Room = new PlatformRoom("C01234", "whatever")
            };

            var serialized = JsonSerializer.Serialize(request);
            var deserialized = JsonSerializer.Deserialize<SignalRequest>(serialized);

            Assert.NotNull(deserialized);
            Assert.Equal("signal", deserialized!.Name);
            Assert.Equal("some args", deserialized.Arguments);
            var signalSource = deserialized.Source;
            Assert.NotNull(signalSource.Pattern);
            Assert.Equal(".*", signalSource.Pattern!.Pattern);
            Assert.Equal("The Raw Body", signalSource.Request!.RawBody);
            var mention = Assert.Single(signalSource.Mentions);
            Assert.Equal("Snoopy", mention.Name);
        }
    }

    public class TheContainsCycleMethod
    {
        [Fact]
        public void ReturnsFalseIfCycleIsNotDetected()
        {
            var signal = new SignalRequest
            {
                Name = "hello",
                Source = new SignalSourceMessage
                {
                    SignalEvent = new SignalMessage
                    {
                        Name = "world",
                        Source = new SignalSourceMessage
                        {
                            SignalEvent = new SignalMessage
                            {
                                Name = "hi",
                                Source = new SignalSourceMessage()
                            }
                        }
                    }
                },
                Room = new PlatformRoom("C01234", "whatever")
            };

            var result = signal.ContainsCycle();

            Assert.False(result);
            Assert.False(signal.ContainsCycle()); // We have logic to not recompute it. Want to make sure it works.
        }

        [Fact]
        public void ReturnsTrueIfCycleDetected()
        {
            var signal = new SignalRequest
            {
                Name = "foot",
                Source = new SignalSourceMessage
                {
                    SignalEvent = new SignalMessage
                    {
                        Name = "world",
                        Source = new SignalSourceMessage
                        {
                            SignalEvent = new SignalMessage
                            {
                                Name = "hi",
                                Source = new SignalSourceMessage
                                {
                                    SignalEvent = new SignalMessage
                                    {
                                        Name = "FOOT",
                                        Source = new SignalSourceMessage
                                        {
                                            SignalEvent = new SignalMessage
                                            {
                                                Name = "root",
                                                Source = new SignalSourceMessage()
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                },
                Room = new PlatformRoom("C012341234", "whatever")
            };

            var result = signal.ContainsCycle();

            Assert.True(result);
            Assert.True(signal.ContainsCycle()); // We have logic to not recompute it. Want to make sure it works.
        }
    }
}
