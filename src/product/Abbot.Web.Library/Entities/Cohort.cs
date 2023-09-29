using System;
using Microsoft.EntityFrameworkCore;

namespace Serious.Abbot.Entities;

/// <summary>
/// Represents a cohort of users or organizations.
/// </summary>
[Keyless]
public class Cohort
{
    /// <summary>
    /// The date of the cohort. This is always the first day of a month.
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Size of the cohort.
    /// </summary>
    public int Size { get; set; }

    /// <summary>
    /// The cohort number.
    /// </summary>
    public int Number { get; set; }

    /// <summary>
    /// The percentage still active.
    /// </summary>
    public double Percentage { get; set; }
}
