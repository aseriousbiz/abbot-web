using System;
using Microsoft.AspNetCore.Http;

namespace Serious.Abbot.Security;

/// <summary>
/// The "Staff Mode" HTTP Context Feature manages how the site appears to staff users.
/// </summary>
/// <remarks>
/// Staff mode is enabled when all of the below conditions are met:
/// <list type="number">
/// <item>
/// <description>The user has the "Staff" role.</description>
/// </item>
/// <item>
/// <description>The user has not explicitly disabled staff mode for this browser session.</description>
/// </item>
/// </list>
/// </remarks>
public class StaffModeFeature
{
    const string CookieName = "staffmode";
    static readonly CookieOptions CookieOptions = new()
    {
        HttpOnly = true,
        Secure = true
    };
    readonly HttpContext _context;
    readonly Lazy<StaffModeState> _state;

    /// <summary>
    /// Indicates if the current request is "in staff mode", which means staff-only information and controls can be displayed.
    /// If this is <c>false</c>, it does not mean the user is not staff, just that they are not in staff mode for some reason.
    /// </summary>
    public bool IsStaffMode => _state.Value.Enabled;

    /// <summary>
    /// Indicates if the current user is a staff user, which does NOT imply they should be shown staff-only information and controls.
    /// </summary>
    public bool IsStaffUser => _context.User.IsInRole(Roles.Staff);

    /// <summary>
    /// Gets the reason staff-mode is for this user, if any.
    /// Only relevant is <see cref="IsStaffUser"/> is true and <see cref="IsStaffMode"/> is false.
    /// </summary>
    public string? DisabledReason => _state.Value.Reason;

    public StaffModeFeature(HttpContext context)
    {
        _context = context;
        _state = new Lazy<StaffModeState>(LoadStaffModeState);
    }

    /// <summary>
    /// Sets the staff mode preference for the current browser session.
    /// </summary>
    /// <param name="enabled">If <c>true</c>, the user prefers to be in staff mode. If <c>false</c>, they do not.</param>
    public void SetStaffModePreference(bool enabled)
    {
        // No point of being here if the user isn't eligible for staff mode.
        if (!IsStaffUser)
        {
            return;
        }

        // Restoring Staff Mode.
        if (enabled && _context.Request.Cookies[CookieName] is { Length: > 0 })
        {
            _context.Response.Cookies.Delete(CookieName, CookieOptions);
        }
        else if (!enabled)
        {
            _context.Response.Cookies.Append(CookieName, "disabled", CookieOptions);
        }

        // The remaining case doesn't matter.
        // It just means enabled was true and the cookie to disable staff mode doesn't exist.
    }

    StaffModeState LoadStaffModeState()
    {
        if (!_context.User.IsInRole(Roles.Staff))
        {
            return new StaffModeState(false, "The user is not staff.");
        }

        var staffModeCookie = _context.Request.Cookies[CookieName];
        if (staffModeCookie is { Length: > 0 })
        {
            return new StaffModeState(false, "Staff mode has been disabled by the user.");
        }

        // If we want, we can add additional checks like requiring certain IP addresses, client credentials, etc.

        return new StaffModeState(true, null);
    }

    record StaffModeState(bool Enabled, string? Reason);
}
