using System;
using System.ComponentModel.DataAnnotations;

namespace Serious.Abbot.Scripting;

/// <summary>
/// The type of chat platform.
/// </summary>
/// <remarks>
/// ALWAYS set an explicit integer value for each entry, and DO NOT change the existing ones, no matter how tempting it may be to reorder them for "consistency".
/// </remarks>
public enum PlatformType
{
    /// <summary>
    /// Unit test
    /// </summary>
    [Display(Name = "UnitTest")]
    UnitTest = 0,

    /// <summary>
    /// Slack
    /// </summary>
    Slack = 1,

    /// <summary>
    /// Discord
    /// </summary>
    [Obsolete("Discord is no longer supported.")]
    Discord = 2,

    /// <summary>
    /// Teams
    /// </summary>
    [Display(Name = "Teams")]
    [Obsolete("Teams is no longer supported.")]
    MsTeams = 3,
}
