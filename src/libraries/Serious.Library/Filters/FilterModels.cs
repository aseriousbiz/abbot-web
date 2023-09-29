using System;
using System.Collections.Generic;
using System.Linq;
using Serious.Filters;

namespace Serious.Abbot.Pages.Shared.Filters;

/// <summary>
/// Interface for a filter model.
/// </summary>
public interface IFilterModel
{
    /// <summary>
    /// The label to use for the filter.
    /// </summary>
    string Label { get; }

    /// <summary>
    /// If true, the current value should be shown in the label when filter is active.
    /// </summary>
    bool ShowValueInLabel { get; }

    /// <summary>
    /// The current filter value.
    /// </summary>
    FilterOption? FilterValue { get; }
}

/// <summary>
/// Model used for filtering on a collection of options.
/// </summary>
/// <param name="Options">The sent of entities that can be filtered on.</param>
/// <param name="Current">The current filter.</param>
public record FilterModel(IReadOnlyList<FilterOptionModel> Options, FilterList Current) : IFilterModel
{
    /// <summary>
    /// Creates a filter model from the specified items and the current value.
    /// </summary>
    /// <param name="options">The set of filter options.</param>
    /// <param name="filter">The current filter.</param>
    /// <param name="field">The field to use.</param>
    /// <param name="label">The label to use.</param>
    /// <param name="excludeField">The field to exclude in the option filter.</param>
    /// <param name="showValueInLabel">If <c>true</c>, then the label shows the current filter value, if any.</param>
    /// <returns>A <see cref="FilterModel"/>.</returns>
    public static FilterModel Create(
        IEnumerable<FilterOption> options,
        FilterList filter,
        string field,
        string label,
        string? excludeField = null,
        bool showValueInLabel = false) =>
        new(options.Select(option => CreateOptionModel(option, field, filter, excludeField: excludeField)).ToReadOnlyList(), filter)
        {
            Field = field,
            Label = label,
            ShowValueInLabel = showValueInLabel,
        };

    /// <summary>
    /// Creates a filter model from the specified items and the current value.
    /// </summary>
    /// <param name="items">The members to create options from.</param>
    /// <param name="filter">The current filter.</param>
    /// <param name="field">The field to use.</param>
    /// <param name="label">The label to use.</param>
    /// <param name="valueGetter">Func to get a value from the item.</param>
    /// <param name="excludeField">The field to exclude in the option filter.</param>
    /// <param name="showValueInLabel">If <c>true</c>, then the label shows the current filter value, if any.</param>
    /// <returns>A <see cref="FilterModel"/>.</returns>
    public static FilterModel Create<T>(
        IEnumerable<T> items,
        FilterList filter,
        string field,
        string label,
        Func<T, string> valueGetter,
        string? excludeField = null,
        bool showValueInLabel = false) =>
        Create(CreateFilterOptions(items, valueGetter), filter, field, label, excludeField, showValueInLabel);

    /// <summary>
    /// Creates a composite filter model from the specified items and the current value.
    /// </summary>
    /// <param name="firstFilter">The first filter.</param>
    /// <param name="secondFilter">The second filter.</param>
    /// <param name="label">The label for the filter.</param>
    /// <param name="showValueInLabel">If <c>true</c>, then the label shows the current filter value, if any.</param>

    public static CompositeFilterModel CreateCompositeFilterModel(
        FilterModel firstFilter,
        FilterModel secondFilter,
        string label,
        bool showValueInLabel = false)
        => new(firstFilter, secondFilter)
        {
            Label = label,
            ShowValueInLabel = showValueInLabel,
        };

    static FilterOptionModel CreateOptionModel(
        FilterOption option,
        string field,
        FilterList filter,
        string? excludeField = null)
        => option as FilterOptionModel ?? FilterOptionModel.CreateOption(
            field,
            filter,
            value: option.Value,
            label: option.Label,
            excludeField: excludeField);

    /// <summary>
    /// If specified, this is the field to use for the filter.
    /// </summary>
    public required string Field { get; init; }

    /// <inheritdoc />
    public required string Label { get; init; }

    /// <inheritdoc />
    public bool ShowValueInLabel { get; init; }

    /// <inheritdoc />
    public FilterOption? FilterValue => Options.FirstOrDefault(o => o.IsActive);

    static IEnumerable<FilterOption> CreateFilterOptions<T>(IEnumerable<T> items, Func<T, string> valueGetter)
    {
        return new[] { new FilterOption("Unassigned", "none") }
            .Concat(items.Select(e => new FilterOption(valueGetter(e), valueGetter(e))));
    }
}

/// <summary>
/// The model for a single filter option.
/// </summary>
/// <param name="Label">The label to use for the option.</param>
/// <param name="Value">The value of the filter option.</param>
/// <param name="Image">The image to display for this option.</param>
public record FilterOption(string Label, string Value, string? Image = null);

/// <summary>
/// The model for a single filter option.
/// </summary>
/// <param name="Field">The field to filter on.</param>
/// <param name="CurrentFilter">The current filter.</param>
/// <param name="Label">The label to use for the option.</param>
/// <param name="Value">The value of the filter option.</param>
/// <param name="Image">The image to display for this option.</param>
public record FilterOptionModel(
    string Field,
    FilterList CurrentFilter,
    string Value,
    string? Label = null,
    string? Image = null) : FilterOption(Label ?? Value, Value, Image)
{
    public Filter? CurrentFilterItem => CurrentFilter[Field];

    protected virtual string? NewFilterValue => IsActive && Including
        ? null
        : Filter.FormatValue(Value);

    public FilterList NewFilter => CurrentFilter.WithReplaced(Field, NewFilterValue);

    /// <summary>
    /// If true, then this filter option is active and is being included.
    /// </summary>
    public bool Including => CurrentFilterItem?.Include is not false;

    public virtual bool IsActive => (CurrentFilterItem?.LowerCaseValue ?? string.Empty) == Value.ToLowerInvariant();

    /// <summary>
    /// Creates a filter option model for the specified item.
    /// </summary>
    /// <returns>Returns a <see cref="FilterOptionModel"/>.</returns>
    public static FilterOptionModel CreateOption(
        string field,
        FilterList filter,
        string value,
        string? label = default,
        string? image = default,
        string? excludeField = default)
        => new(
            field,
            excludeField is null ? filter : filter.Without(excludeField),
            value,
            Label: label ?? value,
            Image: image);
}

/// <summary>
/// Used to represent a filter option that is part of a pair of "toggle" options.
/// </summary>
/// <remarks>
/// This is used in cases where we want to prevent an option and its negation as two options in the filter list.
/// For example, for responders, we have "Default" and "Not default". This requires a slight behavior change in that
/// if one of these options are negated, instead of showing the negated symbol, we show nothing because we know the
/// other toggle option will be selected.
/// </remarks>
/// <param name="Field">The field to filter on.</param>
/// <param name="CurrentFilter">The current filter.</param>
/// <param name="Label">The label for this option.</param>
/// <param name="Value">The value for this option.</param>
public record ToggleOptionModel(
        string Field,
        FilterList CurrentFilter,
        string Label,
        string Value,
        bool Include)
    : FilterOptionModel(Include ? Field : $"-{Field}", CurrentFilter, Value, Label)
{
    public override bool IsActive => CurrentFilterItem?.Include == Include
        && (CurrentFilterItem?.LowerCaseValue ?? string.Empty) == Value.ToLowerInvariant();

    protected override string? NewFilterValue => IsActive && Including == Include
        ? null
        : Filter.FormatValue(Value);
}


/// <summary>
/// Model used for a composite filter (usually a tabbed filter UI) where each tab is a different filter type.
/// </summary>
/// <param name="FirstFilter">The first filter.</param>
/// <param name="SecondFilter">The second filter.</param>
public record CompositeFilterModel(
    FilterModel FirstFilter,
    FilterModel SecondFilter) : IFilterModel
{
    /// <inheritdoc />
    public required string Label { get; init; }

    /// <inheritdoc />
    public bool ShowValueInLabel { get; init; }

    /// <inheritdoc />
    public FilterOption? FilterValue => FirstFilter.FilterValue ?? SecondFilter.FilterValue;
}
