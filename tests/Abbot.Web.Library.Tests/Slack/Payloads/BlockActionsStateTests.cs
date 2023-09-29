using System;
using System.Collections.Generic;
using Serious.Slack.BlockKit;
using Serious.Slack.Converters;
using Serious.Slack.Payloads;
using Xunit;

public class BlockActionsStateTests
{
    public class TheTryBindRecordMethod
    {
        public record SubmissionState(
            [property: Bind("when-input", "when-action")]
            string SelectedOption,

            [property: Bind("date-picker-input", "date-picker-action")]
            string SelectedDate,

            [property: Bind("channels-input", "channels-action")]
            IReadOnlyList<string> SelectedChannels);

        [Fact]
        public void CanBindBlockActionsStateToSubmissionState()
        {
            var values = new Dictionary<string, Dictionary<string, IPayloadElement>>()
            {
                ["when-input"] = new()
                {
                    ["when-action"] = new RadioButtonGroup
                    {
                        SelectedOption = new CheckOption { Value = "later" }
                    }
                },
                ["date-picker-input"] = new()
                {
                    ["date-picker-action"] = new DatePicker
                    {
                        Value = "2022-01-23"
                    }
                },
                ["channels-input"] = new()
                {
                    ["channels-action"] = new ChannelsMultiSelectMenu
                    {
                        SelectedValues = new[] { "C12345678", "C012342134" }
                    }
                }
            };
            var state = new BlockActionsState(values);

            var result = state.TryBindRecord<SubmissionState>(out var submissionState);

            Assert.True(result);
            Assert.NotNull(submissionState);
            Assert.Equal("later", submissionState.SelectedOption);
            Assert.Equal("2022-01-23", submissionState.SelectedDate);
            Assert.Collection(submissionState.SelectedChannels,
                channel => Assert.Equal("C12345678", channel),
                channel => Assert.Equal("C012342134", channel));

            Assert.Equal(submissionState, state.BindRecord<SubmissionState>());
        }

        public record SubmissionState2(
            [property: Bind("range")]
            string SelectedStart,

            [property: Bind("range")]
            string SelectedEnd);

        [Fact]
        public void CanBindBlockActionsStateToSubmissionStateWhenMoreThanOneActionHasSameBlockId()
        {
            var values = new Dictionary<string, Dictionary<string, IPayloadElement>>()
            {
                ["range"] = new()
                {
                    ["SelectedStart"] = new RadioButtonGroup
                    {
                        SelectedOption = new CheckOption { Value = "the-start" }
                    },
                    ["SelectedEnd"] = new RadioButtonGroup
                    {
                        SelectedOption = new CheckOption { Value = "the-end" }
                    }
                }
            };
            var state = new BlockActionsState(values);

            var result = state.TryBindRecord<SubmissionState2>(out var submissionState);

            Assert.True(result);
            Assert.NotNull(submissionState);
            Assert.Equal("the-start", submissionState.SelectedStart);
            Assert.Equal("the-end", submissionState.SelectedEnd);

            Assert.Equal(submissionState, state.BindRecord<SubmissionState2>());
        }

        public record ConventionSubmissionState(
            string SelectedOption,
            string SelectedDate,
            IReadOnlyList<string> SelectedChannels);

        [Fact]
        public void CanBindBlockActionsStateToConventionBasedSubmissionState()
        {
            var values = new Dictionary<string, Dictionary<string, IPayloadElement>>()
            {
                [nameof(ConventionSubmissionState.SelectedOption)] = new()
                {
                    ["xyZ"] = new RadioButtonGroup
                    {
                        SelectedOption = new CheckOption { Value = "later" }
                    }
                },
                [nameof(ConventionSubmissionState.SelectedDate)] = new()
                {
                    ["abx"] = new DatePicker
                    {
                        Value = "2022-01-23"
                    }
                },
                [nameof(ConventionSubmissionState.SelectedChannels)] = new()
                {
                    ["pry"] = new ChannelsMultiSelectMenu
                    {
                        SelectedValues = new[] { "C12345678", "C012342134" }
                    }
                }
            };
            var state = new BlockActionsState(values);

            var result = state.TryBindRecord<ConventionSubmissionState>(out var submissionState);

            Assert.True(result);
            Assert.NotNull(submissionState);
            Assert.Equal("later", submissionState.SelectedOption);
            Assert.Equal("2022-01-23", submissionState.SelectedDate);
            Assert.Collection(submissionState.SelectedChannels,
                channel => Assert.Equal("C12345678", channel),
                channel => Assert.Equal("C012342134", channel));

            Assert.Equal(submissionState, state.BindRecord<ConventionSubmissionState>());
        }

        public record UnbindableState(string Good)
        {
            public string? Bad { get; set; }
        }

        [Fact]
        public void ReturnsFalseForInvalidType()
        {
            var values = new Dictionary<string, Dictionary<string, IPayloadElement>>();
            var state = new BlockActionsState(values);

            var result = state.TryBindRecord<UnbindableState>(out var bound);

            Assert.False(result);
            Assert.Null(bound);

            Assert.Throws<InvalidOperationException>(() => state.BindRecord<UnbindableState>());
        }
    }

    public class TheTryGetAsMethod
    {
        [Fact]
        public void CanRetrieveElementByBlockIdAndActionId()
        {
            var values = new Dictionary<string, Dictionary<string, IPayloadElement>>()
            {
                ["block_id"] = new()
                {
                    ["action_id"] = new ButtonElement { Value = "value" }
                }
            };
            var state = new BlockActionsState(values);

            Assert.True(state.TryGetAs<ButtonElement>("block_id", "action_id", out var element));
            Assert.Equal("value", element.Value);

            Assert.Same(element, state.GetAs<ButtonElement>("block_id", "action_id"));
        }

        [Theory]
        [InlineData("block_idx", "action_id")]
        [InlineData("block_id", "action_idx")]
        public void ReturnsFalseWhenNoSuchElementExists(string blockId, string actionId)
        {
            var values = new Dictionary<string, Dictionary<string, IPayloadElement>>()
            {
                ["block_id"] = new()
                {
                    ["action_id"] = new ButtonElement { Value = "value" },
                    ["action2_id"] = new ButtonElement { Value = "value" }
                }
            };
            var state = new BlockActionsState(values);

            Assert.False(state.TryGetAs<ButtonElement>(blockId, actionId, out var element));
            Assert.Null(element);

            Assert.Throws<KeyNotFoundException>(() => state.GetAs<ButtonElement>(blockId, actionId));
        }

        [Fact]
        public void ReturnsFalseWhenElementIsWrongType()
        {
            var values = new Dictionary<string, Dictionary<string, IPayloadElement>>()
            {
                ["block_id"] = new()
                {
                    ["action_id"] = new PlainTextInput { Value = "value" }
                }
            };
            var state = new BlockActionsState(values);

            Assert.False(state.TryGetAs<ButtonElement>("block_id", "action_id", out var element));
            Assert.Null(element);

            Assert.Throws<KeyNotFoundException>(() => state.GetAs<ButtonElement>("block_id", "action_id"));
        }

        [Fact]
        public void CanRetrieveElementWithoutActionIdWhenBlockContainsOnlyOneAction()
        {
            var values = new Dictionary<string, Dictionary<string, IPayloadElement>>()
            {
                ["block_id"] = new()
                {
                    ["action_id"] = new ButtonElement { Value = "value" },
                }
            };
            var state = new BlockActionsState(values);

            Assert.True(state.TryGetAs<ButtonElement>("block_id", null, out var element));
            Assert.Equal("value", element.Value);

            Assert.Same(element, state.GetAs<ButtonElement>("block_id", null));
        }

        [Fact]
        public void ReturnsFalseWhenNoActionIdProvidedAndBlockContainsMultipleActions()
        {
            var values = new Dictionary<string, Dictionary<string, IPayloadElement>>()
            {
                ["block_id"] = new()
                {
                    ["action_id"] = new ButtonElement { Value = "value" },
                    ["action_id2"] = new ButtonElement { Value = "value" },
                }
            };
            var state = new BlockActionsState(values);

            Assert.False(state.TryGetAs<ButtonElement>("block_id", null, out var _));

            Assert.Throws<KeyNotFoundException>(() => state.GetAs<ButtonElement>("block_id", null));
        }
    }
}
