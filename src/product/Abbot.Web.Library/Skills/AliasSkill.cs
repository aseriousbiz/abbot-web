using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Messaging;
using Serious.Abbot.Repositories;
using Serious.Abbot.Services;

namespace Serious.Abbot.Skills;

[Skill(Description = "Manages aliases (or shortcuts) for an existing skill.", Hidden = true)]
public sealed class AliasSkill : ISkill
{
    readonly IAliasRepository _aliasRepository;
    readonly ISkillManifest _skillManifest;

    public AliasSkill(IAliasRepository aliasRepository, ISkillManifest skillManifest)
    {
        _aliasRepository = aliasRepository;
        _skillManifest = skillManifest;
    }

    public async Task OnMessageActivityAsync(MessageContext messageContext, CancellationToken cancellationToken)
    {
        var (commandOrName, nameOrCommand, valueArg) = messageContext.Arguments;

        await ((commandOrName.Value, nameOrCommand, valueArg) switch
        {
            ("show", var name, IMissingArgument) => ReplyWithAlias(name.Value, messageContext),
            ("list", IMissingArgument, IMissingArgument) => ReplyWithList(messageContext),
            (_, IMissingArgument, IMissingArgument) => messageContext.SendHelpTextAsync(this),
            ("add", var name, var value) => AddItemAndReply(name.Value, value.Value, messageContext),
            ("remove" or "delete", var name, IMissingArgument) => RemoveAliasAndReply(name.Value, messageContext),
            ("search", var searchText, IMissingArgument) => Search(searchText.Value, "Search results:", messageContext),
            ("describe", var name, var value) when value is not IMissingArgument => AddDescriptionAndReply(name.Value, value.Value, messageContext),
            _ => messageContext.SendHelpTextAsync(this)
        });
    }

    async Task AddItemAndReply(string name, string value, MessageContext messageContext)
    {
        var existing = await _aliasRepository.GetAsync(name, messageContext.Organization);

        if (existing is not null)
        {
            await messageContext.SendActivityAsync(
                FormatItemAlreadyExistsMessage(existing, messageContext));
            return;
        }

        var alias = CreateAliasInstance(name, value, messageContext);

        if (!await ValidateAndReplyIfInvalid(alias, messageContext))
        {
            return;
        }

        await _aliasRepository.CreateAsync(alias, messageContext.From);
        await messageContext.SendActivityAsync(
            FormatAddItemReply(name, value, messageContext));
    }

    async Task RemoveAliasAndReply(string name, MessageContext messageContext)
    {
        var alias = await _aliasRepository.GetAsync(name, messageContext.Organization);
        if (alias is not null)
        {
            await _aliasRepository.RemoveAsync(alias, messageContext.From);
            await messageContext.SendActivityAsync(
                $@"Poof! I removed `{name}`.");
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

        var matchesText = matches
            .ToMarkdownList();

        var response = $@"{searchHeader}
{matchesText}";
        await messageContext.SendActivityAsync(response);
    }

    async Task<IReadOnlyList<Alias>> SearchBrain(string searchText, Organization organization)
    {
        var aliases = await _aliasRepository.GetAllAsync(organization);
        return aliases
            .WhereFuzzyMatch(kvp => kvp.Name, searchText)
            .ToReadOnlyList();
    }

    async Task ReplyWithAlias(string name, MessageContext messageContext)
    {
        var alias = await _aliasRepository.GetAsync(name, messageContext.Organization);
        if (alias is not null)
        {
            string formattedValue = FormatForShow(alias);
            await messageContext.SendActivityAsync(formattedValue);
        }
        else
        {
            await Search(
                name,
                @$"I don’t know anything about `{name}`, but I found these similar items:",
                messageContext);
        }
    }

    public void BuildUsageHelp(UsageBuilder usage)
    {
        usage.Add("add {name} {skill}", "makes it possible to call {skill} via {name}.");
        usage.Add("add {name} {skill} {arguments}", "prepends the {arguments} to the user supplied arguments when using the alias.");
        usage.Add("show {name}", "shows the alias for {name}.");
        usage.Add("list", "lists all of the aliases. All of them.");
        usage.Add("remove {name}", "removes the {name} alias.");
        usage.AddExample("add deepthoughts list deepthoughts",
            $"makes it possible to add to the `deepthoughts` list by calling `{usage.Bot} deepthoughts add My Deep Thought` which translates to `{usage.Bot} list deepthoughts add My Deep Thought`");
    }

    async Task AddDescriptionAndReply(string name, string value, MessageContext messageContext)
    {
        var alias = await _aliasRepository.GetAsync(name, messageContext.Organization);
        if (alias is null)
        {
            await messageContext.SendActivityAsync(
                $"I could not describe the alias `{name}` because it does not exist.");
            return;
        }

        alias.Description = value;
        await _aliasRepository.UpdateAsync(alias, messageContext.From);
        await messageContext.SendActivityAsync("I added the description to the alias.");
    }

    async Task ReplyWithList(MessageContext messageContext)
    {
        var aliases = await _aliasRepository.GetAllAsync(messageContext.Organization);
        if (!aliases.Any())
        {
            await messageContext.SendActivityAsync(
                $"There are no aliases yet. Try `{messageContext.Bot} alias help` to learn how to create an alias");
            return;
        }

        var reply = aliases
            .ToMarkdownList();
        await messageContext.SendActivityAsync(reply);
    }

    static Alias CreateAliasInstance(string name, string value, MessageContext messageContext)
    {
        var (skill, args) = ParsedMessage.ParseCommand(value);
        var description = $"A shortcut to `{skill}`";

        return new Alias
        {
            Name = name,
            TargetSkill = skill,
            TargetArguments = args,
            Description = args is { Length: > 0 }
                ? $"{description} that prepends `{args}` to the arguments"
                : description,
            Organization = messageContext.Organization
        };
    }

    async Task<bool> ValidateAndReplyIfInvalid(Alias alias, MessageContext messageContext)
    {
        // Make sure it doesn't target another alias
        var targetAlias = await _aliasRepository.GetAsync(alias.TargetSkill, messageContext.Organization);
        if (targetAlias is not null)
        {
            await messageContext.SendActivityAsync(
                $"Sorry, I cannot create an alias that points to another alias (`{targetAlias.Name}`).");
            return false;
        }

        var existing = await _skillManifest.ResolveSkillAsync(
            alias.Name,
            messageContext.Organization,
            messageContext);
        if (existing is not null)
        {
            await messageContext.SendActivityAsync(
                $"The alias `{alias.Name}` conflicts with the `{existing.Name}` skill. Try another name.");
            return false;
        }

        var targetSkill = await _skillManifest.ResolveSkillAsync(
            alias.TargetSkill,
            messageContext.Organization,
            messageContext);

        if (targetSkill is null)
        {
            await messageContext.SendActivityAsync(
                $"Cannot create an alias to `{alias.TargetSkill}` as it does not exist.");
            return false;
        }

        return true;
    }

    static string FormatForShow(Alias alias)
    {
        return $"`{alias.TargetSkill.AppendIfNotEmpty(alias.TargetArguments)}`"
                   .AppendIfNotEmpty(alias.Description, " - ")
               + $" {alias.ToMetadataMarkdown()}";
    }

    static string FormatAddItemReply(string name, string value, MessageContext messageContext)
    {
        return $"Ok! `{messageContext.Bot} {name}` will call `{messageContext.Bot} {value}`.\nYou can add a description to the alias by calling `{messageContext.Bot} alias describe {name} {{description}}`";
    }

    static string FormatItemAlreadyExistsMessage(Alias alias, MessageContext messageContext)
    {
        string value = alias.TargetSkill.AppendIfNotEmpty(alias.TargetArguments);

        return $@"`{alias.Name}` is already `{value}` {alias.ToMetadataMarkdown()}."
               + $"\nTry another name or call `{messageContext.Bot} alias remove {alias.Name}` to remove it first.";
    }
}
