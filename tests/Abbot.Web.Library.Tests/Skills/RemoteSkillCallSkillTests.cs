using Abbot.Common.TestHelpers;
using Microsoft.Bot.Schema;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenAI_API.Models;
using Serious.Abbot.AI;
using Serious.Abbot.AI.Templating;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Exceptions;
using Serious.Abbot.Messages;
using Serious.Abbot.Skills;
using Serious.Abbot.Telemetry;
using Serious.Slack.InteractiveMessages;
using Serious.Slack.Payloads;
using Serious.TestHelpers;
using Activity = System.Diagnostics.Activity;

public class RemoteSkillCallSkillTests
{
    public class WithRemoteSkillNameAndArgs
    {
        [Theory]
        [InlineData("some-room", "Ran command `pug bomb` in `#some-room` (`C00000000`).")]
        [InlineData(null, "Ran command `pug bomb` in _a channel with an unknown name_ (`C00000000`).")]
        public async Task CallsRemoteSkillWithProperLogging(string room, string expected)
        {
            var builder = TestEnvironmentBuilder.Create();
            builder.Services.AddScoped<ISkillRunnerClient>(services =>
                FakeSkillRunnerClient.CreateRealSkillRunnerClient(
                    services.GetRequiredService<ISkillAuditLog>(),
                    new SkillRunResponse
                    {
                        Success = true,
                        Errors = new List<RuntimeError>(),
                        Content = null,
                        ContentType = null,
                        Replies = new List<string>
                        {
                            "Got your message loud and clear!"
                        }
                    },
                    "application/vnd.abbot.v1+json"));

            var env = builder.Build();
            await env.CreateSkillAsync("pug");
            var messageContext = env.CreateFakeMessageContext(
                "remoteskillcall",
                "pug bomb",
                room: new Room
                {
                    Name = room,
                    PlatformRoomId = "C00000000"
                });

            var skill = env.Activate<RemoteSkillCallSkill>();

            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            Assert.Equal("Got your message loud and clear!", messageContext.SentMessages.Single());
            var auditEvent = await env.Db.AuditEvents.OfType<SkillRunAuditEvent>().LastAsync();
            Assert.Equal("bomb", auditEvent.Arguments);
            Assert.Equal(expected, auditEvent.Description);
        }

        [Fact]
        public async Task InvokesSkillWithCorrectCallerAndMessageAuthor()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            await env.CreateSkillAsync("pug");
            var messageContext = env.CreateFakeMessageContext(
                "remoteskillcall",
                "pug bomb",
                room: room);
            var skill = env.Activate<RemoteSkillCallSkill>();

            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            var invocation = Assert.Single(env.SkillRunnerClient.Invocations);
            Assert.Equal(env.TestData.User.Id, invocation.Caller.Id);
            Assert.Equal(env.TestData.Member.Id, invocation.TriggeringMessageAuthor?.Id);
            Assert.Equal(messageContext.MessageUrl, invocation.MessageUrl);
            Assert.NotNull(invocation.MessageUrl);
        }

        [Fact]
        public async Task InvokesSkillWithCorrectCallerAndMessageAuthorWhenButtonClicked()
        {
            var env = TestEnvironment.Create();
            var room = await env.CreateRoomAsync();
            await env.CreateSkillAsync("pug");
            var messageInteractionInfo = new MessageInteractionInfo(
                new MessageBlockActionsPayload
                {
                    Container = new MessageContainer("12345666.000001", false, "C00123240"),
                    Message = new SlackMessage("Some message")
                    {
                        User = env.TestData.User.PlatformUserId,
                    }
                },
                "click",
                new UserSkillCallbackInfo(default));
            var messageContext = env.CreateFakeMessageContext(
                "remoteskillcall",
                "pug bomb",
                room: room,
                from: env.TestData.ForeignMember,
                messageInteractionInfo: messageInteractionInfo);
            var skill = env.Activate<RemoteSkillCallSkill>();

            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            var invocation = Assert.Single(env.SkillRunnerClient.Invocations);
            Assert.Equal(env.TestData.ForeignUser.Id, invocation.Caller.Id); // Foreign user clicked the button.
            Assert.Equal(env.TestData.Member.Id, invocation.TriggeringMessageAuthor?.Id); // Original message from member.
            Assert.Equal(messageContext.MessageUrl, invocation.MessageUrl);
            Assert.NotNull(invocation.MessageUrl);
        }

        [Theory]
        [InlineData(true, "", "this is a test", "recognized arguments", true)]
        [InlineData(true, "!", "this is a test", "this is a test", false)]
        [InlineData(false, "", "this is a test", "this is a test", false)]
        [InlineData(false, "!", "this is a test", "this is a test", false)]
        public async Task PerformsArgumentExtractionIfEnabled(bool recognitionEnabled, string sigil,
            string inputArguments, string outputArguments, bool didRecognition)
        {
            var env = TestEnvironment.Create();
            await env.AISettings.SetModelSettingsAsync(
                AIFeature.ArgumentRecognizer,
                new ModelSettings
                {
                    Model = Model.ChatGPTTurbo,
                    Prompt = new()
                    {
                        Version = PromptVersion.Version1,
                        Text =
                            """
                            Skill:{SkillName}
                            """,
                    },
                    Temperature = 1.0
                },
                env.TestData.Member);

            env.OpenAiClient.PushChatResult("recognized arguments");

            var skill = await env.CreateSkillAsync("pug");
            skill.Properties = new()
            {
                ArgumentExtractionEnabled = recognitionEnabled
            };

            await env.Db.SaveChangesAsync();

            env.SkillRunnerClient.PushResponse(new()
            {
                Success = true,
                Replies = new[] { "Pugged!" },
            });

            var messageContext = env.CreateFakeMessageContext(
                "remoteskillcall",
                $"pug {inputArguments}",
                room: new Room
                {
                    Name = "room",
                    PlatformRoomId = "Croom"
                },
                sigil: sigil,
                mentions: new[] { env.TestData.Member },
                messageId: "M.message",
                threadId: "M.thread");
            var remoteSkillCallSkill = env.Activate<RemoteSkillCallSkill>();

            await remoteSkillCallSkill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            var invocation = Assert.Single(env.SkillRunnerClient.Invocations);
            Assert.Equal(outputArguments, invocation.Arguments);

            Assert.NotNull(invocation.AuditProperties);
            if (didRecognition)
            {
                Assert.NotNull(invocation.AuditProperties.ArgumentRecognitionResult);
                var receivedPrompt = env.OpenAiClient.ReceivedChatPrompts.Single();
                Assert.NotNull(receivedPrompt);
                Assert.Equal("system: Skill:pug\n\nuser: this is a test\n", receivedPrompt.Format());

                Assert.Collection(messageContext.SentActivities,
                    recognizerActivity => {
                        var dest = recognizerActivity.GetOverriddenDestination();
                        Assert.NotNull(dest);
                        Assert.Equal(
                            $":robot_face: From your message, I inferred the following arguments\n```\nrecognized arguments\n```",
                            recognizerActivity.Text);

                        Assert.Equal("Croom", dest.Address.Id);
                        Assert.Equal(ChatAddressType.Room, dest.Address.Type);
                        Assert.Equal("M.thread", dest.Address.ThreadId);
                    },
                    response => Assert.Equal("Pugged!", response.Text));
            }
            else
            {
                Assert.Null(invocation.AuditProperties.ArgumentRecognitionResult);
                Assert.Equal(new[] { "Pugged!" }, messageContext.SentMessages);
            }
        }

        [Fact]
        public async Task ArgumentExtractionPropagatesMentions()
        {
            var env = TestEnvironment.Create();
            await env.AISettings.SetModelSettingsAsync(
                AIFeature.ArgumentRecognizer,
                new ModelSettings
                {
                    Model = Model.ChatGPTTurbo,
                    Prompt = new()
                    {
                        Version = PromptVersion.Version1,
                        Text =
                            """
                            Skill:{SkillName}
                            """,
                    },
                    Temperature = 1.0
                },
                env.TestData.Member);

            env.OpenAiClient.PushChatResult("to <@Uhome>");

            var skill = await env.CreateSkillAsync("pug");
            skill.Properties = new()
            {
                ArgumentExtractionEnabled = true
            };

            await env.Db.SaveChangesAsync();

            env.SkillRunnerClient.PushResponse(new()
            {
                Success = true,
                Replies = new[] { "Pugged!" },
            });

            var messageContext = env.CreateFakeMessageContext(
                "remoteskillcall",
                $"pug send some pugs to <@Uhome>",
                messageId: "M.message",
                threadId: "M.thread",
                room: new Room { Name = "room", PlatformRoomId = "Croom" },
                mentions: new[] { env.TestData.Member },
                sigil: "");
            var remoteSkillCallSkill = env.Activate<RemoteSkillCallSkill>();

            await remoteSkillCallSkill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            var invocation = Assert.Single(env.SkillRunnerClient.Invocations);
            Assert.Equal("to <@Uhome>", invocation.Arguments);
            Assert.NotNull(invocation.TokenizedArguments);
            Assert.Collection(invocation.TokenizedArguments,
                a1 => {
                    Assert.Equal("to", a1.Value);
                    Assert.IsType<Argument>(a1);
                },
                a2 => {
                    Assert.Equal("<@Uhome>", a2.Value);
                    var m = Assert.IsType<MentionArgument>(a2);
                    Assert.Equal(env.TestData.Member.User.PlatformUserId, m.Mentioned.Id);
                    Assert.Equal(env.TestData.Member.DisplayName, m.Mentioned.Name);
                    Assert.Equal(env.TestData.Member.DisplayName, m.Mentioned.UserName);
                });

            Assert.NotNull(invocation.AuditProperties);
            Assert.Collection(messageContext.SentActivities,
                recognizerActivity => {
                    var dest = recognizerActivity.GetOverriddenDestination();
                    Assert.NotNull(dest);
                    Assert.Equal(
                        $":robot_face: From your message, I inferred the following arguments\n```\nto @{env.TestData.Member.DisplayName}\n```",
                        recognizerActivity.Text);

                    Assert.Equal("Croom", dest.Address.Id);
                    Assert.Equal(ChatAddressType.Room, dest.Address.Type);
                    Assert.Equal("M.thread", dest.Address.ThreadId);
                },
                response => Assert.Equal("Pugged!", response.Text));
        }

        [Fact]
        public async Task ReportsArgumentExtractionFailure()
        {
            using var activity = new Activity("Test").Start();
            var env = TestEnvironment.Create();
            await env.AISettings.SetModelSettingsAsync(
                AIFeature.ArgumentRecognizer,
                new ModelSettings
                {
                    Model = Model.DavinciText,
                    Prompt = new()
                    {
                        Version = PromptVersion.Version1,
                        Text =
                            """
                            MSG:{Message}
                            """,
                    },
                    Temperature = 1.0
                },
                env.TestData.Member);

            env.OpenAiClient.PushChatResult(new Exception("yoink"));

            var skill = await env.CreateSkillAsync("pug");
            skill.Properties = new()
            {
                ArgumentExtractionEnabled = true
            };

            await env.Db.SaveChangesAsync();

            var messageContext = env.CreateFakeMessageContext(
                "remoteskillcall",
                $"pug oops",
                room: new Room
                {
                    Name = "room",
                    PlatformRoomId = "C00000000"
                });

            var remoteSkillCallSkill = env.Activate<RemoteSkillCallSkill>();

            await remoteSkillCallSkill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            Assert.Empty(env.SkillRunnerClient.Invocations);

            var sent = Assert.Single(messageContext.SentMessages);
            Assert.Equal(
                $"Argument recognition for `pug` failed. Use `pug!` to skip recognition and pass arguments directly. {WebConstants.GetContactSupportSentence()}",
                sent);
        }

        [Fact]
        public async Task ArgumentExtractionPreservesMentions()
        {
            var env = TestEnvironment.Create();
            await env.AISettings.SetModelSettingsAsync(
                AIFeature.ArgumentRecognizer,
                new ModelSettings
                {
                    Model = Model.DavinciText,
                    Prompt = new()
                    {
                        Version = PromptVersion.Version1,
                        Text =
                            """
                            Skill:{SkillName}
                            """,
                    },
                    Temperature = 1.0
                },
                env.TestData.Member);

            env.OpenAiClient.PushChatResult($"<@U123> <@{env.TestData.Member.User.PlatformUserId}> <@U456>");

            var skill = await env.CreateSkillAsync("pug");
            skill.Properties = new()
            {
                ArgumentExtractionEnabled = true,
            };

            await env.Db.SaveChangesAsync();

            var messageContext = env.CreateFakeMessageContext(
                "remoteskillcall",
                $"pug send pugs to <@{env.TestData.Member.User.PlatformUserId}>",
                mentions: new[]
                {
                    env.TestData.Member
                    // U123, U456 not mentioned in the original message.
                },
                room: new Room
                {
                    Name = "room",
                    PlatformRoomId = "C00000000"
                });

            var remoteSkillCallSkill = env.Activate<RemoteSkillCallSkill>();

            await remoteSkillCallSkill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            var invocation = Assert.Single(env.SkillRunnerClient.Invocations);
            Assert.NotNull(invocation.TokenizedArguments);
            Assert.Collection(invocation.TokenizedArguments,
                arg => {
                    Assert.Equal($"<@U123>", arg.Value);
                    Assert.IsType<Argument>(arg);
                },
                arg => {
                    Assert.Equal($"<@{env.TestData.Member.User.PlatformUserId}>", arg.Value);
                    var mentionArg = Assert.IsType<MentionArgument>(arg);
                    Assert.Equal(env.TestData.Member.User.PlatformUserId, mentionArg.Mentioned.Id);
                    Assert.Equal(env.TestData.Member.User.DisplayName, mentionArg.Mentioned.UserName);
                    Assert.Equal(env.TestData.Member.User.DisplayName, mentionArg.Mentioned.Name);
                },
                arg => {
                    Assert.Equal($"<@U456>", arg.Value);
                    Assert.IsType<Argument>(arg);
                });
        }

        [Fact]
        public async Task WithMultiplePatternMatchesCallsAllMatchedSkillsWithMatchedPattern()
        {
            var env = TestEnvironment.Create();
            var skills = new[]
            {
                await env.CreateSkillAsync("pug"), await env.CreateSkillAsync("yell", language: CodeLanguage.Python)
            };

            env.SkillRunnerClient.PushResponse(new SkillRunResponse
            {
                Success = true,
                Errors = new List<RuntimeError>(),
                Content = null,
                ContentType = null,
                Replies = new List<string>
                {
                    "I also got your message loud and clear!"
                }
            });

            env.SkillRunnerClient.PushResponse(new SkillRunResponse
            {
                Success = true,
                Errors = new List<RuntimeError>(),
                Content = null,
                ContentType = null,
                Replies = new List<string>
                {
                    "Got your message loud and clear!"
                }
            });

            var messageContext = env.CreateFakeMessageContext(
                "remoteskillcall",
                "Some random message",
                room: new Room
                {
                    PlatformRoomId = "C00000000",
                    Name = "cool-room"
                },
                patterns: skills.Select(s => new SkillPattern
                {
                    Skill = s,
                    Pattern = s.Name + " pattern"
                }).ToList());

            var skill = env.Activate<RemoteSkillCallSkill>();

            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            Assert.Equal(2, messageContext.SentMessages.Count);
            Assert.Equal("Got your message loud and clear!", messageContext.SentMessages[0]);
            Assert.Equal("I also got your message loud and clear!", messageContext.SentMessages[1]);
            Assert.Equal(2, env.SkillRunnerClient.Invocations.Count);
            var firstInvocationPattern = env.SkillRunnerClient.Invocations[0].Pattern;
            var secondInvocationPattern = env.SkillRunnerClient.Invocations[1].Pattern;
            Assert.NotNull(firstInvocationPattern);
            Assert.Equal("pug pattern", firstInvocationPattern.Pattern);
            Assert.NotNull(secondInvocationPattern);
            Assert.Equal("yell pattern", secondInvocationPattern.Pattern);
        }

        [Fact]
        public async Task ReportsWithResponseRepliesWhenErrorOccurs()
        {
            var env = TestEnvironment.Create();
            await env.CreateSkillAsync("pug", enabled: false);
            env.SkillRunnerClient.PushResponse(new SkillRunResponse
            {
                Success = false,
                Replies = new List<string>
                {
                    "Failed to run the `pug` skill because it is disabled."
                },
                Errors = new[]
                {
                    new RuntimeError
                    {
                        Description = "That shit not enabled, yo."
                    }
                }
            });

            var messageContext = env.CreateFakeMessageContext("remoteskillcall", "pug bomb");
            var skill = env.Activate<RemoteSkillCallSkill>();

            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            Assert.Equal(
                "Failed to run the `pug` skill because it is disabled.",
                messageContext.SentMessages.Single());
        }

        [Theory]
        [InlineData((string?)null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task ReplacesEmptyReplyWithMessage(string value)
        {
            var env = TestEnvironment.Create();
            await env.CreateSkillAsync("pug");
            env.SkillRunnerClient.PushResponse(new SkillRunResponse
            {
                Success = true,
                Replies = new List<string>
                {
                    value
                },
                Errors = new List<RuntimeError>(),
                ContentType = null,
                Content = null
            });

            var messageContext = env.CreateFakeMessageContext("remoteskillcall", "pug bomb");
            var skill = env.Activate<RemoteSkillCallSkill>();

            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            Assert.Equal(
                "An empty reply was returned by the Bot skill.",
                messageContext.SentMessages.Single());
        }

        [Fact]
        public async Task ReportsGenericErrorMessageIfScriptFailsWithNoReplies()
        {
            var env = TestEnvironment.Create();
            await env.CreateSkillAsync("pug");
            var compilationErrors = new[] { new RuntimeError() };
            env.SkillRunnerClient.PushResponse(new SkillRunResponse
            {
                Success = false,
                Replies = new List<string>(),
                Errors = compilationErrors.ToList(),
                ContentType = null,
                Content = null
            });

            var messageContext = env.CreateFakeMessageContext("remoteskillcall", "pug bomb");
            var skill = env.Activate<RemoteSkillCallSkill>();

            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            Assert.Equal(
                "1 error occurred running the skill. Visit https://app.ab.bot/skills/pug to fix it.",
                messageContext.SentMessages.Single());
        }

        [Fact]
        public async Task ReportsGenericErrorMessagesIfScriptFailsWithNoReplies()
        {
            var env = TestEnvironment.Create();
            await env.CreateSkillAsync("pug");
            var compilationErrors = new[] { new RuntimeError(), new RuntimeError(), };
            env.SkillRunnerClient.PushResponse(new SkillRunResponse
            {
                Success = false,
                Replies = new List<string>(),
                Errors = compilationErrors.ToList(),
                ContentType = null,
                Content = null
            });

            var messageContext = env.CreateFakeMessageContext("remoteskillcall", "pug bomb");
            var skill = env.Activate<RemoteSkillCallSkill>();

            await skill.OnMessageActivityAsync(messageContext, CancellationToken.None);

            Assert.Equal(
                "2 errors occurred running the skill. Visit https://app.ab.bot/skills/pug to fix it.",
                messageContext.SentMessages.Single());
        }

        [Fact]
        public async Task WrapsRemoteCallExceptionWithSkillCallException()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            var calledSkill = await env.CreateSkillAsync("pug");
            env.SkillRunnerClient.PushException(new InvalidOperationException("Unknown bad stuff happened."));
            var messageContext = env.CreateFakeMessageContext("remoteskillcall", "pug bomb");
            var skill = env.Activate<RemoteSkillCallSkill>();

            var exception = await Assert.ThrowsAsync<SkillRunException>(() =>
                skill.OnMessageActivityAsync(messageContext, CancellationToken.None));

            Assert.Same(calledSkill, exception.Skill);
            Assert.Equal(organization.PlatformId, exception.PlatformId);
        }
    }

    public class TheGetSkillUsageTextMethod
    {
        [Fact]
        public async Task ReturnsUsageForSkill()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            await env.CreateSkillAsync("pug", usageText: "just pug it");
            var skill = env.Activate<RemoteSkillCallSkill>();

            var result = await skill.GetSkillUsageText("pug", organization);

            Assert.Equal("just pug it", result);
        }

        [Fact]
        public async Task ReplacesReplacementStrings()
        {
            var env = TestEnvironment.Create();
            var organization = env.TestData.Organization;
            await env.CreateSkillAsync("pug", usageText: "{bot} {skill} bomb");
            var skill = env.Activate<RemoteSkillCallSkill>();

            var result = await skill.GetSkillUsageText("pug", organization);

            Assert.Equal("@test-abbot pug bomb", result);
        }
    }
}
