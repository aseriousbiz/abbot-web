using Serious.Abbot.Pages;

namespace Serious.Abbot.Infrastructure.Security;

/// <summary>
/// Place this attribute on a non-GET Razor page handler in a <see cref="StaffViewablePage"/> to allow staff members to access it.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class AllowStaffAttribute : Attribute
{
}

/// <summary>
/// Place this attribute on a GET Razor page handler in a <see cref="StaffViewablePage"/> to forbid staff members from accessing it.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class ForbidStaffAttribute : Attribute
{
}
