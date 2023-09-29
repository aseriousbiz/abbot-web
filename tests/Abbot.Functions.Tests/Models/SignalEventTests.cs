using System;
using System.Collections.Generic;
using System.Net.Http;
using Serious.Abbot.Functions.Models;
using Serious.Abbot.Messages;
using Serious.Abbot.Models;
using Serious.Abbot.Scripting;
using Xunit;

public class SignalEventTests
{
    public class TheConstructor
    {
        [Fact]
        public void PopulatesSignalEventFromSignalMessageWithRootSource()
        {
            var message = new SignalMessage
            {
                Name = "ready-set-go",
                Arguments = "go go go",
                Source = new SignalSourceMessage
                {
                    SkillName = "some-skill",
                    Arguments = "the original args",
                    SkillUrl = new Uri("https://app.ab.bot/skills/some-skill"),
                    Mentions = new List<PlatformUser> { new("U987", "username", "name") },
                    IsChat = true,
                    IsPatternMatch = true,
                    Pattern = new PatternMessage
                    {
                        Name = "some-pattern",
                        PatternType = PatternType.RegularExpression,
                        Pattern = ".*"
                    }
                }
            };

            var result = new SignalEvent(message);

            Assert.Equal("ready-set-go", result.Name);
            Assert.Equal("go go go", result.Arguments);
            Assert.Equal("some-skill", result.Source.SkillName);
            Assert.Equal("the original args", result.Source.Arguments.Value);
            var mention = Assert.Single(result.Source.Mentions);
            Assert.Equal("U987", mention.Id);
            Assert.Null(result.Source.SignalEvent);
            Assert.Equal("some-skill", result.RootSource.SkillName);
            Assert.Equal("the original args", result.RootSource.Arguments.Value);
            Assert.True(result.RootSource.IsChat);
            Assert.True(result.RootSource.IsPatternMatch);
            Assert.NotNull(result.RootSource.Pattern);
            Assert.Equal("some-pattern", result.RootSource.Pattern.Name);
            Assert.Equal(".*", result.RootSource.Pattern.Pattern);
            Assert.Equal(PatternType.RegularExpression, result.RootSource.Pattern.PatternType);
        }

        [Fact]
        public void PopulatesRootSourceSignalMessage()
        {
            var message = new SignalMessage
            {
                Name = "ready-set-go",
                Arguments = "go go go",
                Source = new SignalSourceMessage
                {
                    SkillName = "some-skill",
                    Arguments = "the original args",
                    SkillUrl = new Uri("https://app.ab.bot/skills/some-skill"),
                    Mentions = new List<PlatformUser> { new("U987", "username", "name") },
                    SignalEvent = new SignalMessage
                    {
                        Name = "wolf-head",
                        Arguments = "raise a coin to your witcher",
                        Source = new SignalSourceMessage
                        {
                            SkillName = "witcher-skill",
                            Arguments = "the witcher args",
                            SkillUrl = new Uri("https://app.ab.bot/skills/witcher-skill"),
                            Mentions = new List<PlatformUser> { new("U987", "username", "name") },
                            SignalEvent = new SignalMessage
                            {
                                Name = "root-signal",
                                Arguments = "you may start",
                                Source = new SignalSourceMessage
                                {
                                    SkillName = "root-skill",
                                    Arguments = "the root args",
                                    SkillUrl = new Uri("https://app.ab.bot/skills/root-skill"),
                                    Mentions = new List<PlatformUser> { new("U986", "geralt", "geralt") },
                                    IsChat = false,
                                    IsRequest = true,
                                    Request = new HttpTriggerRequest
                                    {
                                        ContentType = "application/json",
                                        HttpMethod = "POST",
                                        RawBody = "The Root!",
                                        Url = "https://example.com/webhook",
                                        Headers = new(),
                                        Form = new(),
                                        Query = new()
                                    }
                                }
                            }
                        }
                    }
                }
            };

            var result = new SignalEvent(message);

            Assert.Equal("some-skill", result.Source.SkillName);
            Assert.Equal("root-skill", result.RootSource.SkillName);
            Assert.NotNull(result.RootSource.Request);
            Assert.Equal("application/json", result.RootSource.Request.ContentType);
            Assert.Equal("The Root!", result.RootSource.Request.RawBody);
            Assert.Equal(HttpMethod.Post, result.RootSource.Request.HttpMethod);
        }
    }
}
