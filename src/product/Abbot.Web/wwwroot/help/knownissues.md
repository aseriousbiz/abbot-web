# Known Issues

We hope you're having a great time with Abbot. Sometimes you might run into an issue that seems strange, and you aren't sure if something's broken, or working as intended. This is a short list of issues that we're aware of but haven't scheduled to fix immediately.

Please let us know if any of these issues impacts you negatively by using the `@abbot feedback` skill from chat or the bot console.


## Core Abbot
No known issues

## Skill Editor
  * Delayed replies in skills will return immediately when testing in the Skill Editor on https://ab.bot. This is by design, as we don't maintain an active connection with the editor console on the website. Developers can still use the language-appropriate version of "reply later" in the skill editor and expect it to work correctly in chat as long as the delay is set. This impacts Abbot's Bot Console as well.

## Skill Triggers
  * Skill Triggers may only be attached in channels with more than one participant. When attempting to attach a trigger in a channel with only one participant, the message may be confusing.

## C# Runtime
  * C# 8 switch expressions with an undeclared type (using `var`) will cause the script compiler to crash, which in turn prevents a skill from completing correctly. We have [reported the issue upstream to Microsoft](https://github.com/dotnet/roslyn/issues/49529). The workaround is to specify a type for the expression. For example, this will crash:

```csharp
var (first, second) = Bot.Arguments;
var reply = (first, second) switch
{
    (IMissingArgument, _) => "Both arguments are missing",
    (IMentionArgument, IMissingArgument) => "You mentioned somebody",
    (IMentionArgument, IArgument) => "You mentioned someone and said more",
    _ => "Dunno what you did"
};
await Bot.ReplyAsync(reply);
```

But this will work:

```csharp
var (first, second) = Bot.Arguments;
string reply = (first, second) switch
{
    (IMissingArgument, _) => "Both arguments are missing",
    (IMentionArgument, IMissingArgument) => "You mentioned somebody",
    (IMentionArgument, IArgument) => "You mentioned someone and said more",
    _ => "Dunno what you did"
};
await Bot.ReplyAsync(reply);
```

Notice that `reply` is typed as `string` instead of relying on type inference in the second case.

## Python Runtime
No known issues

## JavaScript Runtime
No known issues
