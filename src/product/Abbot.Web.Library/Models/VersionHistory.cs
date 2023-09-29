using System;
using System.Collections.Generic;
using System.Linq;
using Serious.Abbot.Entities;

namespace Serious.Abbot.Models;

public class VersionHistory
{
    readonly Skill _skill;
    readonly List<SkillVersion> _versions;

    public VersionHistory(Skill skill)
    {
        _skill = skill;
        _versions = skill.Versions.OrderBy(v => v.Created).ToList();
    }

    public VersionHistoryItem GetVersionSnapshot(int version)
    {
        var snapshot = version < _versions.Count
            ? _versions[version - 1]
            : new SkillVersion
            {
                Created = _skill.Modified,
                Creator = _skill.Creator
            };
        var update = new SkillUpdateModel
        {
            Code = GetValue(version, s => s.Code) ?? _skill.Code,
            Name = GetValue(version, s => s.Name) ?? _skill.Name,
            Description = GetValue(version, s => s.Description) ?? _skill.Description,
            UsageText = GetValue(version, s => s.UsageText) ?? _skill.UsageText
        };
        return new VersionHistoryItem(_skill, update, snapshot);
    }

    // Since SkillVersion only stores the fields that have changed, we have to look back in the history
    // to find the value of these fields for this specified version.
    string? GetValue(int version, Func<SkillVersion, string?> propertyGetter)
    {
        if (version < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(version), $"Version must be between 1 and {_versions.Count}.");
        }
        return _versions.Skip(version - 1)
            .Select(propertyGetter)
            .FirstOrDefault(value => value is not null);
    }
}
