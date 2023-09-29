using System.Linq;
using System.Threading.Tasks;
using Serious.Abbot.Entities;
using Serious.Abbot.Models;

namespace Serious.Abbot.Repositories;

/// <summary>
/// The set of skill packages.
/// </summary>
public interface IPackageRepository : IOrganizationScopedRepository<Package>
{
    /// <summary>
    /// Retrieves the skill package, if any, for the specified skill.
    /// </summary>
    /// <param name="skill">The skill to retrieve a package for.</param>
    ValueTask<Package?> GetAsync(Skill skill);

    /// <summary>
    /// Retrieves the skill package by Id. This is used when installing a package.
    /// </summary>
    /// <param name="id">The id of the package in the database.</param>
    ValueTask<Package?> GetAsync(int id);

    /// <summary>
    /// Retrieves the skill package by organization slug and package name. This is used by the package directory
    /// details page for a package.
    /// </summary>
    ValueTask<Package?> GetDetailsAsync(string organizationSlug, string name);

    /// <summary>
    /// Get a queryable of all skill packages in the system. This allows callers to apply additional
    /// includes or filtering.
    /// </summary>
    IQueryable<Package> GetQueryable();

    /// <summary>
    /// Creates a package with the specified information.
    /// </summary>
    /// <param name="createModel">Encapsulates the changes to this skill package</param>
    /// <param name="skill">The skill to publish as a package.</param>
    /// <param name="user">The user creating this package.</param>
    ValueTask<Package> CreateAsync(PackageCreateModel createModel, Skill skill, User user);

    /// <summary>
    /// Creates a new version of the package.
    /// </summary>
    /// <param name="updateModel">Encapsulates the changes to this skill package</param>
    /// <param name="package">The existing package</param>
    /// <param name="skill">The skill being published</param>
    /// <param name="user">The user creating this package.</param>
    ValueTask<PackageVersion> PublishNewVersionAsync(
        PackageUpdateModel updateModel,
        Package package,
        Skill skill,
        User user);

    /// <summary>
    /// Update the package metadata without publishing a new vesion. 
    /// </summary>
    /// <param name="package">The package to update.</param>
    /// <param name="readme">The new readme for the package.</param>
    /// <param name="listed">The new listed state.</param>
    /// <param name="user">The user updating this package.</param>
    Task UpdatePackageMetadataAsync(Package package, string readme, bool listed, User user);
}
