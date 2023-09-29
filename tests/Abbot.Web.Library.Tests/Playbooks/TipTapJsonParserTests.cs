using Abbot.Common.TestHelpers.Fakes;
using Serious.Abbot.Playbooks;
using Serious.Abbot.Serialization;

public class TipTapJsonParserTests
{
    public class TheParseMethod
    {
        [Fact]
        public async Task HandlesLeadingSpacesAndMarks()
        {
            var document = new TipTapDocument(new TipTapNode[]
            {
                new TipTapParagraphNode(new TipTapNode[]
                {
                    new TipTapTextNode("Some text"),
                    new TipTapTextNode(" with bold", new [] { new TipTapMarkNode("bold") }),
                    new TipTapTextNode(" and italic", new [] { new TipTapMarkNode("bold"), new TipTapMarkNode("italic") }),
                    new TipTapTextNode(" minus italic", new [] { new TipTapMarkNode("bold") }),
                    new TipTapTextNode(" then not bold"),
                })
            });
            var parser = new TipTapJsonParser();

            var result = parser.Parse(document, new FakeTemplateEvaluator());

            Assert.Equal("Some text *with bold _and italic_ minus italic* then not bold", result);
        }

        [Fact]
        public async Task HandlesTrailingSpacesAndMarks()
        {
            var document = new TipTapDocument(new TipTapNode[]
            {
                new TipTapParagraphNode(new TipTapNode[]
                {
                    new TipTapTextNode("Some text"),
                    new TipTapTextNode("  with bold ", new [] { new TipTapMarkNode("bold") }),
                    new TipTapTextNode("then not bold"),
                })
            });
            var parser = new TipTapJsonParser();

            var result = parser.Parse(document, new FakeTemplateEvaluator());

            Assert.Equal("Some text  *with bold* then not bold", result);
        }

        [Fact]
        public async Task HandlesSpacesWithMultipleMarks()
        {
            var document = new TipTapDocument(new TipTapNode[]
            {
                new TipTapParagraphNode(new TipTapNode[]
                {
                    new TipTapTextNode("An intro"),
                    new TipTapTextNode(" with bold ", new [] { new TipTapMarkNode("bold") }),
                    new TipTapTextNode("and italic", new [] { new TipTapMarkNode("bold"), new TipTapMarkNode("italic") }),
                })
            });
            var parser = new TipTapJsonParser();

            var result = parser.Parse(document, new FakeTemplateEvaluator());

            Assert.Equal("An intro *with bold _and italic_*", result);
        }

        [Fact]
        public async Task ReturnsRenderedMrkdwnForTipTapDocument()
        {
            var document = new TipTapDocument(new TipTapNode[]
            {
                new TipTapParagraphNode(new TipTapNode[]
                {
                    new TipTapHandlebarsNode(new TipTapAttributes("trigger.outputs.intro", "An intro")),
                    new TipTapTextNode("with bold ", new [] { new TipTapMarkNode("bold") }),
                    new TipTapTextNode("and italic", new [] { new TipTapMarkNode("bold"), new TipTapMarkNode("italic") }),
                }),
                new TipTapParagraphNode(new []
                {
                    new TipTapTextNode("And "),
                    new TipTapTextNode("a link", new [] { new TipTapLinkMark(new TipTapLinkAttributes("https://ab.bot")) }),
                    new TipTapTextNode(" next to a different link", new [] { new TipTapLinkMark(new TipTapLinkAttributes("https://app.ab.bot")) }),
                    new TipTapTextNode("."),
                }),
                new TipTapParagraphNode(new TipTapNode[]
                {
                    new TipTapEmojiNode(new TipTapAttributes("smile", "😀")),
                    new TipTapTextNode(" "),
                    new TipTapChannelMentionNode(new TipTapAttributes("{{outputs.channel.id}}", "some channel")),
                    new TipTapTextNode(" "),
                    new TipTapUserMentionNode(new TipTapAttributes("U02EMN2AYGH", "@donokuda")),
                }),
                new TipTapBulletListNode
                {
                    Type = "bulletList",
                    Content = new List<TipTapListItemNode>
                    {
                        new()
                        {
                            Content = new List<TipTapParagraphNode>
                            {
                                new()
                                {
                                    Content = new List<TipTapNode>
                                    {
                                        new TipTapTextNode("some text "),
                                        new TipTapTextNode("with bold", new [] { new TipTapMarkNode("bold") }),
                                    }
                                }
                            }
                        },
                        new()
                        {
                            Content = new List<TipTapParagraphNode>
                            {
                                new()
                                {
                                    Content = new List<TipTapNode>
                                    {
                                        new TipTapTextNode(" italic ", new [] { new TipTapMarkNode("italic") }),
                                        new TipTapTextNode("and bold ", new [] { new TipTapMarkNode("italic"), new TipTapMarkNode("bold") }),
                                        new TipTapTextNode("item 2"),
                                    }
                                }
                            }
                        }
                    }
                },
                new TipTapOrderedListNode
                {
                    Type = "orderedList",
                    Content = new List<TipTapListItemNode>
                    {
                        new()
                        {
                            Content = new List<TipTapParagraphNode>
                            {
                                new()
                                {
                                    Content = new List<TipTapNode>
                                    {
                                        new TipTapTextNode("Another item 1"),
                                    }
                                }
                            }
                        },
                        new()
                        {
                            Content = new List<TipTapParagraphNode>
                            {
                                new()
                                {
                                    Content = new List<TipTapNode>
                                    {
                                        new TipTapTextNode("Another item 2 "),
                                    }
                                }
                            }
                        }
                    }
                }
            });

            var evaluator = new FakeTemplateEvaluator
            {
                ["{{trigger.outputs.intro}}"] = "Some text ",
                ["{{outputs.channel.id}}"] = "C05EZ8GFNCC",
            };
            var parser = new TipTapJsonParser();

            var result = parser.Parse(document, evaluator);

            Assert.Equal("""
                        Some text *with bold _and italic_*
                        And <https://ab.bot|a link> <https://app.ab.bot|next to a different link>.
                        :smile: <#C05EZ8GFNCC> <@U02EMN2AYGH>
                        * some text *with bold*
                        *  _italic *and bold*_ item 2
                        1. Another item 1
                        2. Another item 2
                        """, result);
        }

        [Fact]
        public async Task ReturnsMarkedExpressions()
        {
            var json = """
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
                    },
                    {
                      "type": "paragraph",
                      "content": [
                        {
                          "type": "text",
                          "text": "One big link: "
                        },
                        {
                          "type": "text",
                          "marks": [
                            {
                              "type": "link",
                              "attrs": {
                                "href": "https://example.com/",
                                "target": "_blank",
                                "class": null
                              }
                            }
                          ],
                          "text": "Text"
                        },
                        {
                          "type": "emoji",
                          "attrs": {
                            "id": "exclamation",
                            "label": "❗"
                          }
                        },
                        {
                          "type": "text",
                          "text": " "
                        },
                        {
                          "type": "mention",
                          "attrs": {
                            "id": "U03DYLAKR6U",
                            "label": "dahlbyk"
                          },
                          "marks": [
                            {
                              "type": "link",
                              "attrs": {
                                "href": "https://example.com/",
                                "target": "_blank",
                                "class": null
                              }
                            }
                          ]
                        },
                        {
                          "type": "text",
                          "marks": [
                            {
                              "type": "link",
                              "attrs": {
                                "href": "https://example.com/",
                                "target": "_blank",
                                "class": null
                              }
                            }
                          ],
                          "text": " is in "
                        },
                        {
                          "type": "channel",
                          "attrs": {
                            "id": "C03EJAGQY0L",
                            "label": "dahlbyk-playground"
                          },
                          "marks": [
                            {
                              "type": "link",
                              "attrs": {
                                "href": "https://example.com/",
                                "target": "_blank",
                                "class": null
                              }
                            }
                          ]
                        },
                        {
                          "type": "text",
                          "marks": [
                            {
                              "type": "link",
                              "attrs": {
                                "href": "https://example.com/",
                                "target": "_blank",
                                "class": null
                              }
                            }
                          ],
                          "text": " "
                        },
                        {
                          "type": "handlebars",
                          "attrs": {
                            "id": "outputs.example",
                            "label": "outputs.example"
                          },
                          "marks": [
                            {
                              "type": "link",
                              "attrs": {
                                "href": "https://example.com/",
                                "target": "_blank",
                                "class": null
                              }
                            }
                          ]
                        },
                        {
                          "type": "text",
                          "marks": [
                            {
                              "type": "link",
                              "attrs": {
                                "href": "https://example.com/",
                                "target": "_blank",
                                "class": null
                              }
                            }
                          ],
                          "text": " text."
                        }
                      ]
                    }
                  ]
                }
                """;

            var document = AbbotJsonFormat.Default.Deserialize<TipTapDocument>(json).Require();

            var evaluator = new FakeTemplateEvaluator
            {
                ["{{outputs.weapon}}"] = "Candlestick",
                ["{{outputs.example}}"] = "e.g.",
            };

            var parser = new TipTapJsonParser();

            var result = parser.Parse(document, evaluator);

            Assert.Equal(
                """
                Unbold *bold <@U03DYLAKR6U> _in <#C03EJAGQY0L> with_ <https://en.wikipedia.org/wiki/Weapon|the Candlestick> still bold* unbold.
                One big link: <https://example.com/|Text>:exclamation: <@U03DYLAKR6U> <https://example.com/|is in> <#C03EJAGQY0L> <https://example.com/|e.g. text.>
                """, result);
        }

        [Fact]
        public async Task RendersBlockQuotes()
        {
            var document = new TipTapDocument(new TipTapNode[]
            {
                new TipTapParagraphNode(new TipTapNode[]
                {
                    new TipTapTextNode("A paragraph"),
                }),
                new TipTapBlockQuoteNode()
                {
                    Content = new List<TipTapNode>
                    {
                        new TipTapParagraphNode(new TipTapNode[]
                        {
                            new TipTapTextNode("Paragraph in a block quote")
                        }),
                        new TipTapBulletListNode
                        {
                            Type = "bulletList",
                            Content = new List<TipTapListItemNode>
                            {
                                new()
                                {
                                    Content = new List<TipTapParagraphNode>
                                    {
                                        new()
                                        {
                                            Content = new List<TipTapNode>
                                            {
                                                new TipTapTextNode("List Item in a block quote"),
                                            }
                                        }
                                    }
                                },
                                new()
                                {
                                    Content = new List<TipTapParagraphNode>
                                    {
                                        new()
                                        {
                                            Content = new List<TipTapNode>
                                            {
                                                new TipTapTextNode("Another list item in a block quote"),
                                            }
                                        }
                                    }
                                }
                            }
                        },
                    }
                },
                new TipTapParagraphNode(new TipTapNode[]
                {
                    new TipTapTextNode("More text"),
                }),
            });
            var parser = new TipTapJsonParser();

            var result = parser.Parse(document, new FakeTemplateEvaluator());

            Assert.Equal("""
                        A paragraph
                        > Paragraph in a block quote
                        > • List Item in a block quote
                        > • Another list item in a block quote
                        More text
                        """, result);
        }

        [Fact]
        public async Task RendersNestedBlockQuotes()
        {
            var document = new TipTapDocument(new TipTapNode[]
            {
                new TipTapBlockQuoteNode()
                {
                    Content = new List<TipTapNode>
                    {
                        new TipTapBlockQuoteNode
                        {
                            Content = new []
                            {
                                new TipTapParagraphNode
                                {
                                    Content = new []
                                    {
                                        new TipTapTextNode("Nested block quote")
                                    }
                                }
                            }
                        },
                    }
                },
            });
            var parser = new TipTapJsonParser();

            var result = parser.Parse(document, new FakeTemplateEvaluator());

            Assert.Equal("""
                        >> Nested block quote
                        """, result);
        }

        [Fact]
        public async Task RendersCodeBlock()
        {
            var document = new TipTapDocument(new TipTapNode[]
            {
                new TipTapCodeBlockNode
                {
                    Content = new[]
                    {
                        new TipTapTextNode("public void Method1() {\\n...\\n}"),
                    }
                },
                new TipTapBlockQuoteNode
                {
                    Content = new List<TipTapNode>
                    {
                        new TipTapCodeBlockNode
                        {
                            Content = new[]
                            {
                                new TipTapTextNode("public void Method2() {\\n...\\n}"),
                            }
                        },
                    },
                },
            });
            var parser = new TipTapJsonParser();

            var result = parser.Parse(document, new FakeTemplateEvaluator());

            Assert.Equal("""
                        ```public void Method1() {
                        ...
                        }```
                        > ```public void Method2() {
                        ...
                        }```
                        """, result);
        }
    }
}
