using System;
using System.Net.Http;
using System.Threading.Tasks;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Serious.Slack;
using Serious.Slack.InteractiveMessages;
using Xunit;

public class ApiExtensionsTests
{
    public class TheGetMessageAsyncMethod
    {
        [Fact]
        public async Task ReturnsMessageIdentifiedById()
        {
            var client = Substitute.For<IConversationsApiClient>();
            client.GetConversationHistoryAsync("apitoken", "C012345", "12345678.901234", limit: 1, inclusive: true)
                .Returns(new ConversationHistoryResponse
                {
                    Ok = true,
                    Body = new[] {
                        new SlackMessage
                        {
                            Text = "This is the message you are looking for",
                            Timestamp = "12345678.901234"
                        }
                    }
                });

            var message = await client.GetConversationAsync("apitoken", "C012345", "12345678.901234");

            Assert.NotNull(message);
            Assert.Equal("This is the message you are looking for", message.Text);
        }

        [Fact]
        public async Task ReturnsNullIfMessageDoesNotMatchTimestamp()
        {
            var client = Substitute.For<IConversationsApiClient>();
            client.GetConversationHistoryAsync("apitoken", "C012345", "12345678.901234", limit: 1, inclusive: true)
                .Returns(new ConversationHistoryResponse
                {
                    Ok = true,
                    Body = new[] {
                        new SlackMessage
                        {
                            Text = "This is NOT the message you are looking for",
                            Timestamp = "12345679.001234"
                        }
                    }
                });
            client.GetConversationRepliesAsync("apitoken", "C012345", "12345678.901234", limit: 1, inclusive: true)
                .Returns(new ConversationHistoryResponse
                {
                    Ok = true,
                    Body = new[] {
                        new SlackMessage
                        {
                            Text = "This is NOT the reply you are looking for",
                            Timestamp = "12345680.001234"
                        }
                    }
                });


            var message = await client.GetConversationAsync("apitoken", "C012345", "12345678.901234");

            Assert.Null(message);
        }

        [Fact]
        public async Task ReturnsReplyIdentifiedByIdWhenIncludeRepliesTrue()
        {
            var client = Substitute.For<IConversationsApiClient>();
            client.GetConversationHistoryAsync("apitoken", "C012345", "12345678.901234", limit: 1, inclusive: true)
                .Returns(new ConversationHistoryResponse
                {
                    Ok = true,
                    Body = new[] {
                        new SlackMessage
                        {
                            Text = "This is NOT the message you are looking for",
                            Timestamp = "12345679.000000"
                        }
                    }
                });
            client.GetConversationRepliesAsync("apitoken", "C012345", "12345678.901234", limit: 1, inclusive: true)
                .Returns(new ConversationHistoryResponse
                {
                    Ok = true,
                    Body = new[] {
                        new SlackMessage
                        {
                            Text = "This is the reply you are looking for",
                            Timestamp = "12345678.901234",
                            ThreadTimestamp = "12345679.000000"
                        }
                    }
                });

            var message = await client.GetConversationAsync("apitoken", "C012345", "12345678.901234");

            Assert.NotNull(message);
            Assert.Equal("This is the reply you are looking for", message.Text);
            Assert.Equal("12345678.901234", message.Timestamp);
            Assert.Equal("12345679.000000", message.ThreadTimestamp);
        }

        [Fact]
        public async Task ReturnsNullWhenNoMessageNorReplyFound()
        {
            var client = Substitute.For<IConversationsApiClient>();
            client.GetConversationHistoryAsync("apitoken", "C012345", "12345678.901234", limit: 1, inclusive: true)
                .Returns(new ConversationHistoryResponse
                {
                    Ok = true,
                    Body = new[] {
                        new SlackMessage
                        {
                            Text = "This is NOT the message you are looking for",
                            Timestamp = "12345679.000000"
                        }
                    }
                });
            client.GetConversationRepliesAsync("apitoken", "C012345", "12345678.901234", limit: 1, inclusive: true)
                .Returns(new ConversationHistoryResponse
                {
                    Ok = true,
                    Body = new[] {
                        new SlackMessage
                        {
                            Text = "This is NOT the reply you are looking for",
                            Timestamp = "12345679.000001",
                            ThreadTimestamp = "12345679.000000"
                        }
                    }
                });

            var message = await client.GetConversationAsync("apitoken", "C012345", "12345678.901234");

            Assert.Null(message);
        }
    }

    public class TheGetAllUsersConversationsAsyncMethod
    {
        [Fact]
        public async Task ReturnsSingleResponse()
        {
            var client = Substitute.For<ISlackApiClient>();
            client.GetUsersConversationsAsync(
                accessToken: "accessToken",
                limit: 1000,
                user: "U01234568",
                types: "public_channels",
                teamId: null,
                excludeArchived: true,
                cursor: null)
                .Returns(new ConversationsResponse
                {
                    Ok = true,
                    Body = new ConversationInfo[]
                    {
                        new() { Id="id1", Name = "one" },
                        new() { Id="id2", Name = "two" },
                    }
                });

            var response = await client.GetAllUsersConversationsAsync("accessToken",
                user: "U01234568",
                types: "public_channels",
                teamId: null,
                excludeArchived: true);

            Assert.True(response.Ok);
            Assert.Collection(response.Body,
                c => Assert.Equal("one", c.Name),
                c => Assert.Equal("two", c.Name));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task ReturnsMultipleResponses(string finalCursor)
        {
            var client = Substitute.For<ISlackApiClient>();
            client.GetUsersConversationsAsync(
                    accessToken: "accessToken",
                    limit: 1000,
                    user: "U01234568",
                    types: "public_channels",
                    teamId: null,
                    excludeArchived: true,
                    cursor: null)
                .Returns(new ConversationsResponse
                {
                    Ok = true,
                    Body = new ConversationInfo[]
                    {
                        new() { Id = "id1", Name = "one" },
                        new() { Id = "id2", Name = "two" },
                    },
                    ResponseMetadata = new ResponseMetadata
                    {
                        NextCursor = "a"
                    }
                });
            client.GetUsersConversationsAsync(
                    accessToken: "accessToken",
                    limit: 1000,
                    user: "U01234568",
                    types: "public_channels",
                    teamId: null,
                    excludeArchived: true,
                    cursor: "a")
                .Returns(new ConversationsResponse
                {
                    Ok = true,
                    Body = new ConversationInfo[]
                    {
                        new() { Id = "id1", Name = "three" },
                        new() { Id = "id2", Name = "four" },
                    },
                    ResponseMetadata = new ResponseMetadata
                    {
                        NextCursor = "b"
                    }
                });
            client.GetUsersConversationsAsync(
                    accessToken: "accessToken",
                    limit: 1000,
                    user: "U01234568",
                    types: "public_channels",
                    teamId: null,
                    excludeArchived: true,
                    cursor: "b")
                .Returns(new ConversationsResponse
                {
                    Ok = true,
                    Body = new ConversationInfo[]
                    {
                        new() {  Id = "id1", Name = "five" },
                        new() {  Id = "id2", Name = "six" },
                    },
                    ResponseMetadata = new ResponseMetadata
                    {
                        NextCursor = finalCursor
                    }
                });

            var response = await client.GetAllUsersConversationsAsync("accessToken",
                "U01234568",
                types: "public_channels",
                teamId: null,
                excludeArchived: true);

            Assert.True(response.Ok);
            Assert.Collection(response.Body,
                c => Assert.Equal("one", c.Name),
                c => Assert.Equal("two", c.Name),
                c => Assert.Equal("three", c.Name),
                c => Assert.Equal("four", c.Name),
                c => Assert.Equal("five", c.Name),
                c => Assert.Equal("six", c.Name));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ReturnsOkFalseIfErrorFound(bool responseOkButBodyNull)
        {
            var client = Substitute.For<ISlackApiClient>();
            client.GetUsersConversationsAsync(
                    accessToken: "accessToken",
                    limit: 1000,
                    user: "U01234568",
                    types: "public_channels",
                    teamId: null,
                    excludeArchived: true,
                    cursor: null)
                .Returns(new ConversationsResponse
                {
                    Ok = true,
                    Body = new ConversationInfo[]
                    {
                        new() {  Id = "id1", Name = "one" },
                        new() {  Id = "id2", Name = "two" },
                    },
                    ResponseMetadata = new ResponseMetadata
                    {
                        NextCursor = "a"
                    }
                });
            client.GetUsersConversationsAsync(
                    accessToken: "accessToken",
                    limit: 1000,
                    user: "U01234568",
                    types: "public_channels",
                    teamId: null,
                    excludeArchived: true,
                    cursor: "a")
                .Returns(new ConversationsResponse
                {
                    Ok = responseOkButBodyNull,
                    Body = responseOkButBodyNull ? null : new ConversationInfo[] { new() { Id = "id1", Name = "blah" } },
                    Error = "shit broke",
                    ResponseMetadata = new ResponseMetadata
                    {
                        NextCursor = "b"
                    }
                });
            client.GetUsersConversationsAsync(
                    accessToken: "accessToken",
                    limit: 1000,
                    user: "U01234568",
                    types: "public_channels",
                    teamId: null,
                    excludeArchived: true,
                    cursor: "b")
                .Returns(new ConversationsResponse
                {
                    Ok = true,
                    Body = new ConversationInfo[]
                    {
                        new() { Id = "id1", Name = "five" },
                        new() { Id = "id2", Name = "six" },
                    },
                    ResponseMetadata = new ResponseMetadata
                    {
                        NextCursor = null
                    }
                });

            var response = await client.GetAllUsersConversationsAsync("accessToken",
                "U01234568",
                types: "public_channels",
                teamId: null,
                excludeArchived: true);

            Assert.False(response.Ok);
            Assert.Equal("shit broke", response.Error);
            Assert.Null(response.Body);
        }
    }

    public class TheGetAllConversationMembersAsyncMethod
    {
        [Fact]
        public async Task ReturnsMultipleResponses()
        {
            var client = Substitute.For<IConversationsApiClient>();
            client.GetConversationMembersAsync(
                    accessToken: "accessToken",
                    channel: "C01234568",
                    limit: 1000,
                    cursor: null)
                .Returns(new ConversationMembersResponse
                {
                    Ok = true,
                    Body = new[]
                    {
                        "one",
                        "two",
                    },
                    ResponseMetadata = new ResponseMetadata
                    {
                        NextCursor = "a"
                    }
                });
            client.GetConversationMembersAsync(
                    accessToken: "accessToken",
                    channel: "C01234568",
                    limit: 1000,
                    cursor: "a")
                .Returns(new ConversationMembersResponse
                {
                    Ok = true,
                    Body = new[]
                    {
                        "three",
                        "four",
                    },
                    ResponseMetadata = new ResponseMetadata
                    {
                        NextCursor = "b"
                    }
                });
            client.GetConversationMembersAsync(
                    accessToken: "accessToken",
                    channel: "C01234568",
                    limit: 1000,
                    cursor: "b")
                .Returns(new ConversationMembersResponse
                {
                    Ok = true,
                    Body = new[]
                    {
                        "five",
                        "six",
                    },
                    ResponseMetadata = new ResponseMetadata
                    {
                        NextCursor = ""
                    }
                });

            var response = await client.GetAllConversationMembersAsync(
                "accessToken",
                channel: "C01234568");

            Assert.True(response.Ok);
            Assert.Collection(response.Body,
                c => Assert.Equal("one", c),
                c => Assert.Equal("two", c),
                c => Assert.Equal("three", c),
                c => Assert.Equal("four", c),
                c => Assert.Equal("five", c),
                c => Assert.Equal("six", c));
        }

        [Fact]
        public async Task RetriesRequestWithCursor()
        {
            // If the first request succeeded (and thus we know we should be getting data), we'll retry
            // any subsequent requests. We know it's a subsequent request by the presence of a cursor.
            var client = Substitute.For<IConversationsApiClient>();
            client.GetConversationMembersAsync(
                    accessToken: "accessToken",
                    channel: "C01234568",
                    limit: 1000,
                    cursor: null)
                .Returns(new ConversationMembersResponse
                {
                    Ok = true,
                    Body = new[]
                    {
                        "one",
                        "two",
                    },
                    ResponseMetadata = new ResponseMetadata
                    {
                        NextCursor = "a"
                    }
                });
            client.GetConversationMembersAsync(
                    accessToken: "accessToken",
                    channel: "C01234568",
                    limit: 1000,
                    cursor: "a")
                .Returns(
                    _ => throw new TimeoutException(),
                    _ => new ConversationMembersResponse
                    {
                        Ok = true,
                        Body = new[]
                        {
                            "three",
                            "four",
                        },
                        ResponseMetadata = new ResponseMetadata
                        {
                            NextCursor = "b"
                        }
                    });
            client.GetConversationMembersAsync(
                    accessToken: "accessToken",
                    channel: "C01234568",
                    limit: 1000,
                    cursor: "b")
                .Returns(new ConversationMembersResponse
                {
                    Ok = true,
                    Body = new[]
                    {
                        "five",
                        "six",
                    },
                    ResponseMetadata = new ResponseMetadata
                    {
                        NextCursor = ""
                    }
                });

            var response = await client.GetAllConversationMembersAsync(
                accessToken: "accessToken",
                channel: "C01234568");

            Assert.True(response.Ok);
            Assert.Collection(response.Body,
                c => Assert.Equal("one", c),
                c => Assert.Equal("two", c),
                c => Assert.Equal("three", c),
                c => Assert.Equal("four", c),
                c => Assert.Equal("five", c),
                c => Assert.Equal("six", c));
        }

        [Fact]
        public async Task ThrowsIfRetryFails()
        {
            var client = Substitute.For<IConversationsApiClient>();
            client.GetConversationMembersAsync(
                    accessToken: "accessToken",
                    channel: "C01234568",
                    limit: 1000,
                    cursor: null)
                .Returns(new ConversationMembersResponse
                {
                    Ok = true,
                    Body = new[]
                    {
                        "one",
                        "two",
                    },
                    ResponseMetadata = new ResponseMetadata
                    {
                        NextCursor = "a"
                    }
                });
            client.GetConversationMembersAsync(
                    accessToken: "accessToken",
                    channel: "C01234568",
                    limit: 1000,
                    cursor: "a")
                .Returns(
                    _ => throw new HttpRequestException(),
                    _ => throw new TimeoutException(),
                    _ => new ConversationMembersResponse
                    {
                        Ok = true,
                        Body = new[]
                        {
                            "three",
                            "four",
                        },
                        ResponseMetadata = new ResponseMetadata
                        {
                            NextCursor = "b"
                        }
                    });
            client.GetConversationMembersAsync(
                    accessToken: "accessToken",
                    channel: "C01234568",
                    limit: 1000,
                    cursor: "b")
                .Throws(new InvalidOperationException());

            await Assert.ThrowsAsync<TimeoutException>(() => client.GetAllConversationMembersAsync(
                "accessToken",
                channel: "C01234568"));
        }

        [Fact]
        public async Task ThrowsExceptionIfSameCursorSeen()
        {
            var client = Substitute.For<IConversationsApiClient>();
            client.GetConversationMembersAsync(
                    accessToken: "accessToken",
                    channel: "C01234568",
                    limit: 1000,
                    cursor: null)
                .Returns(new ConversationMembersResponse
                {
                    Ok = true,
                    Body = new[]
                    {
                        "one",
                        "two",
                    },
                    ResponseMetadata = new ResponseMetadata
                    {
                        NextCursor = "a"
                    }
                });
            client.GetConversationMembersAsync(
                    accessToken: "accessToken",
                    channel: "C01234568",
                    limit: 1000,
                    cursor: "a")
                .Returns(new ConversationMembersResponse
                {
                    Ok = true,
                    Body = new[]
                    {
                        "three",
                        "four",
                    },
                    ResponseMetadata = new ResponseMetadata
                    {
                        NextCursor = "b"
                    }
                });
            client.GetConversationMembersAsync(
                    accessToken: "accessToken",
                    channel: "C01234568",
                    limit: 1000,
                    cursor: "b")
                .Returns(new ConversationMembersResponse
                {
                    Ok = true,
                    Body = new[]
                    {
                        "five",
                        "six",
                    },
                    ResponseMetadata = new ResponseMetadata
                    {
                        NextCursor = "a"
                    }
                });

            await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetAllConversationMembersAsync(
                "accessToken",
                channel: "C01234568"));
        }
    }
}
