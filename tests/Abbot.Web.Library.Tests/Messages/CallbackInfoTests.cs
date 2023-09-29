using Serious.Abbot.Events;
using Serious.Abbot.Skills;
using Serious.Slack.BlockKit;
using Serious.Text;
using Xunit;

public class CallbackInfoTests
{
    public class TheTryParseMethod
    {
        [Theory]
        [InlineData("b:echo:foo bar", "echo", "foo bar")]
        [InlineData("b:echo", "echo", null)]
        public void ParsesBuiltInSkillInfoFromCallbackId(
            string callbackId,
            string? expectedSkillName,
            string? expectedContextId)
        {
            var result = CallbackInfo.TryParse(callbackId, out var callbackInfo);

            Assert.True(result);
            var builtInCallbackInfo = Assert.IsType<BuiltInSkillCallbackInfo>(callbackInfo);
            Assert.Equal(expectedSkillName, builtInCallbackInfo.SkillName);
            Assert.Equal(expectedContextId, builtInCallbackInfo.ContextId);
        }

        [Theory]
        [InlineData("s:42:foo", 42, "foo")]
        [InlineData("s:42:", 42, "")]
        [InlineData("s:42", 42, null)]
        public void ParsesUserSkillCallInfoFromCallbackId(
            string callbackId,
            int? expectedSkillId,
            string? expectedContextId)
        {
            var result = CallbackInfo.TryParse(callbackId, out var callbackInfo);

            Assert.True(result);
            var userSkillCallbackInfo = Assert.IsType<UserSkillCallbackInfo>(callbackInfo);
            Assert.Equal(expectedSkillId, userSkillCallbackInfo.SkillId);
            Assert.Equal(expectedContextId, userSkillCallbackInfo.ContextId);
        }

        [Theory]
        [InlineData("i:TestHandler", "TestHandler", null)]
        [InlineData("i:TestHandler:", "TestHandler", "")]
        [InlineData("i:TestHandler:123", "TestHandler", "123")]
        [InlineData("i:TestHandler:123:456", "TestHandler", "123:456")]
        public void ParsesHandlerCallInfoFromCallbackId(
            string callbackId,
            string expectedTypeName,
            string? expectedContextId)
        {
            var result = CallbackInfo.TryParse(callbackId, out var callbackInfo);

            Assert.True(result);
            var viewCallbackInfo = Assert.IsType<InteractionCallbackInfo>(callbackInfo);
            Assert.Equal(expectedTypeName, viewCallbackInfo.TypeName);
            Assert.Equal(expectedContextId, viewCallbackInfo.ContextId);
        }

        [Theory]
        [InlineData("")]
        [InlineData("s")]
        [InlineData("b")]
        [InlineData("42")]
        [InlineData("42|block-id")]
        public void ReturnsFalseWhenFormatWrong(string callbackId)
        {
            var result = CallbackInfo.TryParse(callbackId, out var callbackInfo);

            Assert.False(result);
            Assert.Null(callbackInfo);
        }
    }

    public class TheToStringMethod
    {
        [Theory]
        [InlineData(42, null, "s:42")]
        [InlineData(42, "", "s:42:")]
        [InlineData(42, "context-id", "s:42:context-id")]
        public void WithUserSkillCallbackInfoReturnsFormattedString(int skillId, string? contextId, string expected)
        {
            var callbackInfo = new UserSkillCallbackInfo(new(skillId), contextId);

            Assert.Equal(expected, callbackInfo.ToString());
        }

        [Theory]
        [InlineData("echo", null, "b:echo")]
        [InlineData("echo", "", "b:echo:")]
        [InlineData("echo", "context-id", "b:echo:context-id")]
        public void WithBuiltInCallbackInfoReturnsFormattedString(string skillName, string? contextId, string expected)
        {
            var callbackInfo = new BuiltInSkillCallbackInfo(skillName, contextId);

            Assert.Equal(expected, callbackInfo.ToString());
        }

        [Theory]
        [InlineData("TestHandler", null, "i:TestHandler")]
        [InlineData("TestHandler", "", "i:TestHandler:")]
        [InlineData("TestHandler", "123", "i:TestHandler:123")]
        [InlineData("TestHandler", "123:456", "i:TestHandler:123:456")]
        public void WithInteractionCallbackInfoReturnsFormattedString(string typeName, string? contextId, string expected)
        {
            var callbackInfo = new InteractionCallbackInfo(typeName, contextId);

            Assert.Equal(expected, callbackInfo.ToString());
        }

        [Theory]
        [InlineData(null, "i:TestHandler")]
        [InlineData("", "i:TestHandler:")]
        [InlineData("123", "i:TestHandler:123")]
        [InlineData("123:456", "i:TestHandler:123:456")]
        public void WithInteractionCallbackInfoForReturnsFormattedString(string? contextId, string expected)
        {
            var callbackInfo = InteractionCallbackInfo.For<TestHandler>(contextId);

            Assert.Equal(expected, callbackInfo.ToString());
        }

        class TestHandler : IHandler { }
    }

    public class TheTryGetCallbackInfoPayloadElementMethod
    {
        [Fact]
        public void RetrievesCallbackInfoFromActionId()
        {
            var buttonElement = new ButtonElement()
            {
                ActionId = InteractionCallbackInfo.For<IHandler>()
            };

            var result = CallbackInfo.TryGetCallbackInfoPayloadElement<CallbackInfo>(
                buttonElement,
                out var callbackInfo);

            Assert.True(result);
            var interactionCallbackInfo = Assert.IsType<InteractionCallbackInfo>(callbackInfo);
            Assert.Equal("IHandler", interactionCallbackInfo.TypeName);
        }

        [Fact]
        public void RetrievesCallbackInfoFromWrappedActionId()
        {
            var buttonElement = new ButtonElement()
            {
                ActionId = new WrappedValue(InteractionCallbackInfo.For<IHandler>(), "foo bar")
            };

            var result = CallbackInfo.TryGetCallbackInfoPayloadElement<CallbackInfo>(
                buttonElement,
                out var callbackInfo);

            Assert.True(result);
            var interactionCallbackInfo = Assert.IsType<InteractionCallbackInfo>(callbackInfo);
            Assert.Equal("IHandler", interactionCallbackInfo.TypeName);
        }

        [Fact]
        public void RetrievesCallbackInfoFromActionIdForBuiltInSkillCallbackInfo()
        {
            var buttonElement = new ButtonElement()
            {
                ActionId = new BuiltInSkillCallbackInfo("echo")
            };

            var result = CallbackInfo.TryGetCallbackInfoPayloadElement<CallbackInfo>(
                buttonElement,
                out var callbackInfo);

            Assert.True(result);
            var interactionCallbackInfo = Assert.IsType<BuiltInSkillCallbackInfo>(callbackInfo);
            Assert.Equal("echo", interactionCallbackInfo.SkillName);
        }

        [Fact]
        public void RetrievesTypedCallbackInfoFromActionId()
        {
            var buttonElement = new ButtonElement()
            {
                ActionId = InteractionCallbackInfo.For<IHandler>()
            };

            var result = CallbackInfo.TryGetCallbackInfoPayloadElement<InteractionCallbackInfo>(
                buttonElement,
                out var callbackInfo);

            Assert.True(result);
            Assert.IsType<InteractionCallbackInfo>(callbackInfo);
            Assert.Equal("IHandler", callbackInfo.TypeName);
        }

        [Fact]
        public void RetrievesCallbackInfoFromBlockId()
        {
            var buttonElement = new ButtonElement();
            ((IPayloadElement)buttonElement).BlockId = InteractionCallbackInfo.For<IHandler>();

            var result = CallbackInfo.TryGetCallbackInfoPayloadElement<CallbackInfo>(
                buttonElement,
                out var callbackInfo);

            Assert.True(result);
            var interactionCallbackInfo = Assert.IsType<InteractionCallbackInfo>(callbackInfo);
            Assert.Equal("IHandler", interactionCallbackInfo.TypeName);
        }

        [Fact]
        public void RetrievesTypedCallbackInfoFromBlockId()
        {
            var buttonElement = new ButtonElement();
            ((IPayloadElement)buttonElement).BlockId = InteractionCallbackInfo.For<IHandler>();

            var result = CallbackInfo.TryGetCallbackInfoPayloadElement<InteractionCallbackInfo>(
                buttonElement,
                out var callbackInfo);

            Assert.True(result);
            Assert.IsType<InteractionCallbackInfo>(callbackInfo);
            Assert.Equal("IHandler", callbackInfo.TypeName);
        }
    }
}
