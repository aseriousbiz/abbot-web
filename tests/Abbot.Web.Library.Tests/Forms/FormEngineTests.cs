using System;
using System.Collections.Generic;
using System.Linq;
using Abbot.Common.TestHelpers;
using Newtonsoft.Json.Linq;
using Serious.Abbot.Entities;
using Serious.Abbot.Forms;
using Serious.Slack;
using Serious.Slack.BlockKit;
using Serious.Slack.Payloads;
using Xunit;

public class FormEngineTests
{
    public class TheTranslateFormMethod
    {
        [Fact]
        public void ProperlyTranslatesFixedValueField()
        {
            var block = RunSingleFieldTranslateTest(FormFieldType.FixedValue, "the_value");
            Assert.Null(block);
        }

        [Theory]
        [InlineData(FormFieldType.SingleLineText, false)]
        [InlineData(FormFieldType.MultiLineText, true)]
        public void ProperlyTranslatesTextFields(FormFieldType fieldType, bool isMultiLine)
        {
            var block = RunSingleFieldTranslateTest(fieldType, "initial_value");
            var input = Assert.IsType<Input>(block);
            Assert.Equal("__field:test_field", input.BlockId);
            Assert.Equal("Test Field", input.Label.Text);

            var element = Assert.IsType<PlainTextInput>(input.Element);
            Assert.Equal(isMultiLine, element.Multiline);
            Assert.Equal("initial_value", element.InitialValue);
            Assert.Equal("Test Description", element.Placeholder);
        }

        [Theory]
        [InlineData("true", true)]
        [InlineData("false", false)]
        [InlineData("anything", false)]
        [InlineData(null, false)]
        public void ProperlyTranslatesCheckbox(string? initialValue, bool isChecked)
        {
            var block = RunSingleFieldTranslateTest(FormFieldType.Checkbox, initialValue);
            var actions = Assert.IsType<Actions>(block);
            Assert.Equal("__field:test_field", actions.BlockId);

            var element = Assert.IsType<CheckboxGroup>(Assert.Single(actions.Elements));
            var option = element.Options.Single();
            if (isChecked)
            {
                Assert.Equal(new[] { option }, element.InitialOptions);
            }
            else
            {
                Assert.Empty(element.InitialOptions);
            }

            Assert.Equal("Test Field", option.Text.Text);
            Assert.Equal("true", option.Value);
        }

        [Fact]
        public void ProperlyTranslatesDropdownList()
        {
            var block = RunSingleFieldTranslateTest(FormFieldType.DropdownList,
                "option_2",
                new("Option 1", "option_1"),
                new("Option 2", "option_2"),
                new("Option 3", "option_3"));

            var input = Assert.IsType<Input>(block);
            Assert.Equal("__field:test_field", input.BlockId);
            Assert.Equal("Test Field", input.Label.Text);

            var element = Assert.IsType<StaticSelectMenu>(input.Element);
            Assert.Equal(new[] { "Option 1", "Option 2", "Option 3" },
                element.Options?.Select(o => o.Text.Text).ToArray());

            Assert.Equal(new[] { "option_1", "option_2", "option_3" }, element.Options?.Select(o => o.Value).ToArray());
            Assert.Equal("option_2", element.InitialOption?.Value);
        }

        [Fact]
        public void ProperlyTranslatesMultiDropdownList()
        {
            var block = RunSingleFieldTranslateTest(FormFieldType.MultiDropdownList,
                new[] { "option_1", "option_3" },
                new("Option 1", "option_1"),
                new("Option 2", "option_2"),
                new("Option 3", "option_3"));

            var input = Assert.IsType<Input>(block);
            Assert.Equal("__field:test_field", input.BlockId);
            Assert.Equal("Test Field", input.Label.Text);

            var element = Assert.IsType<MultiStaticSelectMenu>(input.Element);
            Assert.Equal(new[] { "Option 1", "Option 2", "Option 3" },
                element.Options?.Select(o => o.Text.Text).ToArray());

            Assert.Equal(new[] { "option_1", "option_2", "option_3" }, element.Options?.Select(o => o.Value).ToArray());
            Assert.Equal(new[] { "option_1", "option_3" }, element.InitialOptions?.Select(o => o.Value).ToArray());
        }

        [Fact]
        public void WithMultiDropDownListThrowsExceptionIfInitialValueIsString()
        {
            var exception = Assert.Throws<InvalidFormFieldException>(() => RunSingleFieldTranslateTest(
                FormFieldType.MultiDropdownList,
                initialValue: "option_1,option_3",
                new("Option 1", "option_1"),
                new("Option 2", "option_2"),
                new("Option 3", "option_3")));

            Assert.Equal("test_field", exception.FieldId);
            Assert.Equal("InitialValue", exception.TemplateParameterName);
            Assert.Equal("MultiDropdownList InitialValue must be an array of strings.", exception.Message);
        }

        [Fact]
        public void WithMultiDropDownListThrowsExceptionIfDefaultValueIsString()
        {
            var env = TestEnvironment.Create();
            var testDefinition = new FormDefinition
            {
                Fields =
                {
                    new FormFieldDefinition
                    {
                        Id = "test_field",
                        Title = "Test Field",
                        Type = FormFieldType.MultiDropdownList,
                        DefaultValue = "{{options}}",
                        Description = "Test Description",
                        Options = new[] { new FormSelectOption("one", "one") }
                    },
                },
            };
            var engine = env.Activate<FormEngine>();

            var exception = Assert.Throws<InvalidFormFieldException>(
                () => engine.TranslateForm(testDefinition, new object()));

            Assert.Equal("test_field", exception.FieldId);
            Assert.Equal("DefaultValue", exception.TemplateParameterName);
            Assert.Equal("MultiDropdownList DefaultValue must be an array of strings.", exception.Message);
        }

        [Fact]
        public void ProperlyTranslatesRadioList()
        {
            var block = RunSingleFieldTranslateTest(FormFieldType.RadioList,
                "option_2",
                new("Option 1", "option_1"),
                new("Option 2", "option_2"),
                new("Option 3", "option_3"));

            var input = Assert.IsType<Input>(block);
            Assert.Equal("__field:test_field", input.BlockId);
            Assert.Equal("Test Field", input.Label.Text);

            var element = Assert.IsType<RadioButtonGroup>(input.Element);
            Assert.Equal(new[] { "Option 1", "Option 2", "Option 3" },
                element.Options.Select(o => o.Text.Text).ToArray());

            Assert.Equal(new[] { "option_1", "option_2", "option_3" }, element.Options.Select(o => o.Value).ToArray());
            Assert.Equal("option_2", element.InitialOption?.Value);
        }

        [Fact]
        public void ProperlyTranslatesDatePicker()
        {
            var block = RunSingleFieldTranslateTest(FormFieldType.DatePicker, "2022-01-01");
            var input = Assert.IsType<Input>(block);
            Assert.Equal("__field:test_field", input.BlockId);
            Assert.Equal("Test Field", input.Label.Text);

            var element = Assert.IsType<DatePicker>(input.Element);
            Assert.Equal("2022-01-01", element.InitialDate);
            Assert.Equal("Test Description", element.Placeholder);
        }

        [Fact]
        public void ProperlyTranslatesTimePicker()
        {
            var block = RunSingleFieldTranslateTest(FormFieldType.TimePicker, "23:00");
            var input = Assert.IsType<Input>(block);
            Assert.Equal("__field:test_field", input.BlockId);
            Assert.Equal("Test Field", input.Label.Text);

            var element = Assert.IsType<TimePicker>(input.Element);
            Assert.Equal("23:00", element.InitialTime);
            Assert.Equal("Test Description", element.Placeholder);
        }

        [Theory]
        [InlineData(FormFieldType.FixedValue, "{{simple_int}}", null)]
        [InlineData(FormFieldType.SingleLineText, "{{simple_int}}", "42")]
        [InlineData(FormFieldType.SingleLineText, "{{metadata.metadata_key}}", "metadata_value")]
        [InlineData(FormFieldType.SingleLineText, "{{obj.nested_int}}", "23")]
        [InlineData(FormFieldType.MultiLineText, "{{simple_string}} world", "hello world")]
        [InlineData(FormFieldType.DropdownList, "{{option}}2", "option_2")]
        [InlineData(FormFieldType.RadioList, "{{option}}1", "option_1")]
        [InlineData(FormFieldType.Checkbox, "{{truthy}}", "true")]
        [InlineData(FormFieldType.Checkbox, "{{falsey}}", null)]
        [InlineData(FormFieldType.DatePicker, "20{{simple_int}}-01-01", "2042-01-01")]
        [InlineData(FormFieldType.TimePicker, "{{obj.nested_int}}:00", "23:00")]
        public void ProperlyExecutesHandlebarsTemplatesOnInitialValue(
            FormFieldType type,
            string initialValue,
            string? expectedValue)
        {
            var block = RunSingleFieldTranslateTest(
                type,
                initialValue,
                new("Option 1", "option_1"),
                new("Option 2", "option_2"),
                new("Option 3", "option_3"));

            if (type == FormFieldType.FixedValue)
            {
                Assert.Null(block);
                return;
            }

            Assert.NotNull(block);

            // Do a little digging for the initial value
            var actualValue = block switch
            {
                Input inp => inp.Element switch
                {
                    PlainTextInput pt => pt.InitialValue,
                    StaticSelectMenu sm => sm.InitialOption?.Value,
                    RadioButtonGroup rg => rg.InitialOption?.Value,
                    DatePicker dp => dp.InitialDate,
                    TimePicker tp => tp.InitialTime,
                    _ => throw new Exception("Unknown input type")
                },
                Actions act => act.Elements.Single() switch
                {
                    CheckboxGroup grp => grp.InitialOptions.SingleOrDefault()?.Value,
                    _ => throw new Exception("Unknown action type")
                },
                _ => throw new Exception("Unknown block type")
            };

            Assert.Equal(expectedValue, actualValue);
        }

        [Fact]
        public void EvaluatesInitialValueTemplates()
        {
            var block = RunSingleFieldTranslateTest(
                FormFieldType.MultiDropdownList,
                new[]
                {
                    "{{option1_property}}",
                    "{{option3_property}}",
                },
                new("Option 1", "option_1"),
                new("Option 2", "option_2"),
                new("Option 3", "option_3"));
            Assert.NotNull(block);

            var multiSelect = Assert.IsType<MultiStaticSelectMenu>(Assert.IsType<Input>(block).Element);

            Assert.Equal(
            new[] { "option_1", "option_3" },
            multiSelect.InitialOptions!.Select(o => o.Value).ToArray());
        }

        [Fact]
        public void CanTranslateSeveralFields()
        {
            var templateContext = new {
                a = "a",
                b = "option_b",
                c = 3,
                is_it_true = "false"
            };

            var env = TestEnvironment.Create();
            var definition = new FormDefinition
            {
                Fields =
                {
                    new()
                    {
                        Id = "field_a",
                        Title = "Field A",
                        Type = FormFieldType.SingleLineText,
                        InitialValue = "This is field {{a}}",
                        Description = "It's Field A",
                    },
                    new()
                    {
                        Id = "field_b",
                        Title = "Field B",
                        Type = FormFieldType.Checkbox,
                        InitialValue = "{{is_it_true}}",
                        Description = "It's Field B",
                    },
                    new()
                    {
                        Id = "field_c",
                        Title = "Field C",
                        Type = FormFieldType.DropdownList,
                        InitialValue = "{{b}}",
                        Description = "It's Field C",
                        Options =
                        {
                            new("Option A", "option_a"),
                            new("Option B", "option_b"),
                            new("Option C", "option_c"),
                        }
                    },
                }
            };

            var engine = env.Activate<FormEngine>();
            var translated = engine.TranslateForm(definition, templateContext);

            Assert.Collection(translated,
                fieldA => {
                    var input = Assert.IsType<Input>(fieldA);
                    Assert.Equal("__field:field_a", input.BlockId);
                    var text = Assert.IsType<PlainTextInput>(input.Element);
                    Assert.Equal("This is field a", text.InitialValue);
                    Assert.Equal("It's Field A", text.Placeholder);
                    Assert.False(text.Multiline);
                },
                fieldB => {
                    var actions = Assert.IsType<Actions>(fieldB);
                    Assert.Equal("__field:field_b", actions.BlockId);
                    var checkbox = Assert.IsType<CheckboxGroup>(Assert.Single(actions.Elements));
                    Assert.Empty(checkbox.InitialOptions);
                    Assert.Equal(new[] { ("Field B", "true") },
                        checkbox.Options.Select(o => (o.Text.Text, o.Value)).ToArray());
                },
                fieldC => {
                    var input = Assert.IsType<Input>(fieldC);
                    Assert.Equal("__field:field_c", input.BlockId);
                    var dropdown = Assert.IsType<StaticSelectMenu>(input.Element);
                    Assert.Equal(("Option B", "option_b"),
                        (dropdown.InitialOption?.Text.Text, dropdown.InitialOption?.Value));

                    Assert.Equal(new[] { ("Option A", "option_a"), ("Option B", "option_b"), ("Option C", "option_c") },
                        dropdown.Options?.Select(o => (o.Text.Text, o.Value)).ToArray());
                });
        }

        static ILayoutBlock? RunSingleFieldTranslateTest(
            FormFieldType fieldType,
            object? initialValue,
            params FormSelectOption[] options)
        {
            var templateContext = new {
                simple_int = 42,
                simple_string = "hello",
                option = "option_",
                truthy = "true",
                falsey = "false",
                obj = new {
                    nested_int = 23,
                },
                option1_property = "option_1",
                option3_property = "option_3",
                metadata = new Dictionary<string, string>
                {
                    ["metadata_key"] = "metadata_value",
                }
            };

            var env = TestEnvironment.Create();
            var testDefinition = new FormDefinition
            {
                Fields =
                {
                    new FormFieldDefinition
                    {
                        Id = "test_field",
                        Title = "Test Field",
                        Type = fieldType,
                        InitialValue = initialValue,
                        Description = "Test Description",
                        Options = options.ToList(),
                    }
                },
            };

            var engine = env.Activate<FormEngine>();

            var translated = engine.TranslateForm(testDefinition, templateContext);

            return translated.SingleOrDefault();
        }
    }

    public class TheProcessFormSubmissionMethod
    {
        [Fact]
        public void ExtractsAllFieldValuesAndFixedValues()
        {
            var templateContext = new {
                world = "World",
                description = "<@U000001> or <#C000001>",
                default_option = "option_b",
            };

            var form = new FormDefinition
            {
                Fields =
                {
                    new()
                    {
                        Id = "field_a",
                        Type = FormFieldType.FixedValue,
                        InitialValue = "Hello, {{world}}",
                    },
                    new()
                    {
                        Id = "field_b",
                        Type = FormFieldType.SingleLineText,
                        InitialValue = "The Initial Value",
                        DefaultValue = "Hello, {{world}}.",
                    },
                    new()
                    {
                        Id = "field_c",
                        Type = FormFieldType.SingleLineText,
                        InitialValue = "The Initial Value",
                        DefaultValue = "Hello, {{world}} {{description}}",
                    },
                    new()
                    {
                        Id = "field_d",
                        Type = FormFieldType.Checkbox,
                        DefaultValue = "false",
                        Options =
                        {
                            new("Field D", "true"),
                        }
                    },
                    new()
                    {
                        Id = "field_e",
                        Type = FormFieldType.Checkbox,
                        DefaultValue = "false",
                        Options =
                        {
                            new("Field E", "true"),
                        }
                    },
                    new()
                    {
                        Id = "field_f",
                        Type = FormFieldType.DropdownList,
                        InitialValue = "option_a",
                        DefaultValue = "{{default_option}}",
                        Options =
                        {
                            new("Option A", "option_a"),
                            new("Option B", "option_b"),
                            new("Option C", "option_c"),
                        }
                    },
                    new()
                    {
                        Id = "field_g",
                        Type = FormFieldType.DropdownList,
                        InitialValue = "option_a",
                        DefaultValue = "{{default_option}}",
                        Options =
                        {
                            new("Option A", "option_a"),
                            new("Option B", "option_b"),
                            new("Option C", "option_c"),
                        }
                    },
                    new()
                    {
                        Id = "field_h",
                        Type = FormFieldType.MultiDropdownList,
                        InitialValue = new[] { "option_a", "option_b", "option_c" },
                        DefaultValue = new [] { "option_a", "option_b" },
                        Options =
                        {
                            new("Option A", "option_a"),
                            new("Option B", "option_b"),
                            new("Option C", "option_c"),
                        }
                    },
                    new()
                    {
                        Id = "field_i",
                        Type = FormFieldType.MultiDropdownList,
                        InitialValue = new[] { "option_a", "option_b", "option_c" },
                        DefaultValue = new [] { "option_a", "option_b" },
                        Options =
                        {
                            new("Option A", "option_a"),
                            new("Option B", "option_b"),
                            new("Option C", "option_c"),
                        }
                    },
                    new()
                    {
                        Id = "field_j",
                        Type = FormFieldType.FixedValue,
                        InitialValue = new JArray() { "Hello", "{{world}}" },
                    },
                    new()
                    {
                        Id = "field_k",
                        Type = FormFieldType.FixedValue,
                        InitialValue = new[] { "Hello", "{{world}}" },
                    },
                }
            };

            var payload = new ViewSubmissionPayload
            {
                View = new ModalView
                {
                    State = new BlockActionsState(
                        new Dictionary<string, Dictionary<string, IPayloadElement>>
                        {
                            // field_a is a fixed value, so does not appear in the state.
                            // field_b is set to a value
                            {
                                "__field:field_b", new Dictionary<string, IPayloadElement>
                                {
                                    {
                                        "irrelevant", new PlainTextInput
                                        {
                                            Value = "Field B Value"
                                        }
                                    }
                                }
                            },
                            // field_c is not specified, and will use the default value
                            {
                                "__field:field_c", new Dictionary<string, IPayloadElement>
                                {
                                    {"irrelevant", new PlainTextInput()}
                                }
                            },
                            // field_d (checkbox) is not checked
                            {
                                "__field:field_d", new Dictionary<string, IPayloadElement>
                                {
                                    {"irrelevant", new CheckboxGroup()},
                                }
                            },
                            // field_e (checkbox) is checked
                            {
                                "__field:field_e", new Dictionary<string, IPayloadElement>
                                {
                                    {
                                        "irrelevant", new CheckboxGroup
                                        {
                                            SelectedOptions = new[] {new CheckOption("Field E", "true")}
                                        }
                                    },
                                }
                            },
                            // field_f is set
                            {
                                "__field:field_f", new Dictionary<string, IPayloadElement>
                                {
                                    {
                                        "irrelevant", new StaticSelectMenu
                                        {
                                            SelectedOption = new Option("Option C", "option_c")
                                        }
                                    }
                                }
                            },
                            // field_g is not specified, and will use the default value
                            {
                                "__field:field_g", new Dictionary<string, IPayloadElement>
                                {
                                    {"irrelevant", new StaticSelectMenu()}
                                }
                            },
                            // field_h is set
                            {
                                "__field:field_h", new Dictionary<string, IPayloadElement>
                                {
                                    {"irrelevant", new MultiStaticSelectMenu
                                    {
                                        SelectedOptions = new Option[] { new("Option B", "option_b"), new("Option C", "option_c") }
                                    }}
                                }
                            },
                            // field_i is not specified, and will use the default value
                            {
                                "__field:field_i", new Dictionary<string, IPayloadElement>
                                {
                                    {"irrelevant", new MultiStaticSelectMenu()}
                                }
                            },
                            // field_j is a fixed value, so does not appear in the state.
                            // field_k is a fixed value, so does not appear in the state.
                        })
                },
            };

            // Ok, now we have a form and a payload, let's run the test
            var env = TestEnvironment.Create();
            var formEngine = env.Activate<FormEngine>();
            var results = formEngine.ProcessFormSubmission(payload, form, templateContext);

            Assert.Equal("Hello, World", results["field_a"]);
            Assert.Equal("Field B Value", results["field_b"]);
            Assert.Equal("Hello, World <@U000001> or <#C000001>", results["field_c"]);
            Assert.Equal("false", results["field_d"]);
            Assert.Equal("true", results["field_e"]);
            Assert.Equal("option_c", results["field_f"]);
            Assert.Equal("option_b", results["field_g"]);
            Assert.Equal(new[] { "option_b", "option_c" }, Assert.IsType<string[]>(results["field_h"]));
            Assert.Equal(new[] { "option_a", "option_b" }, Assert.IsType<string[]>(results["field_i"]));
            Assert.Equal(new[] { "Hello", "World" }, Assert.IsType<string[]>(results["field_j"]));
            Assert.Equal(new[] { "Hello", "World" }, Assert.IsType<string[]>(results["field_k"]));

            // And assert the results
            Assert.Equal(11, results.Count);
        }
    }
}
