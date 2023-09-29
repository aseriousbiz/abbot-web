using System;
using System.Collections.Generic;
using System.Linq;

namespace Serious.Abbot.Models;

/// <summary>
/// The type of plan the organization is on.
/// </summary>
public enum PlanType
{
    /// <summary>
    /// Designates an organization that was "imported" such as a foreign organization.
    /// </summary>
    None = 0,

    /// <summary>
    /// An organization on a free plan. Typically this only applies to the free trial period.
    /// </summary>
    Free = 1,

    /// <summary>
    /// Legacy team plan. This will eventually go away.
    /// </summary>
    Team = 2,

    /// <summary>
    /// The plan for users of conversation management. This is what we're about right now.
    /// </summary>
    Business = 3,

    /// <summary>
    /// Legacy founding member plan.
    /// </summary>
    FoundingCustomer = 4,

    /// <summary>
    /// This is going away.
    /// </summary>
    Beta = 5,

    /// <summary>
    /// This is just for us.
    /// </summary>
    Unlimited = 6,
}

public static class PlanTypeExtensions
{
    public static Plan GetFeatures(this PlanType planType) => Plan.ForPlan(planType);
}

[Flags]
public enum PlanFeature
{
    None = 0b0000_0000,
    SkillPermissions = 0b0000_0001,
    ConversationTracking = 0b0000_0010,

    All = SkillPermissions | ConversationTracking,
}

public class Plan
{
    public static readonly IReadOnlyList<PlanType> AllTypes = GetAllPlans().Select(x => x.Type).ToList();
    static readonly Dictionary<PlanType, Plan> PlansByType = GetAllPlans().ToDictionary(x => x.Type);

    /// <summary>
    /// Gets the <see cref="PlanType"/> that identifies this plan.
    /// </summary>
    public PlanType Type { get; init; }

    /// <summary>
    /// Gets the display name of the plan.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets the monthly price for the plan
    /// </summary>
    public decimal MonthlyPrice { get; init; }

    public bool PricePerAgent { get; init; }

    /// <summary>
    /// Gets the maximum number of skills allowed by this plan, or null if unlimited.
    /// </summary>
    public int? MaximumSkillCount { get; init; }

    /// <summary>
    /// Gets the maximum number of admins allowed by this plan, or null if unlimited.
    /// </summary>
    public int? MaximumAdminCount { get; init; }

    /// <summary>
    /// Gets a bitwise-OR of <see cref="PlanFeature"/> values representing the features this plan supports.
    /// </summary>
    public PlanFeature Features { get; init; }

    /// <summary>
    /// Gets a boolean indicating if the plan has the provided feature.
    /// </summary>
    /// <param name="feature">The <see cref="PlanFeature"/> feature to test for.</param>
    public bool HasFeature(PlanFeature feature) => (Features & feature) == feature;

    public static Plan ForPlan(PlanType planType)
    {
        if (!PlansByType.TryGetValue(planType, out var features))
        {
            throw new ArgumentException($"Unknown plan type: {planType}");
        }

        return features;
    }

    static IEnumerable<Plan> GetAllPlans()
    {
        yield return new Plan
        {
            Type = PlanType.None,
            Name = "None",
            Features = PlanFeature.None,
            MaximumSkillCount = 0,
            MaximumAdminCount = 0,
        };

        yield return new Plan
        {
            Type = PlanType.Free,
            Name = "Free",
            Features = PlanFeature.None,
            MaximumSkillCount = 5,
            MaximumAdminCount = 1,
        };

        yield return new Plan
        {
            Type = PlanType.Team,
            Name = "Abbot Classic",
            Features = PlanFeature.None,
            MonthlyPrice = 19.00m,
            MaximumSkillCount = 25,
        };

        yield return new Plan
        {
            Type = PlanType.Business,
            Name = "Business",
            Features = PlanFeature.SkillPermissions | PlanFeature.ConversationTracking,
            MonthlyPrice = 49.00m,
            PricePerAgent = true,
        };

        yield return new Plan
        {
            Type = PlanType.FoundingCustomer,
            Name = "✨ Founding Customer ✨",
            Features = PlanFeature.SkillPermissions,
            MonthlyPrice = 8.25m,
            MaximumSkillCount = 25,
        };

        yield return new Plan
        {
            Type = PlanType.Beta,
            Name = "Beta",
            Features = PlanFeature.All,
        };

        yield return new Plan
        {
            Type = PlanType.Unlimited,
            Name = "Unlimited",
            Features = PlanFeature.All,
        };
    }
}
