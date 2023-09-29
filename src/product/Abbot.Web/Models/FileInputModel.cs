using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Serious.Abbot.Models;

// Input model for file uploads.
public class FileInputModel
{
    public string SavePageHandler { get; set; } = null!;

    public string Prefix { get; set; } = null!;

    public string? Url { get; set; }

    /// <summary>
    /// Prevent tampering with the Avatar.
    /// </summary>
    public string Checksum { get; set; } = null!;

    [Display(Name = "File")]
    public IFormFile File { get; set; } = null!;
}
