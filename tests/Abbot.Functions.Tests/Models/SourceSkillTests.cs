using System;
using System.Collections.Generic;
using Serious.Abbot.Functions.Models;
using Serious.Abbot.Messages;
using Serious.Abbot.Models;
using Xunit;

public class SourceSkillTests
{
    public class TheConstructor
    {
        [Fact]
        public void PopulatesPropertiesWithNoSignalEvent()
        {
            var sourceSkillMessage = new SignalSourceMessage
            {
                SkillName = "some-skill",
                Arguments = "the original args",
                SkillUrl = new Uri("https://app.ab.bot/skills/some-skill"),
                Mentions = new List<PlatformUser> { new("U987", "username", "name") }
            };

            var result = new SourceSkill(sourceSkillMessage);

            Assert.Equal("some-skill", result.SkillName);
            Assert.Equal("the original args", result.Arguments.Value);
            var mention = Assert.Single(result.Mentions);
            Assert.Equal("U987", mention.Id);
            Assert.Null(result.SignalEvent);
        }

        [Fact]
        public void PopulatesSignalEvent()
        {
            var sourceSkillMessage = new SignalSourceMessage
            {
                SkillName = "some-skill",
                Arguments = "the original args",
                SkillUrl = new Uri("https://app.ab.bot/skills/some-skill"),
                Mentions = new List<PlatformUser> { new("U987", "username", "name") },
                SignalEvent = new SignalMessage
                {
                    Name = "ready-set-go",
                    Arguments = "go go go",
                    Source = new SignalSourceMessage
                    {
                        SkillName = "root-skill",
                        Arguments = "the root args",
                        SkillUrl = new Uri("https://app.ab.bot/skills/root-skill"),
                        Mentions = new List<PlatformUser> { new("U8675309", "username", "name") }
                    }
                }
            };

            var result = new SourceSkill(sourceSkillMessage);

            Assert.NotNull(result.SignalEvent);
            Assert.Equal("ready-set-go", result.SignalEvent.Name);
            Assert.Equal("go go go", result.SignalEvent.Arguments);
            Assert.Equal("root-skill", result.SignalEvent.Source.SkillName);
            Assert.Equal("the root args", result.SignalEvent.Source.Arguments.Value);
            Assert.Equal(new Uri("https://app.ab.bot/skills/root-skill"), result.SignalEvent.Source.SkillUrl);
            var mention = Assert.Single(result.SignalEvent.Source.Mentions);
            Assert.Equal("U8675309", mention.Id);
        }
    }
}
