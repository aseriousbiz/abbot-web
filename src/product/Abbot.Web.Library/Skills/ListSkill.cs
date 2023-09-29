using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Serious.Abbot.Entities;
using Serious.Abbot.Infrastructure;
using Serious.Abbot.Messaging;
using Serious.Abbot.Models;
using Serious.Abbot.Repositories;
using Serious.Abbot.Scripting;
using Serious.Abbot.Validation;
using Serious.Cryptography;
using Serious.Logging;

namespace Serious.Abbot.Skills;

/// <summary>
/// Skill used to create, update, and remove named lists.
/// </summary>
/// <example>
/// @abbot list add deepthought // Creates a list named "deepthought"
/// @abbot list info deepthought // Shows info about deepthought
/// @abbot list remove deepthought // Deletes a list named "deepthought"
/// @abbot list deepthought add A very deep thought
/// @abbot list deepthought remove A very deep thought
/// @abbot list deepthought // Returns a random item from the list.
/// </example>
[Skill(Name, Description = "Manage named custom lists of things and retrieve a random item from a list.")]
public sealed class ListSkill : ISkill, ISkillContainer
{
    public const string Name = "list";
    static readonly ILogger<ListSkill> Log = ApplicationLoggerFactory.CreateLogger<ListSkill>();
    static readonly CryptoRandom Random = new();
    readonly IListRepository _userListRepository;
    readonly ISkillNameValidator _nameValidator;

    public ListSkill(IListRepository userListRepository, ISkillNameValidator nameValidator)
    {
        _userListRepository = userListRepository;
        _nameValidator = nameValidator;
    }

    public async Task OnMessageActivityAsync(
        MessageContext messageContext,
        CancellationToken cancellationToken)
    {
        var (commandOrName, nameOrCommand, valueArg) = messageContext.Arguments;

        await ((commandOrName.Value, nameOrCommand.Value, valueArg.Value) switch
        {
            ({ Length: 0 }, { Length: 0 }, { Length: 0 }) => ReplyWithUsage(messageContext),
            ("add" or "create", { Length: 0 }, { Length: 0 }) => ReplyWithUsage(messageContext),
            ("remove" or "delete", { Length: 0 }, { Length: 0 }) => ReplyWithUsage(messageContext),
            ("add", var name, var description) => CreateList(name, description, messageContext),
            ("describe", var name, var description) => UpdateListDescription(name, description, messageContext),
            ("remove", var name, { Length: 0 }) => RemoveList(name, messageContext),
            ("list", { Length: 0 }, { Length: 0 }) => ShowListOfLists(messageContext),
            (var name, "list", { Length: 0 }) => ShowListItems(name, messageContext),
            (var name, "info", { Length: 0 }) => ShowListInfo(name, messageContext),
            (var name, "add", var value) => AddToList(name, value, messageContext),
            (var name, "remove", var value) => RemoveFromList(name, value, messageContext),
            var (name, _, _) => ShowRandomListItem(name, messageContext),
        });
    }

    public void BuildUsageHelp(UsageBuilder usage)
    {
        foreach (var (args, description) in GetUsages())
        {
            usage.Add(args, description);
        }
    }

    public void BuildSkillUsageHelp(UsageBuilder usage)
    {
        // Filter usages.
        foreach (var (args, description) in GetUsages())
        {
            if (!args.StartsWith("{name}", StringComparison.Ordinal))
                continue;
            var arg = args.RightAfter("{name}", StringComparison.Ordinal).TrimStart();
            usage.Add(arg, description.Replace("{name}", usage.SkillName, StringComparison.Ordinal));
        }
    }

    static IEnumerable<(string, string)> GetUsages()
    {
        yield return ("add {name} {description}", "creates a list named {name} with a description.");
        yield return ("remove {name}", "removes the {name} list.");
        yield return ("{name} add {some text}", "adds {some text} to the {name} list.");
        yield return ("{name} remove {some text}", "removes {some text} from the {name} list.");
        yield return ("{name}", "retrieves a random item from the {name} list.");
        yield return ("{name} info", "Returns information about the list.");
        yield return ("{name} list", "Returns all the items in the list.");
    }

    async Task ShowListOfLists(MessageContext messageContext)
    {
        var lists = await _userListRepository.GetAllAsync(messageContext.Organization);
        var response = lists
            .ToMarkdownList();
        if (response.Length == 0)
        {
            response = $"There are no lists yet. Use `{messageContext.Bot} {Name} add {{name}} {{description}}` to add " +
                       $"a list. Use `{messageContext.Bot} help {Name}` to see how to use the `{Name}` command";
        }

        await messageContext.SendActivityAsync(response);
    }

    async Task ShowListItems(string name, MessageContext messageContext)
    {
        var list = await _userListRepository.GetAsync(name, messageContext.Organization);
        if (list is not null)
        {
            var listText = list.Entries
                .Select(l => $"`{l.Content}`")
                .ToMarkdownList();
            if (listText.Length == 0)
            {
                listText =
                    $"There are no items in the list `{name}`. Try `{messageContext.Bot} {name} add ...` to add something to the list.";
            }

            await messageContext.SendActivityAsync(listText);
        }
        else
        {
            await messageContext.SendActivityAsync(
                $"The list `{name}` doesn’t exist. {GetAddListUsage(name, messageContext)}");
        }
    }

    async Task ShowListInfo(string name, MessageContext messageContext)
    {
        var list = await _userListRepository.GetAsync(name, messageContext.Organization);
        if (list is not null)
        {
            await messageContext.SendActivityAsync(
                $"The list `{name}`".AppendIfNotEmpty(list.Description, " with description \"_", "_\"")
                + $" was created by `{list.Creator.FormatMention()}`{list.Created.ToMarkdown()} and has `{list.Entries.Count}` items.");
        }
        else
        {
            await messageContext.SendActivityAsync(
                $"The list `{name}` doesn’t exist. {GetAddListUsage(name, messageContext)}");
        }
    }

    async Task ShowRandomListItem(string name, MessageContext messageContext)
    {
        var list = await _userListRepository.GetAsync(name, messageContext.Organization);
        if (list is not null)
        {
            if (list.Entries.Any())
            {
                var randomItem = list.Entries[Random.Next(list.Entries.Count)].Content;
                await messageContext.SendActivityAsync(randomItem);
            }
            else
            {
                await messageContext.SendActivityAsync($"The list `{name}` is empty. {GetAddToListUsage(name, messageContext)}");
            }
        }
        else
        {
            await messageContext.SendActivityAsync($"The list `{name}` doesn’t exist. {GetAddListUsage(name, messageContext)}");
        }
    }

    async Task CreateList(string name, string description, MessageContext messageContext)
    {
        var existing = await _userListRepository.GetAsync(name, messageContext.Organization);

        if (existing is null)
        {
            var isUnique = await _nameValidator.IsUniqueNameAsync(name, 0, nameof(UserList), messageContext.Organization);
            if (!isUnique.IsUnique)
            {
                await messageContext.SendActivityAsync(
                    $"The name `{name}` is already in use.");
                return;
            }

            var list = new UserList
            {
                Name = name,
                Description = description,
                Organization = messageContext.Organization
            };
            await _userListRepository.CreateAsync(list, messageContext.From);

            var reply = $"Created list `{name}`. {GetAddToListUsage(name, messageContext)}";
            if (description.Length == 0)
            {
                reply +=
                    $" Use `{messageContext.Bot} {Name} describe {name} {{description}}` to add a " +
                    "description so others know what the list is about.";
            }

            await messageContext.SendActivityAsync(reply);
        }
        else
        {
            await messageContext.SendActivityAsync($"List `{name}` already exists. {GetAddToListUsage(name, messageContext)}");
        }
    }

    async Task RemoveList(string name, MessageContext messageContext)
    {
        var list = await _userListRepository.GetAsync(name, messageContext.Organization);
        if (list is not null)
        {
            await _userListRepository.RemoveAsync(list, messageContext.From);
            await messageContext.SendActivityAsync($"Poof! I removed the list `{name}`.");
        }
        else
        {
            await messageContext.SendActivityAsync($"Nothing to remove. The list `{name}` doesn’t exist.");
        }
    }

    async Task UpdateListDescription(string name, string description, MessageContext messageContext)
    {
        var list = await _userListRepository.GetAsync(name, messageContext.Organization);
        if (list is not null)
        {
            list.Description = description;
            await _userListRepository.UpdateAsync(list, messageContext.From);
            await messageContext.SendActivityAsync($"I updated the description for the `{name}` list.");
        }
        else
        {
            await messageContext.SendActivityAsync($"Nothing to describe. The list `{name}` doesn’t exist.");
        }
    }


    async Task AddToList(string name, string value, MessageContext messageContext)
    {
        var list = await _userListRepository.GetAsync(name, messageContext.Organization);
        if (list is not null)
        {
            try
            {
                await _userListRepository.AddEntryToList(list, value, messageContext.From);
                await messageContext.SendActivityAsync($"Added `{value}` to the `{name}` list.");
            }
            catch (Exception e)
            {
                Log.ExceptionAddingValueToList(e, name, value, messageContext.Organization.PlatformId, messageContext.Organization.PlatformType);
                await messageContext.SendActivityAsync($"Something went wrong adding the `{value}` to the `{name}` list.");
            }
        }
        else
        {
            await ReplyListDoesNotExist(name, messageContext);
        }
    }

    async Task RemoveFromList(string name,
        string value,
        MessageContext messageContext)
    {
        var list = await _userListRepository.GetAsync(name, messageContext.Organization);
        if (list is null)
        {
            await messageContext.SendActivityAsync(
                $"The list `{name}` doesn’t exist. {GetAddListUsage(name, messageContext)}");
            return;
        }

        try
        {
            var removed = await _userListRepository.RemovesEntryFromList(list, value, messageContext.From);
            if (removed)
            {
                await messageContext.SendActivityAsync($"Removed `{value}` from the list `{name}`.");
            }
            else
            {
                await messageContext.SendActivityAsync(
                    $"Nothing to remove. I could not find `{value}` in the list `{name}`.");
            }
        }
        catch (Exception e)
        {
            Log.ExceptionRemovingValueFromList(e, name, value, messageContext.Organization.PlatformId, messageContext.Organization.PlatformType);
            await messageContext.SendActivityAsync(
                $"Something went wrong trying to remove `{value}` from the list `{name}`.");
        }
    }

    Task ReplyWithUsage(MessageContext messageContext)
    {
        return messageContext.SendHelpTextAsync(this);
    }

    static async Task ReplyListDoesNotExist(string name, MessageContext messageContext)
    {
        await messageContext.SendActivityAsync($"The list `{name}` doesn’t exist. {GetAddListUsage(name, messageContext)}");
    }

    static string GetAddListUsage(string name, MessageContext messageContext)
    {
        return $"Use `{messageContext.Bot} {Name} add {name} {{description}}` to create the list.";
    }

    static string GetAddToListUsage(string name, MessageContext messageContext)
    {
        return $"Use `{messageContext.Bot} {name} add {{something}}` to add items to this list.";
    }
}
