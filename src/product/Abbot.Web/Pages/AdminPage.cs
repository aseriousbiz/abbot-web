using System.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Serious.Abbot.Models;
using Serious.Abbot.Repositories;
using Serious.Abbot.Telemetry;
using Serious.AspNetCore;
using Serious.Cryptography;

namespace Serious.Abbot.Pages;

/// <summary>
/// Base class for most Admin/Settings pages.
/// </summary>
public abstract class AdminPage : UserPage
{
    const string ImageValidationSeed = "!mS5JA@bx9Q#uzw==";

    protected IOrganizationRepository OrganizationRepository { get; }
    protected IAuditLog AuditLog { get; }

    protected AdminPage(
        IOrganizationRepository organizationRepository,
        IAuditLog auditLog)
    {
        OrganizationRepository = organizationRepository;
        AuditLog = auditLog;
    }

    protected async Task<IActionResult> SaveAvatarAsync(
        FileInputModel inputModel,
        string avatarType,
        Action<string> avatarSetter)
    {
        if (inputModel.Url != Organization.Avatar && inputModel.Url is not null)
        {
            // Make sure user isn't messing with form.
            var checksum = inputModel.Url.ComputeHMACSHA256Hash(ImageValidationSeed);
            if (!checksum.Equals(inputModel.Checksum, StringComparison.Ordinal))
            {
                StatusMessage = $"{WebConstants.ErrorStatusPrefix}Invalid Form Modification Detected.";
                return RedirectToPage();
            }

            avatarSetter(inputModel.Url);
            await OrganizationRepository.SaveChangesAsync();
            await AuditLog.LogOrganizationAvatarChanged(Viewer.User, avatarType, Organization);
            StatusMessage = $"{avatarType} Avatar updated!";
        }
        else
        {
            StatusMessage = $"No change to the {avatarType} avatar";
        }

        return RedirectToPage();
    }

    protected async Task<IActionResult> UploadAvatarAsync(IFormFile formFile, Func<string, Stream, Task<Uri>> uploadMethod)
    {
        if (formFile.Length > 50000)
        {
            return new JsonResult(new { error = "Please choose an image 50kb or smaller." });
        }

        var fileStream = formFile.OpenReadStream();
        if (!UploadHelpers.IsValidFileExtensionAndSignature(formFile.FileName, fileStream,
                new[] { ".png", ".jpg", ".jpeg" }))
        {
            return new JsonResult(new { error = "Only png, jpg, and jpeg files are allowed." });
        }

        var avatarUrl = await uploadMethod(Organization.PlatformId, fileStream);

        return new JsonResult(new {
            url = avatarUrl,
            checksum = avatarUrl.ToString().ComputeHMACSHA256Hash(ImageValidationSeed)
        });
    }
}
