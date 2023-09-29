using System.Collections.Generic;
using System.Linq;
using Serious.Abbot.Pages.Shared.Filters;
using Serious.Filters;

namespace Serious.Abbot.Entities.Filters;

public static class AbbotFilterModelHelpers
{
    public static CompositeFilterModel CreateCustomerFilterModel(
        IEnumerable<Customer> customers,
        IEnumerable<CustomerTag> segments,
        FilterList filter,
        bool showValueInLabel = false)
    {
        return FilterModel.CreateCompositeFilterModel(
            FilterModel.Create(
                customers,
                filter,
                field: "customer",
                label: "Customers",
                c => c.Name,
                excludeField: "segment"),
            FilterModel.Create(
                segments,
                filter,
                field: "segment",
                label: "Segments",
                c => c.Name,
                excludeField: "customer"),
            "Customers",
            showValueInLabel: showValueInLabel);
    }

    /// <summary>
    /// Creates a filter model from the specified <see cref="Member"/> collection and the current value.
    /// </summary>
    /// <param name="members">The members to create options from.</param>
    /// <param name="filter">The current filter.</param>
    /// <param name="field">The field to use.</param>
    /// <param name="label">The label to use.</param>
    /// <returns>A <see cref="FilterModel"/>.</returns>
    public static FilterModel Create(
        IEnumerable<Member> members,
        FilterList filter,
        string field,
        string label) =>
        new(members.Select(m => CreateOptionModel(m, field, filter)).ToReadOnlyList(), filter)
        {
            Field = field,
            Label = label,
        };

    /// <summary>
    /// Creates a filter model from the specified <see cref="Member"/> collection and the current value.
    /// </summary>
    /// <param name="members">The members to create options from.</param>
    /// <param name="filter">The current filter.</param>
    /// <param name="field">The field to use.</param>
    /// <param name="label">The label to use.</param>
    /// <returns>A <see cref="FilterModel"/>.</returns>
    public static FilterModel CreateResponderModel(
        IEnumerable<Member> members,
        FilterList filter,
        string field,
        string label)
    {
        var options = new[]
        {
            new ToggleOptionModel(field, filter, "Default", "default", Include: true),
            new ToggleOptionModel(field, filter, "Not Default", "default", Include: false),
        }.Concat(members.Select(m => CreateOptionModel(m, field, filter)));
        return FilterModel.Create(options, filter, field, label);
    }

    static FilterOptionModel CreateOptionModel(Member member, string field, FilterList filter)
        => FilterOptionModel.CreateOption(
            field,
            filter,
            member.User.PlatformUserId,
            member.DisplayName,
            member.User.Avatar);

    public static FilterOption CreateOptionFromMember(Member member)
         => new(member.DisplayName, member.User.PlatformUserId, member.User.Avatar);
}
