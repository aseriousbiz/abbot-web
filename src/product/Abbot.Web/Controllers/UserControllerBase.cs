using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using NodaTime;
using Serious.Abbot.Entities;
using Serious.Abbot.Extensions;
using Serious.Abbot.Infrastructure.Security;
using Serious.Abbot.Pages;
using Serious.AspNetCore.Turbo;

namespace Serious.Abbot.Controllers;

[Authorize(Policy = AuthorizationPolicies.RequireAnyRole)]
[AbbotWebHost]
public abstract class UserControllerBase : Controller
{
    protected static readonly DateTimeZone DefaultTimeZone =
        DateTimeZoneProviders.Tzdb.GetZoneOrNull(WebConstants.DefaultTimezoneId) ?? DateTimeZone.Utc;

    protected Member CurrentMember { get; private set; } = null!;
    protected Organization Organization => CurrentMember.Organization;

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        CurrentMember = HttpContext.RequireCurrentMember();
    }

    protected ObjectResult ProblemNotFound(string title, string detail)
    {
        return Problem(
            detail,
            HttpContext.Request.Path,
            StatusCodes.Status404NotFound,
            title);
    }

    protected ObjectResult ProblemBadArguments(string title, string detail)
    {
        return Problem(
            detail,
            HttpContext.Request.Path,
            StatusCodes.Status400BadRequest,
            title);
    }

    /// <summary>
    /// Displays a Status Message to the user as a turbo stream response.
    /// This means the page won't reload, but the status message will be displayed.
    /// </summary>
    /// <param name="message">The message to display</param>
    /// <param name="isError">Displays the message as an error.</param>
    protected TurboStreamViewResult TurboFlash(string message, bool isError = false)
    {
        message = isError && !message.StartsWith(WebConstants.ErrorStatusPrefix, StringComparison.Ordinal)
            ? WebConstants.ErrorStatusPrefix + message
            : message;
        return TurboReplace(AbbotPageModelBase.StatusMessageHost, PartialView("_StatusMessage", message));
    }

    /// <summary>
    /// Updates the content within the template tag to the container designated by the target dom id.
    /// </summary>
    /// <param name="target">The <see cref="DomId"/> of the element where the content of the element will be replaced.</param>
    /// <param name="partial">The content to replace the target element's content with.</param>
    protected TurboStreamViewResult TurboUpdate(DomId target, PartialViewResult partial)
    {
        return TurboStream(new PartialTurboStreamElement(TurboStreamAction.Update, target, partial));
    }

    /// <summary>
    /// Updates the content within the template tag to the container designated by the target dom id.
    /// </summary>
    /// <param name="target">The <see cref="DomId"/> of the element where the content of the element will be replaced.</param>
    /// <param name="viewName">The name of the partial view.</param>
    /// <param name="model">The model to pass to the partial.</param>
    protected TurboStreamViewResult TurboUpdate(DomId target, string viewName, object model)
        => TurboUpdate(target, PartialView(viewName, model));

    /// <summary>
    /// Updates the content within the template tag to the container designated by the target dom id.
    /// </summary>
    /// <param name="target">The <see cref="DomId"/> of the element where the content of the element will be replaced.</param>
    /// <param name="html">The encoded HTML to replace the target element's content with.</param>
    protected TurboStreamViewResult TurboUpdate(DomId target, string html)
    {
        return TurboStream(new ContentTurboStreamElement(TurboStreamAction.Update, target, html));
    }

    /// <summary>
    /// Replaces the element designated by the target dom id.
    /// </summary>
    /// <remarks>
    /// Note that if the content can be updated again, the new content should contain the target dom id since this
    /// replaces the entire element.
    /// </remarks>
    /// <param name="target">The <see cref="DomId"/> of the element to replace.</param>
    /// <param name="partial">The content to replace the target element with.</param>
    protected TurboStreamViewResult TurboReplace(DomId target, PartialViewResult partial)
    {
        return TurboStream(new PartialTurboStreamElement(TurboStreamAction.Replace, target, partial));
    }

    /// <summary>
    /// Replaces the element designated by the target dom id.
    /// </summary>
    /// <remarks>
    /// Note that if the content can be updated again, the new content should contain the target dom id since this
    /// replaces the entire element.
    /// </remarks>
    /// <param name="target">The <see cref="DomId"/> of the element to replace.</param>
    /// <param name="html">The encoded HTML to replace the target element with.</param>
    protected TurboStreamViewResult TurboReplace(DomId target, string html)
    {
        return TurboStream(new ContentTurboStreamElement(TurboStreamAction.Replace, target, html));
    }

    /// <summary>
    /// Appends the content within the template tag to the container designated by the target dom id.
    /// </summary>
    /// <param name="target">The <see cref="DomId"/> of the element where the content will be appended within.</param>
    /// <param name="partial">The content to replace the target element with.</param>
    protected TurboStreamViewResult TurboAppend(DomId target, PartialViewResult partial)
    {
        return TurboStream(new PartialTurboStreamElement(TurboStreamAction.Append, target, partial));
    }

    /// <summary>
    /// Appends the content within the template tag to the container designated by the target dom id.
    /// </summary>
    /// <param name="target">The <see cref="DomId"/> of the element where the content will be appended within.</param>
    /// <param name="html">The encoded HTML to replace the target element with.</param>
    protected TurboStreamViewResult TurboAppend(DomId target, string html)
    {
        return TurboStream(new ContentTurboStreamElement(TurboStreamAction.Append, target, html));
    }

    /// <summary>
    /// Removes the element designated by the target dom id.
    /// </summary>
    /// <param name="target">The <see cref="DomId"/> of the element to remove.</param>
    protected TurboStreamViewResult TurboRemove(DomId target)
    {
        return TurboStream(new TurboStreamElement(TurboStreamAction.Remove, target));
    }

    /// <summary>
    /// Updates multiple parts of the page with the elements from <paramref name="streamables"/>.
    /// </summary>
    /// <param name="streamables">The parts used to update the page.</param>
    protected TurboStreamViewResult TurboStream(params ITurboStreamable[] streamables)
    {
        return new TurboStreamViewResult(streamables, MetadataProvider, ViewData.ModelState);
    }

    protected ObjectResult Problem(ProblemDetails problemDetails)
    {
        // I don't know why ControllerBase.Problem doesn't have this override...
        return new ObjectResult(problemDetails)
        {
            StatusCode = problemDetails.Status ?? 500
        };
    }
}
