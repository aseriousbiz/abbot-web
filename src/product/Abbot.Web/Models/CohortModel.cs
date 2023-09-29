using System;
using System.Collections.Generic;
using System.Linq;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Models;

public class CohortModel
{
    public static readonly CohortModel Empty = new CohortModel(new List<Cohort>(), string.Empty);

    public CohortModel(ICollection<Cohort> cohorts, string interval)
    {
        CohortInterval = interval;
        CohortIntervalCount = cohorts.Any() ? cohorts.Max(c => c.Number) : 0;
        Cohorts = cohorts.GroupBy(c => c.Date);
    }

    public string CohortInterval { get; }

    public int CohortIntervalCount { get; }

    public IEnumerable<IGrouping<DateTime, Cohort>> Cohorts { get; }
}
