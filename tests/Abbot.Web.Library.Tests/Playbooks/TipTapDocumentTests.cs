using Serious.Abbot.Playbooks;
using Serious.Abbot.Serialization;

public class TipTapDocumentTests
{
    [UsesVerify]
    public class DeserializationTests
    {
        [Fact]
        public async Task CanBeDeserialized()
        {
            const string json = """
            {
              "type": "doc",
              "content": [
                {
                  "type": "paragraph",
                  "content": [
                    {
                      "type": "handlebars",
                      "attrs": {
                        "id": "trigger.outputs.channel.name",
                        "label": "Channel name from triggers"
                      }
                    },
                    {
                      "text": "This is a ",
                      "type": "text"
                    },
                    {
                      "text": "formatted",
                      "type": "text",
                      "marks": [
                        {
                          "type": "link",
                          "attrs": {
                            "href": "https://ab.bot/",
                            "target": "_blank"
                          }
                        },
                        {
                          "type": "bold"
                        }
                      ]
                    },
                    {
                      "text": " message",
                      "type": "text",
                      "marks": [
                        {
                          "type": "bold"
                        }
                      ]
                    },
                    {
                      "text": ".",
                      "type": "text"
                    }
                  ]
                }
              ]
            }
            """;
            var jObject = AbbotJsonFormat.Default.Deserialize(json);

            var result = AbbotJsonFormat.Default.Convert<TipTapDocument>(jObject);

            await Verify(result);
        }

        [Fact]
        public async Task CanDeserializeMentions()
        {
            const string json = """
            {
              "type": "doc",
              "content": [
                {
                  "type": "paragraph",
                  "content": [
                    {
                      "attrs": {
                        "id": "smile",
                        "label": "😀"
                      },
                      "type": "emoji"
                    },
                    {
                      "text": " ",
                      "type": "text"
                    },
                    {
                      "attrs": {
                        "id": "C05EZ8GFNCC",
                        "label": "Some Channel"
                      },
                      "type": "channel",
                    },
                    {
                      "text": " ",
                      "type": "text"
                    },
                    {
                      "attrs": {
                        "id": "U02EMN2AYGH",
                        "label": "@A friendly user"
                      },
                      "type": "mention",
                    },
                  ]
                }
              ]
            }
            """;
            var jObject = AbbotJsonFormat.Default.Deserialize(json);

            var result = AbbotJsonFormat.Default.Convert<TipTapDocument>(jObject);

            await Verify(result);
        }

        [Fact]
        public async Task CanDeserializeMarkedExpressions()
        {
            const string json = """
            {
              "type": "doc",
              "content": [
                {
                  "type": "paragraph",
                  "content": [
                    {
                      "type": "text",
                      "text": "Unbold "
                    },
                    {
                      "type": "text",
                      "marks": [
                        {
                          "type": "bold"
                        }
                      ],
                      "text": "bold "
                    },
                    {
                      "type": "mention",
                      "attrs": {
                        "id": "U03DYLAKR6U",
                        "label": "dahlbyk"
                      },
                      "marks": [
                        {
                          "type": "bold"
                        }
                      ]
                    },
                    {
                      "type": "text",
                      "marks": [
                        {
                          "type": "bold"
                        }
                      ],
                      "text": " "
                    },
                    {
                      "type": "text",
                      "marks": [
                        {
                          "type": "bold"
                        },
                        {
                          "type": "italic"
                        }
                      ],
                      "text": "in "
                    },
                    {
                      "type": "channel",
                      "attrs": {
                        "id": "C03EJAGQY0L",
                        "label": "dahlbyk-playground"
                      },
                      "marks": [
                        {
                          "type": "bold"
                        },
                        {
                          "type": "italic"
                        }
                      ]
                    },
                    {
                      "type": "text",
                      "marks": [
                        {
                          "type": "bold"
                        },
                        {
                          "type": "italic"
                        }
                      ],
                      "text": " with"
                    },
                    {
                      "type": "text",
                      "marks": [
                        {
                          "type": "bold"
                        }
                      ],
                      "text": " "
                    },
                    {
                      "type": "text",
                      "marks": [
                        {
                          "type": "link",
                          "attrs": {
                            "href": "https://en.wikipedia.org/wiki/Weapon",
                            "target": "_blank",
                            "class": null
                          }
                        },
                        {
                          "type": "bold"
                        }
                      ],
                      "text": "the "
                    },
                    {
                      "type": "handlebars",
                      "attrs": {
                        "id": "outputs.weapon",
                        "label": "outputs.weapon"
                      },
                      "marks": [
                        {
                          "type": "link",
                          "attrs": {
                            "href": "https://en.wikipedia.org/wiki/Weapon",
                            "target": "_blank",
                            "class": null
                          }
                        },
                        {
                          "type": "bold"
                        }
                      ]
                    },
                    {
                      "type": "text",
                      "marks": [
                        {
                          "type": "bold"
                        }
                      ],
                      "text": " still bold"
                    },
                    {
                      "type": "text",
                      "text": " unbold."
                    }
                  ]
                }
              ]
            }
            """;
            var jObject = AbbotJsonFormat.Default.Deserialize(json);

            var result = AbbotJsonFormat.Default.Convert<TipTapDocument>(jObject);

            await Verify(result);
        }

        [Fact]
        public async Task CanDeserializeBulletList()
        {
            const string json = """
            {
              "type": "doc",
              "content": [
                {
                  "type": "bulletList",
                  "content": [
                    {
                      "type": "listItem",
                      "content": [
                        {
                          "type": "paragraph",
                          "content": [
                            {
                              "type": "text",
                              "text": "item one"
                            }
                          ]
                        },
                        {
                          "type": "paragraph",
                          "content": [
                            {
                              "type": "text",
                              "text": "BY DEFINITION, LIST ITEMS ONLY HAVE 1 PARAGRAPH SO THIS SHOULD BE IGNORED."
                            }
                          ]
                        }
                      ]
                    },
                    {
                      "type": "listItem",
                      "content": [
                        {
                          "type": "paragraph",
                          "content": [
                            {
                              "type": "text",
                              "text": "item "
                            },
                            {
                              "type": "text",
                              "marks": [
                                {
                                  "type": "bold"
                                }
                              ],
                              "text": "two"
                            }
                          ]
                        }
                      ]
                    },
                    {
                      "type": "listItem",
                      "content": [
                        {
                          "type": "paragraph",
                          "content": [
                            {
                              "type": "text",
                              "text": "item three."
                            }
                          ]
                        }
                      ]
                    }
                  ]
                }
              ]
            }
            """;
            var jObject = AbbotJsonFormat.Default.Deserialize(json);

            var result = AbbotJsonFormat.Default.Convert<TipTapDocument>(jObject);

            await Verify(result);
        }

        [Fact]
        public async Task CanDeserializeBlockQuote()
        {
            const string json = """
            {
              "type": "doc",
              "content": [
                {
                  "type": "blockquote",
                  "content": [
                    {
                      "type": "paragraph",
                      "content": [
                        {
                          "type": "text",
                          "text": "Paragraph 1"
                        }
                      ]
                    },
                    {
                      "type": "bulletList",
                      "content": [
                        {
                          "type": "listItem",
                          "content": [
                            {
                              "type": "paragraph",
                              "content": [
                                {
                                  "type": "text",
                                  "text": "item one"
                                }
                              ]
                            },
                            {
                              "type": "paragraph",
                              "content": [
                                {
                                  "type": "text",
                                  "text": "BY DEFINITION, LIST ITEMS ONLY HAVE 1 PARAGRAPH SO THIS SHOULD BE IGNORED."
                                }
                              ]
                            }
                          ]
                        }
                     ]
                    },
                    {
                      "type": "paragraph",
                      "content": [
                        {
                          "type": "text",
                          "text": "Paragraph 1"
                        }
                      ]
                    },
                  ]
                }
              ]
            }
            """;
            var jObject = AbbotJsonFormat.Default.Deserialize(json);

            var result = AbbotJsonFormat.Default.Convert<TipTapDocument>(jObject);

            await Verify(result);
        }

        [Fact]
        public async Task CanDeserializeNestedBlockQuote()
        {
            const string json = """
            {
              "type": "doc",
              "content": [
                {
                  "type": "blockquote",
                  "content": [
                    {
                      "type": "blockquote",
                      "content": [
                        {
                          "type": "paragraph",
                          "content": [
                            {
                              "type": "text",
                              "text": "Paragraph 1"
                            }
                          ]
                        },

                      ]
                    }
                  ]
                }
              ]
            }
            """;
            var jObject = AbbotJsonFormat.Default.Deserialize(json);

            var result = AbbotJsonFormat.Default.Convert<TipTapDocument>(jObject);

            await Verify(result);
        }

        [Fact]
        public async Task CanDeserializeCodeBlock()
        {
            const string json = """
            {
              "type": "doc",
              "content": [
                {
                  "type": "codeBlock",
                  "attrs": {},
                  "content": [
                    {
                      "type": "text",
                      "text": "public void ThisMethod() {\n  SomeCode();\n}"
                    }
                  ]
                },
                {
                  "type": "blockquote",
                  "content": [
                    {
                      "type": "codeBlock",
                      "attrs": {},
                      "content": [
                        {
                          "type": "text",
                          "text": "public void ThatMethod() {\n  MoreCode();\n}"
                        }
                      ]
                    }
                  ]
                }
              ]
            }
            """;
            var jObject = AbbotJsonFormat.Default.Deserialize(json);

            var result = AbbotJsonFormat.Default.Convert<TipTapDocument>(jObject);

            await Verify(result);
        }
    }
}
