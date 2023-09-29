using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Messaging;
using Serious.Abbot.Repositories;

namespace Serious.Abbot.Skills;

[Skill("rem",
    Description = "Remember things. Associate text and urls to a keyword or phrase.")]
public sealed class RememberSkill : ISkill
{
    readonly IMemoryRepository _memoryRepository;

    public RememberSkill(IMemoryRepository memoryRepository)
    {
        _memoryRepository = memoryRepository;
    }

    public async Task OnMessageActivityAsync(
        MessageContext messageContext,
        CancellationToken cancellationToken)
    {
        var match = SkillPatterns.MatchRememberPattern(messageContext.Arguments.Value);

        await (match switch
        {
            ("|", var value) => Search(value, "Search results:", messageContext),
            ({ Length: 0 }, { Length: 0 }) => messageContext.SendHelpTextAsync(this),
            (var key, { Length: 0 }) => ReplyWithMemory(key, messageContext),
            var (key, value) => AddItemAndReply(key, value, messageContext)
        });
    }

    public void BuildUsageHelp(UsageBuilder usage)
    {
        usage.Add("{some phrase} is {some value}",
            "remembers that {some value} is recalled with {some phrase}");
        usage.Add("{some phrase}",
            "returns {some value} associated with {some phrase}. If the phrase is not found, searches for similar phrases.");
        usage.AddAlternativeUsage("forget", "{some phrase}",
            "removes {some phrase} and its value.");
        usage.Add("| {some phrase}",
            "searches remembered items for {some phrase}@m.");
        usage.AddVerbatim("If the phrase to remember contains the word `is`, you can wrap the phrase in quotes. ex:");
        usage.Add("\"some phrase is\" is some value", "remembers that `some value` is recalled with `some phrase is`.");
    }

    async Task AddItemAndReply(string name,
        string value,
        MessageContext messageContext)
    {
        var foundMemory = await _memoryRepository.GetAsync(name, messageContext.Organization);

        if (foundMemory is not null)
        {
            // TODO: Implement conversational overwrite mechanics.
            await messageContext.SendActivityAsync(
                FormatItemAlreadyExistsMessage(foundMemory));
            return;
        }

        var memory = new Memory
        {
            Name = name,
            Content = value,
            Organization = messageContext.Organization
        };

        await _memoryRepository.CreateAsync(memory, messageContext.From);
        await messageContext.SendActivityAsync(
            FormatAddItemReply(name, value));
    }

    static string FormatItemAlreadyExistsMessage(Memory storedItem)
    {
        return $@"`{storedItem.Name}` is already `{storedItem.Content}` {storedItem.ToMetadataMarkdown()}.";
    }

    static string FormatAddItemReply(string name, string value)
    {
        return $@"Ok! I will remember that `{name}` is `{value}`.";
    }

    async Task ReplyWithMemory(string name, MessageContext messageContext)
    {
        var foundMemory = await _memoryRepository.GetAsync(name, messageContext.Organization);
        if (foundMemory is not null)
        {
            await messageContext.SendActivityAsync(foundMemory.Content);
        }
        else
        {
            await Search(
                name,
                @$"I don’t know anything about `{name}`, but I found these similar items:",
                messageContext);
        }
    }

    async Task Search(string searchText,
        string searchHeader,
        MessageContext messageContext)
    {
        var matches = await SearchBrain(searchText, messageContext.Organization);

        if (!matches.Any())
        {
            await messageContext.SendActivityAsync(
                $@"I couldn’t find anything about `{searchText}`.");
            return;
        }

        var matchesText = matches.ToMarkdownList();

        var response = $@"{searchHeader}
{matchesText}";
        await messageContext.SendActivityAsync(response);
    }

    async Task<IReadOnlyList<Memory>> SearchBrain(
        string searchText,
        Organization organization)
    {
        var memories = await _memoryRepository.GetAllAsync(organization);
        return memories
            .WhereFuzzyMatch(kvp => kvp.Name, searchText)
            .ToList();
    }
}
