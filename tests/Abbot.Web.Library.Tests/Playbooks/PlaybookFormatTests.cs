using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serious.Abbot.Playbooks;

namespace Abbot.Web.Library.Tests.Playbooks;

[UsesVerify]
public class PlaybookFormatTests
{
    [Fact]
    public void SerializePlaybook()
    {
        const string expected = """
            {
              "triggers": [
                {
                  "id": "t_01",
                  "type": "trigger:slack.channel_created",
                  "inputs": {}
                },
                {
                  "id": "t_02",
                  "type": "trigger:http.webhook",
                  "inputs": {
                    "include_payload": true
                  }
                }
              ],
              "dispatch": {
                "type": "ByCustomer",
                "customerSegments": []
              },
              "startSequence": "seq_02",
              "sequences": {
                "seq_01": {
                  "actions": [
                    {
                      "branches": {
                        "foo": "bar"
                      },
                      "id": "act_01_01",
                      "type": "action:slack.post_message",
                      "inputs": {
                        "message": "Hello {{customer.name}}!",
                        "channel": "C1234"
                      }
                    },
                    {
                      "branches": {},
                      "id": "act_01_02",
                      "type": "action:system.wait",
                      "inputs": {
                        "seconds": 42,
                        "milliseconds": 42.4
                      }
                    }
                  ]
                },
                "seq_02": {
                  "actions": [
                    {
                      "branches": {},
                      "id": "act_02_01",
                      "type": "action:abbot.create_customer",
                      "inputs": {}
                    }
                  ]
                }
              }
            }
            """;

        var playbook = new PlaybookDefinition()
        {
            Triggers =
            {
                new ("t_01", "trigger:slack.channel_created"),
                new ("t_02", "trigger:http.webhook")
                {
                    Inputs =
                    {
                        ["include_payload"] = true,
                    },
                },
            },
            StartSequence = "seq_02",
            Dispatch = new()
            {
                Type = DispatchType.ByCustomer,
            },
            Sequences =
            {
                ["seq_01"] = new ActionSequence()
                {
                    Actions =
                    {
                        new ("act_01_01", "action:slack.post_message")
                        {
                            Inputs =
                            {
                                ["message"] = "Hello {{customer.name}}!",
                                ["channel"] = "C1234",
                            },
                            Branches =
                            {
                                ["foo"] = "bar",
                            }
                        },
                        new ("act_01_02", "action:system.wait")
                        {
                            Inputs =
                            {
                                ["seconds"] = 42,
                                ["milliseconds"] = 42.4,
                            },
                        },
                    },
                },
                ["seq_02"] = new ActionSequence()
                {
                    Actions =
                    {
                        new ("act_02_01", "action:abbot.create_customer"),
                    },
                },
            },
        };

        var actual = PlaybookFormat.Serialize(playbook);

        // Reformat with JSON.NET for easier comparison
        actual = JToken.Parse(actual).ToString(Formatting.Indented);

        Assert.Equal(expected.ReplaceLineEndings(), actual.ReplaceLineEndings());
    }

    [Fact]
    public async Task DeserializePlaybook()
    {
        const string serialized = """
            {
              "triggers": [
                {
                  "id": "t_01",
                  "type": "trigger:slack.channel_created",
                  "inputs": {}
                },
                {
                  "id": "t_02",
                  "type": "trigger:http.webhook",
                  "inputs": {
                    "include_payload": true
                  }
                }
              ],
              "dispatch": {
                "type": "ByCustomer"
              },
              "startSequence": "seq_02",
              "sequences": {
                "seq_01": {
                  "actions": [
                    {
                      "id": "act_01_01",
                      "type": "action:slack.post_message",
                      "inputs": {
                        "message": "Hello {{customer.name}}!",
                        "channel": "C1234"
                      },
                      "branches": {
                        "foo": "bar"
                      }
                    },
                    {
                      "id": "act_01_02",
                      "type": "action:system.wait",
                      "inputs": {
                        "seconds": 42,
                        "milliseconds": 42.4
                      },
                      "branches": {},
                    }
                  ]
                },
                "seq_02": {
                  "actions": [
                    {
                      "id": "act_02_01",
                      "type": "action:abbot.create_customer",
                      "inputs": {},
                      "branches": {},
                    }
                  ]
                }
              }
            }
            """;

        var actual = PlaybookFormat.Deserialize(serialized);

        // Confirm the types of some inputs when parsed from JSON
        Assert.IsType<bool>(actual.Triggers[1].Inputs["include_payload"]);
        Assert.IsType<string>(actual.Sequences["seq_01"].Actions[0].Inputs["message"]);
        Assert.IsType<string>(actual.Sequences["seq_01"].Actions[0].Inputs["channel"]);
        Assert.IsType<long>(actual.Sequences["seq_01"].Actions[1].Inputs["seconds"]);
        Assert.IsType<double>(actual.Sequences["seq_01"].Actions[1].Inputs["milliseconds"]);

        await Verify(actual);
    }

    [UsesVerify]
    public class TheValidateMethod
    {
        record TestObject;

        [Fact]
        public async Task ProducesExpectedValidationErrors()
        {
            var playbook = new PlaybookDefinition()
            {
                Triggers =
                {
                    new("unique_id_1", "test"),
                    new("shared_id_2", "test"),
                },
                StartSequence = "non_existent_sequence",
                Sequences =
                {
                    ["seq:a1"] = new()
                    {
                        Actions =
                        {
                            new("unique_id_2", "test"),
                            new("shared_id_1", "test"),
                        }
                    },
                    ["seq:a2"] = new()
                    {
                        Actions =
                        {
                            new("shared_id_1", "test"),
                            new("shared_id_2", "test"),
                            new ("bad_inputs", "test")
                            {
                                Inputs =
                                {
                                    ["bad input name"] = "",
                                },
                                Branches =
                                {
                                    ["bad id"] = "ok",
                                    ["ok"] = "bad sequence",
                                }
                            },
                            new ("bad_type", "what even are types"),
                            new ("bad id", "test"),
                        },
                    },
                    ["bad sequence"] = new(),
                }
            };

            var errors = PlaybookFormat.Validate(playbook);

            await Verify(errors.Select(e => e.Message).ToArray());
        }
    }
}
