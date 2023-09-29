using System;
using Microsoft.FeatureManagement.FeatureFilters;
using Serious.Abbot.Entities;
using Serious.Abbot.FeatureManagement;

namespace Serious.TestHelpers;

public class FakeFeatureActor : IFeatureActor
{
    readonly TargetingContext _targetingContext;

    public FakeFeatureActor() : this(null, Array.Empty<string>())
    {
    }

    public FakeFeatureActor(string? userId, params string[] groups)
        : this(new TargetingContext { UserId = userId, Groups = groups })
    {
    }

    public FakeFeatureActor(string userId, Organization organization)
        : this(FeatureHelper.CreateTargetingContext(organization, userId))
    {
    }

    public FakeFeatureActor(TargetingContext targetingContext)
    {
        _targetingContext = targetingContext;
    }

    public TargetingContext GetTargetingContext() => _targetingContext;
}
