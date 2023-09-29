using System.Collections.Generic;
using Newtonsoft.Json;
using NSubstitute;
using Serious;
using Serious.Abbot.Entities;
using Serious.Abbot.Messaging;
using Serious.Slack.BlockKit;
using Serious.Slack.Payloads;

public abstract class FormatterTestBase
{
    protected static IEnumerable<RichTextBlock> TestSlackBlocks()
    {
        yield return new RichTextBlock
        {
            Elements = new[]
            {
                new RichTextSection
                {
                    Elements = new[]
                    {
                        new TextElement
                        {
                            Text = "\n\n \n\n",
                        },
                        new TextElement
                        {
                            Text = "Something unstyled with <html>."
                        },
                        new TextElement
                        {
                            Text = "\nSomething bold.",
                            Style = new TextStyle()
                            {
                                Bold = true
                            }
                        },
                        new TextElement
                        {
                            Text = "\nSomething bold and italic.",
                            Style = new TextStyle()
                            {
                                Bold = true,
                                Italic = true
                            }
                        },
                        new TextElement
                        {
                            Text = "\nSomething italic and strike.",
                            Style = new TextStyle()
                            {
                                Strike = true,
                                Italic = true
                            }
                        },
                        new TextElement
                        {
                            Text = "\nSomething bold, italic, code, and strike with a <div>.",
                            Style = new TextStyle()
                            {
                                Strike = true,
                                Italic = true,
                                Bold = true,
                                Code = true
                            }
                        },
                        new TextElement
                        {
                            Text = "\n"
                        },
                        new LinkElement
                        {
                            Text = null, // Naked link
                            Url = "https://example.com"
                        },
                        new TextElement
                        {
                            Text = "\n"
                        },
                        new LinkElement
                        {
                            Text = null, // Naked styled link
                            Style = new TextStyle()
                            {
                                Bold = true,
                                Italic = true,
                            },
                            Url = "https://example.com"
                        },
                        new TextElement
                        {
                            Text = "\n"
                        },
                        new LinkElement
                        {
                            Text = "An unstyled link",
                            Url = "https://example.com"
                        },
                        new TextElement
                        {
                            Text = "\n"
                        },
                        new LinkElement
                        {
                            Text = "A bold link",
                            Style = new TextStyle()
                            {
                                Bold = true
                            },
                            Url = "https://example.com"
                        },
                    },
                },
            }
        };

    }

    protected static IEnumerable<RichTextBlock> TestSlackCodeAndBlockQuotes()
    {
        yield return new RichTextBlock
        {
            Elements = new[]
            {
                new RichTextSection
                {
                    Elements = new[]
                    {
                        new TextElement
                        {
                            Text = "Something bold.\n\n",
                            Style = new TextStyle()
                            {
                                Bold = true
                            }
                        },
                    },
                },
                new RichTextPreformatted()
                {
                    Elements = new[]
                    {
                        new TextElement
                        {
                            Text = "Style is ignored.",
                            Style = new TextStyle()
                            {
                                Italic = true,
                            },
                        }
                    }
                },
                new RichTextQuote()
                {
                    Elements = new[]
                    {
                        new TextElement
                        {
                            Text = "Style is allowed.",
                            Style = new TextStyle()
                            {
                                Bold = true,
                            },
                        }
                    }
                }
            }
        };
    }

    static void SetupMembers(Organization organization, ISlackResolver slackResolver)
    {
        slackResolver.ResolveMemberAsync("U03DYLAKR6U", organization, forceRefresh: true)
            .Returns(
                new Member { User = new() { DisplayName = "Submarine" } },
                new Member { User = new() { DisplayName = "Cache Me If You Can" } });
        slackResolver.ResolveRoomAsync("C3PO", organization, forceRefresh: true)
            .Returns(
                new Room { Name = "Cantina" },
                new Room { Name = "Cache Me If You Can" });
    }

    protected static IEnumerable<RichTextBlock> TestSlackMentions(Organization org, ISlackResolver slackResolver)
    {
        SetupMembers(org, slackResolver);

        yield return new RichTextBlock
        {
            Elements = new[]
            {
                new RichTextSection
                {
                    Elements = new StyledElement[]
                    {
                        new UserMention
                        {
                            UserId = "UB40",
                        },
                        new UserMention
                        {
                            UserId = "UB40",
                            Style = new()
                            {
                                Bold = true
                            },
                        },
                        new UserMention
                        {
                            UserId = "U03DYLAKR6U",
                        },
                        new ChannelMention
                        {
                            ChannelId = "R5D4",
                        },
                        new ChannelMention
                        {
                            ChannelId = "R5D4",
                            Style = new()
                            {
                                Bold = true,
                                Italic = true
                            },
                        },
                        new ChannelMention
                        {
                            ChannelId = "C3PO",
                        },
                        new UserGroupMention
                        {
                            UserGroupId = "S3R10U5",
                        },
                        new UserGroupMention
                        {
                            UserGroupId = "S3R10U5",
                            Style = new()
                            {
                                Italic = true
                            },
                        },
                        // Second mention should avoid repeated lookup
                        new UserMention
                        {
                            UserId = "U03DYLAKR6U",
                        },
                        new ChannelMention
                        {
                            ChannelId = "C3PO",
                        },
                    },
                },
            }
        };
    }

    protected static IEnumerable<RichTextBlock> TestSlackListItemBlocks(Organization org, ISlackResolver slackResolver)
    {
        SetupMembers(org, slackResolver);
        var json = """
        {
          "type": "rich_text",
          "block_id": "jpL8m",
          "elements": [
            {
              "type": "rich_text_section",
              "elements": [
                {
                  "type": "text",
                  "text": "Here's some bullet points\n\n"
                }
              ]
            },
            {
              "type": "rich_text_list",
              "elements": [
                {
                  "type": "rich_text_section",
                  "elements": [
                    {
                      "type": "text",
                      "text": "First item"
                    }
                  ]
                },
                {
                  "type": "rich_text_section",
                  "elements": [
                    {
                      "type": "text",
                      "text": "Mention "
                    },
                    {
                      "type": "user",
                      "user_id": "U03DYLAKR6U"
                    },
                    {
                      "type": "text",
                      "text": " and other things"
                    }
                  ]
                },
                {
                  "type": "rich_text_section",
                  "elements": [
                    {
                      "type": "text",
                      "text": "Third item"
                    }
                  ]
                }
              ],
              "style": "bullet",
              "indent": 0,
              "border": 0
            },
            {
              "type": "rich_text_section",
              "elements": [
                {
                  "type": "text",
                  "text": "\nAnd an ordered list\n\n"
                }
              ]
            },
            {
              "type": "rich_text_list",
              "elements": [
                {
                  "type": "rich_text_section",
                  "elements": [
                    {
                      "type": "text",
                      "text": "Item 1"
                    }
                  ]
                },
                {
                  "type": "rich_text_section",
                  "elements": [
                    {
                      "type": "text",
                      "text": "Item 2"
                    }
                  ]
                },
                {
                  "type": "rich_text_section",
                  "elements": [
                    {
                      "type": "text",
                      "text": "Item 3"
                    }
                  ]
                }
              ],
              "style": "ordered",
              "indent": 0,
              "border": 0
            }
          ]
        }
        """;
        yield return JsonConvert.DeserializeObject<RichTextBlock>(json).Require();
    }

    protected static IEnumerable<RichTextBlock> TestSlackListItemWithIndentationBlocks()
    {
        var json = """
        {
          "type": "rich_text",
          "block_id": "jpL8m",
          "elements": [
            {
              "type": "rich_text_list",
              "elements": [
                {
                  "type": "rich_text_section",
                  "elements": [
                    {
                      "type": "text",
                      "text": "One"
                    }
                  ]
                },
                {
                  "type": "rich_text_section",
                  "elements": [
                    {
                      "type": "text",
                      "text": "Two"
                    }
                  ]
                }
              ],
              "style": "bullet",
              "indent": 0,
              "border": 0
            },
            {
              "type": "rich_text_list",
              "elements": [
                {
                  "type": "rich_text_section",
                  "elements": [
                    {
                      "type": "text",
                      "text": "A"
                    }
                  ]
                },
                {
                  "type": "rich_text_section",
                  "elements": [
                    {
                      "type": "text",
                      "text": "B"
                    }
                  ]
                }
              ],
              "style": "ordered",
              "indent": 2,
              "border": 0
            },
            {
              "type": "rich_text_list",
              "elements": [
                {
                  "type": "rich_text_section",
                  "elements": [
                    {
                      "type": "text",
                      "text": "1"
                    }
                  ]
                },
                {
                  "type": "rich_text_section",
                  "elements": [
                    {
                      "type": "text",
                      "text": "2"
                    }
                  ]
                }
              ],
              "style": "bullet",
              "indent": 1,
              "border": 0
            },
            {
              "type": "rich_text_list",
              "elements": [
                {
                  "type": "rich_text_section",
                  "elements": [
                    {
                      "type": "text",
                      "text": "Uno"
                    }
                  ]
                },
                {
                  "type": "rich_text_section",
                  "elements": [
                    {
                      "type": "text",
                      "text": "Dos"
                    }
                  ]
                }
              ],
              "style": "bullet",
              "indent": 3,
              "border": 0
            },
            {
              "type": "rich_text_list",
              "elements": [
                {
                  "type": "rich_text_section",
                  "elements": [
                    {
                      "type": "text",
                      "text": "Item One"
                    }
                  ]
                },
                {
                  "type": "rich_text_section",
                  "elements": [
                    {
                      "type": "text",
                      "text": "Item Two"
                    }
                  ]
                }
              ],
              "style": "bullet",
              "indent": 4,
              "border": 0
            }
          ]
        }
        """;
        yield return JsonConvert.DeserializeObject<RichTextBlock>(json).Require();
    }

    protected static IEnumerable<RichTextBlock> TestSlackListItemWithBorder()
    {
        var json = """
        {
          "type": "rich_text",
          "block_id": "jpL8m",
          "elements": [
            {
              "type": "rich_text_list",
              "elements": [
                {
                  "type": "rich_text_section",
                  "elements": [
                    {
                      "type": "text",
                      "text": "One"
                    }
                  ]
                },
                {
                  "type": "rich_text_section",
                  "elements": [
                    {
                      "type": "text",
                      "text": "Two"
                    }
                  ]
                }
              ],
              "style": "bullet",
              "indent": 0,
              "border": 1
            },
            {
              "type": "rich_text_list",
              "elements": [
                {
                  "type": "rich_text_section",
                  "elements": [
                    {
                      "type": "text",
                      "text": "A"
                    }
                  ]
                },
                {
                  "type": "rich_text_section",
                  "elements": [
                    {
                      "type": "text",
                      "text": "B"
                    }
                  ]
                }
              ],
              "style": "ordered",
              "indent": 2,
              "border": 1
            }
          ]
        }
        """;
        yield return JsonConvert.DeserializeObject<RichTextBlock>(json).Require();
    }
}
