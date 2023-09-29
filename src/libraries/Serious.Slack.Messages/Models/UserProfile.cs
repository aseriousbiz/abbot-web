using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Serious.Slack;

/// <summary>
/// Information about a user's profile. This might be from the <c>user_change</c> event or a result of a
/// call to the <c>users.profile.get</c> endpoint.
/// </summary>
/// <param name="Team">The team the user belongs to.</param>
/// <param name="Title">A title if one is set for the user.</param>
/// <param name="Phone">Phone number for the user.</param>
/// <param name="DisplayName">Display name for the user.</param>
/// <param name="DisplayNameNormalized">Normalized display name.</param>
/// <param name="RealName">The real name of the user.</param>
/// <param name="RealNameNormalized">The normalized real name of the user.</param>
/// <param name="StatusText">The status text for the user.</param>
/// <param name="StatusEmoji">The status emoji for the user.</param>
/// <param name="AvatarHash">The avatar hash for the user.</param>
/// <param name="ImageOriginal">The original image used to represent the user.</param>
/// <param name="IsCustomImage">Whether or not the user has a custom avatar image.</param>
/// <param name="Email">Email address of the user.</param>
/// <param name="FirstName">The first name of the user.</param>
/// <param name="LastName">The last name of the user.</param>
/// <param name="Image24">The 24x24 pixel square image used to represent the user.</param>
/// <param name="Image32">The 32x32 pixel square image used to represent the user.</param>
/// <param name="Image48">The 48x48 pixel square image used to represent the user.</param>
/// <param name="Image72">The 72x72 pixel square image used to represent the user.</param>
/// <param name="Image192">The 192x192 pixel square image used to represent the user.</param>
/// <param name="Image512">The 512x512 pixel square image used to represent the user.</param>
/// <param name="Image1024">The 1024x1024 pixel square image used to represent the user.</param>
/// <param name="StatusTextCanonical">The canonical status text.</param>
/// <param name="IsRestricted">If <c>true</c>, then this is a guest user.</param>
/// <param name="IsUltraRestricted">If <c>true</c>, then this is a single-channel guest user.</param>
/// <param name="Fields">A dictionary of custom fields from this user's profile.</param>
/// <remarks>
/// See <see href="https://api.slack.com/methods/users.profile.get"/> for more info.
/// </remarks>
public record UserProfile(
    string? Team,

    [property:JsonProperty("title")]
    [property:JsonPropertyName("title")]
    string? Title,

    [property:JsonProperty("phone")]
    [property:JsonPropertyName("phone")]
    string? Phone,

    [property:JsonProperty("display_name")]
    [property:JsonPropertyName("display_name")]
    string? DisplayName,

    [property:JsonProperty("display_name_normalized")]
    [property:JsonPropertyName("display_name_normalized")]
    string? DisplayNameNormalized,

    string? RealName,

    [property:JsonProperty("real_name_normalized")]
    [property:JsonPropertyName("real_name_normalized")]
    string? RealNameNormalized,

    [property:JsonProperty("status_text")]
    [property:JsonPropertyName("status_text")]
    string? StatusText,

    [property:JsonProperty("status_emoji")]
    [property:JsonPropertyName("status_emoji")]
    string? StatusEmoji,

    string? AvatarHash,

    [property:JsonProperty("image_original")]
    [property:JsonPropertyName("image_original")]
    string? ImageOriginal,

    [property:JsonProperty("is_custom_image")]
    [property:JsonPropertyName("is_custom_image")]
    bool IsCustomImage,

    [property:JsonProperty("email")]
    [property:JsonPropertyName("email")]
    string? Email,

    string? FirstName,

    [property:JsonProperty("last_name")]
    [property:JsonPropertyName("last_name")]
    string? LastName,

    [property:JsonProperty("image_24")]
    [property:JsonPropertyName("image_24")]
    string? Image24,

    [property:JsonProperty("image_32")]
    [property:JsonPropertyName("image_32")]
    string? Image32,

    [property:JsonProperty("image_48")]
    [property:JsonPropertyName("image_48")]
    string? Image48,

    string? Image72,

    [property:JsonProperty("image_192")]
    [property:JsonPropertyName("image_192")]
    string? Image192,

    [property:JsonProperty("image_512")]
    [property:JsonPropertyName("image_512")]
    string? Image512,

    [property:JsonProperty("image_1024")]
    [property:JsonPropertyName("image_1024")]
    string? Image1024,

    [property:JsonProperty("status_text_canonical")]
    [property:JsonPropertyName("status_text_canonical")]
    string? StatusTextCanonical,

    bool IsRestricted,

    bool IsUltraRestricted,

    [property:JsonProperty("fields")]
    [property:JsonPropertyName("fields")]
    IDictionary<string, UserProfileValue>? Fields) : UserProfileMetadata(Team, FirstName, RealName, AvatarHash, Image72, IsRestricted, IsUltraRestricted)
{
    /// <summary>
    /// Default ctor.
    /// </summary>
    public UserProfile() : this(
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        false,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        false,
        false,
        null)
    {
    }
}

/// <summary>
/// A subset of a user's full profile that's included when <c>include_all_metadata</c> is <c>true</c> in a call to
/// <c>conversations.history</c>.
/// </summary>
/// <param name="Team">The team the user belongs to.</param>
/// <param name="FirstName">The first name of the user.</param>
/// <param name="RealName">The real name of the user.</param>
/// <param name="AvatarHash">The avatar hash for the user.</param>
/// <param name="Image72">The 72x72 pixel square image used to represent the user.</param>
/// <param name="IsRestricted">If <c>true</c>, then this is a guest user.</param>
/// <param name="IsUltraRestricted">If <c>true</c>, then this is a single-channel guest user.</param>
public record UserProfileMetadata(
    [property:JsonProperty("team")]
    [property:JsonPropertyName("team")]
    string? Team,

    [property:JsonProperty("first_name")]
    [property:JsonPropertyName("first_name")]
    string? FirstName,

    [property:JsonProperty("real_name")]
    [property:JsonPropertyName("real_name")]
    string? RealName,

    [property:JsonProperty("avatar_hash")]
    [property:JsonPropertyName("avatar_hash")]
    string? AvatarHash,

    [property:JsonProperty("image_72")]
    [property:JsonPropertyName("image_72")]
    string? Image72,

    [property:JsonProperty("is_restricted")]
    [property:JsonPropertyName("is_restricted")]
    bool IsRestricted,

    [property:JsonProperty("is_ultra_restricted")]
    [property:JsonPropertyName("is_ultra_restricted")]
    bool IsUltraRestricted);

/// <summary>
/// Represents the value of a custom user profile field.
/// </summary>
public record UserProfileValue
{
    /// <summary>
    /// The value of the field.
    /// </summary>
    public string? Value { get; init; }

    /// <summary>
    /// The alternate value (used for display purposes) of the field.
    /// If this is non-null, it should be used when displaying this field value to users.
    /// </summary>
    public string? Alt { get; init; }
}
