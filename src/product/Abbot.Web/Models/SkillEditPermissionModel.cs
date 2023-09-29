using Serious.Abbot.Entities;

namespace Serious.Abbot.Models;

public class SkillEditPermissionModel
{
    public SkillEditPermissionModel(
        Skill skill,
        Capability capability,
        bool forceEdit,
        bool planAllowsPermissions)
    {
        bool isProtected = skill.Restricted;
        bool hasSourcePackage = skill.SourcePackageVersionId is not null;

        PlanAllowsPermissions = planAllowsPermissions;
        CanEditSkill = GetCanEditSkill(isProtected, capability);
        CanEditCode = GetCanEditCode(isProtected, CanEditSkill, forceEdit, hasSourcePackage);
        CanChangeRestricted = PlanAllowsPermissions && capability >= Capability.Admin;
        CanRunCode = capability >= Capability.Use;
    }

    public static bool GetCanEditSkill(bool isProtected, Capability capability)
    {
        return !isProtected || capability >= Capability.Edit;
    }

    public static bool GetCanEditCode(
        bool isProtected,
        bool canEditSkill,
        bool forceEdit,
        bool hasSourcePackage)
    {
        return canEditSkill && (!isProtected || forceEdit || !hasSourcePackage);
    }

    /// <summary>
    /// Whether or not the user may edit the code.
    /// </summary>
    public bool CanEditCode { get; }

    /// <summary>
    /// Whether or not the user may edit any part of the skill.
    /// </summary>
    public bool CanEditSkill { get; }

    /// <summary>
    /// Whether or not the user may change the restricted status of the skill.
    /// </summary>
    public bool CanChangeRestricted { get; }

    /// <summary>
    /// Whether or not the user may run the code in the console.
    /// </summary>
    public bool CanRunCode { get; }

    /// <summary>
    /// The organization is on a plan that allows permissions.
    /// </summary>
    public bool PlanAllowsPermissions { get; }
}
