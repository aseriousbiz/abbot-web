# The life of a message

In order to work its magic, Abbot needs to process Slack messages.
This document describes the data Abbot receives and how we process that data.

## Receiving an event from Slack

First, Slack sends us an event payload.
For this example, we'll use the following sample payload:

```json
{
    "token": "XXYYZZ",
    "team_id": "T1",
    "api_app_id": "A1",
    "event": {
        "type": "message",
        "channel": "C1",
        "user": "U1",
        "text": "Hello, <@U2>",
        "ts": "1111111111.111111",
    },
    "type": "event_callback",
    "event_id": "Ev9",
    "event_time": 1234567890
}
```

Upon receiving this event, we validate that it actually came from Slack using the `X-Slack-Signature` header.
Then, we enqueue it in the `SlackEvents` table in the database.
Finally, we queue a background job to process the event.

## Processing the event

The background job retrieves the row from `SlackEvents`, which contains the original event payload.
We use the `team_id` value to identify the organization associated with the event.
The organization has an Bot User Token associated with it, stored in the database, that allows us to act on behalf of the Abbot user on the organization's Chat Platform.
We use this token, along with our database, to resolve additional metadata about Users, Rooms, and other resources (see [What We Store](what-we-store.md) for more detail).

If the message occurred in a room with Managed Conversations enabled, we update Conversation State.
Either we create a new conversation, using the message body as the Title, or we associate the message with an existing conversation.
The only message content we save *in Conversation State* is the text of the first message, which is used to generate the title.
For other messages, we store only Slack's message "timestamp" identifier, which allows us to retrieve message content if **and only if** Abbot is able to view the conversation.
This allows users to revoke Abbot's ability to read message content, even retroactively.

If the message indicates a skill invocation (either directly, like `@abbot ping`/`.ping` or via a pattern) that requires invoking a user-authored skill, then we package the relevant message context up and make an HTTP request to the Skill Runner.
Included in that context is the Skill Code itself (for C# skills, a hash of the code is sent), which is stored in the database (see [What We Store](what-we-store.md) for more detail).
If the message is not a skill invocation, the message processing concludes here.

## Executing user-authored skills

User-authored skills are executed in a separate application, powered by Azure Functions.
Each language has a separate runner.
The runner receives the skill code, as well as any relevant information about the message, and an **API Token** provided by Abbot which authenticates the skill being run and allows the runner to invoke internal APIs for skill data storage and posting replies.
The JS and Python runners run the provided code as-is.
However, the C# runner requires **compiled** code to run, so there are a few extra steps involved.

To compile the skill code, the C# runner submits the "Code" it received from Abbot back to the Compilation API in Abbot using the API Token that was provided in the Runner request.
This "Code" is actually a hash of the skill code.
Abbot combines that hash with the skill identity encoded in the API token to check the compilation cache for a match.
If a match is found, the compiled assembly is returned back to the Runner.
If no match is found, the source text is retrieved from the database and the skill is compiled.
The compiled assembly is cached, and also returned back to the Runner.

Once the runner (regardless of language) has runnable code, it launches the code, providing it with access to APIs for managing skill data, rooms, secrets and replies.
Calling these APIs causes the runner to invoke HTTP APIs in Abbot, using the API Token provided in the Runner request.

## Abbot Runner APIs

Throughout the execution of a user-authored skill, the Runner may invoke several Abbot APIs.
These APIs **require** that the Runner provide an API Token, which was originally provided to the Runner when Abbot invoked it.
This token encodes the identity of the user who invoked the skill, as well as the identity of the skill itself.
The token also includes a timestamp, to prevent replay attacks, and is HMACed using a key known only to Abbot.
Incoming requests include User ID, Skill ID and Timestamp information in plaintext, which allows Abbot to reconstruct and validate the token and authenticate those values as having originally come from Abbot.

Skill Data is managed using the "Brain" API, which is a simple Key-Value store.
Skill Data can be scoped by Room, User or Conversation, allowing the same key to have multiple values depending on which scope it is read/written in.
We don't provide APIs to allow skills to read/write data across scopes (i.e. a Skill invoked by user A cannot read/write state scoped to user B),
but this is implemented within the Skill Runner and not enforced by the Abbot APIs, and should not be considered a security sandbox.

Secrets are read-only values managed in the UI.
Skills can use the "Secrets" API to retrieve the values of these secrets.
See [What We Store](what-we-store.md) for more information on how secrets are stored.
Skills **cannot** read secrets from other skills and **cannot** modify secret values.

Skills can use the "Rooms" API to manage Rooms in the Chat Platform.
This includes reading metadata about arbitrary rooms (as long as Abbot can see the room) and creating/archiving rooms.
A Skill can freely create/archive rooms as long as the Abbot user has permission to perform those operations in the Chat Platform.

Skills can send messages to any User in the Chat Platform, or to any Room that Abbot has access to.
The "Replies" API allows a skill to send these messages.
A Skill can freely send messages to any target that the Abbot user has permission to send messages to in the Chat Platform.