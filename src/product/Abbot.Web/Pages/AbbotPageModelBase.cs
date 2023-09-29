using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Serious.AspNetCore;
using Serious.AspNetCore.Turbo;

namespace Serious.Abbot.Pages;

public abstract class AbbotPageModelBase : PageModel
{
    public static readonly DomId StatusMessageHost = new("status-message-host");

    // We only store non-identifying temporary status messages here, no PII.
    [TempData]
    public string? StatusMessage { get; set; }

    public IActionResult RedirectWithStatusMessage(string statusMessage, string? pageName = null, object? routeValues = null)
    {
        StatusMessage = statusMessage;
        return RedirectToPage(pageName, routeValues);
    }

    public IActionResult? RedirectToReturnUrl()
    {
        var returnUrl = Url.GetReturnUrl();
        return returnUrl is null ? null : Redirect(returnUrl);
    }

    public virtual string? StaffPageUrl() => null;

    public override PageResult Page()
    {
        if (Request.IsTurboRequest() && HttpContext.Request.Method != HttpMethods.Get)
        {
            // Turbo wants a 422 response when a form submission fails validation.
            if (!ModelState.IsValid)
            {
                return PageWithStatus(StatusCodes.Status422UnprocessableEntity);
            }

            throw new UnreachableException("Don't use 'Page' method in form submissions!");
        }

        return base.Page();
    }

    public PageResult PageWithStatus(int statusCode)
    {
        return new PageResult { StatusCode = statusCode };
    }

    /// <summary>
    /// Replaces page URL (based on the specified route parameters) in the browser history.
    /// </summary>
    /// <param name="pageName">The name of the page to replace the location address with.</param>
    /// <param name="values">The route values to redirect to</param>
    protected TurboStreamViewResult TurboPageLocation(string? pageName, object? values = null)
    {
        var url = Url.Page(pageName, values) ?? "";
        return TurboStream(new ContentTurboStreamElement(TurboStreamAction.Location, new DomId("replace"), url));
    }

    /// <summary>
    /// Replaces page URL (based on the specified route parameters) in the browser history.
    /// </summary>
    /// <param name="values">The route values to redirect to</param>
    protected TurboStreamViewResult TurboPageLocation(object? values) => TurboPageLocation(null, values);

    /// <summary>
    /// Refreshes the default values of the form with the specified id. These values are used to track changes to
    /// the form and to determine if the form has been modified.
    /// </summary>
    /// <param name="formId">The Dom ID of the form to update.</param>
    protected TurboStreamViewResult TurboUpdateFormDefaults(DomId formId)
    {
        return TurboStream(new ContentTurboStreamElement(TurboStreamAction.Defaults, formId, ""));
    }

    /// <summary>
    /// Displays a Status Message to the user as a turbo stream response.
    /// This means the page won't reload, but the status message will be displayed.
    /// </summary>
    /// <param name="message">The message to display</param>
    /// <param name="isError">Displays the message as an error.</param>
    protected TurboStreamViewResult TurboFlash(string message, bool isError = false)
        => TurboFlash(StatusMessageHost, message, isError);

    /// <summary>
    /// Displays a Status Message to the user as a turbo stream response. THis overloads the domId for use with
    /// alternative status messages that occur closer to the action.
    /// This means the page won't reload, but the status message will be displayed.
    /// </summary>
    /// <param name="domId">The <see cref="DomId"/> of the element to use for this status.</param>
    /// <param name="message">The message to display</param>
    /// <param name="isError">Displays the message as an error.</param>
    protected TurboStreamViewResult TurboFlash(DomId domId, string message, bool isError = false)
    {
        message = isError && !message.StartsWith(WebConstants.ErrorStatusPrefix, StringComparison.Ordinal)
            ? WebConstants.ErrorStatusPrefix + message
            : message;
        return TurboReplace(domId, Partial("_StatusMessage", message));
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
    /// <param name="partialName">The name of the partial to replace the target element's content with.</param>
    /// <param name="model">The model to pass to the partial.</param>
    protected TurboStreamViewResult TurboUpdate(DomId target, string partialName, object model)
    {
        return TurboStream(new PartialTurboStreamElement(TurboStreamAction.Update, target, Partial(partialName, model)));
    }

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
    protected TurboStreamViewResult TurboStream(params ITurboStreamable?[] streamables)
    {
        return new(streamables, MetadataProvider, ViewData.ModelState);
    }

    /// <summary>
    /// This returns a dictionary of all current route values combined with query string values.
    /// This makes it easy to generate links to the current page with different query string values
    /// by using this in asp-all-route-data. Just make sure to set asp-route-* after asp-all-route-data.
    /// </summary>
    /// <remarks>
    /// Note that this doesn't work for cases where there's more than one value for a query string key.
    /// </remarks>
    public IDictionary<string, string> CurrentRouteData => PageContext.GetCurrentRouteValues();
}
