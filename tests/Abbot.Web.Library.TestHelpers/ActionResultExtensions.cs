using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Serious;
using Serious.Abbot;
using Serious.Abbot.Pages;
using Serious.AspNetCore.Turbo;
using Xunit;

namespace Abbot.Common.TestHelpers;

public static class ActionResultExtensions
{
    public static void AssertTurboFlashMessage(this IActionResult? result, string expectedStatusMessage, bool isError = false)
    {
        var statusMessage = result.AssertTurboPartialResultModelType<string>(
            AbbotPageModelBase.StatusMessageHost,
            "_StatusMessage",
            TurboStreamAction.Replace);

        expectedStatusMessage = isError && !expectedStatusMessage.StartsWith(WebConstants.ErrorStatusPrefix)
            ? WebConstants.ErrorStatusPrefix + expectedStatusMessage
            : expectedStatusMessage;
        Assert.Equal(expectedStatusMessage, statusMessage);
    }

    public static TTurboStreamElement AssertTurboStreamElementIsType<TTurboStreamElement>(
        this IActionResult? result,
        DomId expectedDomId,
        TurboStreamAction expectedTurboStreamAction) where TTurboStreamElement : TurboStreamElement
    {
        Assert.NotNull(result);
        var turboStreamElements = result.GetTurboStreamElements();
        var turboStreamElement = turboStreamElements.FirstOrDefault(x => x.Target == expectedDomId);
        Expect.True(turboStreamElement is not null, $"Could not find TurboStreamElement with Target {expectedDomId}");

        Assert.Equal(expectedTurboStreamAction, turboStreamElement.Action);
        return Assert.IsType<TTurboStreamElement>(turboStreamElement);
    }

    public static object? AssertTurboPartialResult(
        this IActionResult? result,
        DomId expectedDomId,
        string expectedPartialName,
        TurboStreamAction expectedTurboStreamAction = TurboStreamAction.Update)
    {
        var partialStreamElement = result.AssertTurboStreamElementIsType<PartialTurboStreamElement>(
            expectedDomId,
            expectedTurboStreamAction);

        Assert.Equal(expectedPartialName, partialStreamElement.Partial.ViewName);
        return partialStreamElement.Partial.Model;
    }

    public static TPartialModelType AssertTurboPartialResultModelType<TPartialModelType>(
        this IActionResult? result,
        DomId expectedDomId,
        string expectedPartialName,
        TurboStreamAction expectedTurboStreamAction = TurboStreamAction.Update)
    {
        var model = result.AssertTurboPartialResult(expectedDomId,
            expectedPartialName,
            expectedTurboStreamAction);
        return Assert.IsType<TPartialModelType>(model);
    }

    public static IEnumerable<TurboStreamElement> GetTurboStreamElements(this IActionResult result)
    {
        var partial = Assert.IsType<TurboStreamViewResult>(result);
        var turboStream = Assert.IsType<TurboStream>(partial.Model);
        return turboStream.Elements;
    }
}
