using System;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Models;

namespace Serious.Abbot.Entities;

[Owned]
public record TrialPlan(PlanType Plan, DateTime Expiry)
{
    public static readonly int TrialLengthDays = 14;
    public static readonly TimeSpan TrialLength = TimeSpan.FromDays(TrialLengthDays);
}
