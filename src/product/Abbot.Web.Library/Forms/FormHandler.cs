using System.Collections.Generic;
using System.Linq;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Messaging;
using Serious.Abbot.PayloadHandlers;
using Serious.Abbot.Repositories;
using Serious.Abbot.Skills;
using Serious.Slack;
using Serious.Slack.BlockKit;

namespace Serious.Abbot.Forms;

public class FormHandler : IHandler
{
    readonly IFormEngine _formEngine;
    readonly ISettingsManager _settingsManager;

    public FormHandler(IFormEngine formEngine, ISettingsManager settingsManager)
    {
        _formEngine = formEngine;
        _settingsManager = settingsManager;
    }

    public async Task OnMessageInteractionAsync(IPlatformMessage platformMessage)
    {
        // We should only show this to staff, but just in case, let's double-check
        if (!platformMessage.From.IsStaff())
        {
            return;
        }

        if (platformMessage.Payload.InteractionInfo is not { ActionElement: { } actionElement })
        {
            // Nothing to be done.
            return;
        }

        if (actionElement.ActionId is "test_form")
        {
            var formSettingName = platformMessage.Payload.InteractionInfo.Arguments;
            try
            {
                var setting =
                    await _settingsManager.GetAsync(SettingsScope.Member(platformMessage.From), formSettingName);

                if (setting is null)
                {
                    await platformMessage.Responder.SendActivityAsync("The test form has expired.");
                    return;
                }

                var definition = FormEngine.DeserializeFormDefinition(setting.Value);

                var blocks = _formEngine.TranslateForm(definition, CreateTestTemplateContext());
                var modal = new ViewUpdatePayload()
                {
                    Title = "Form Tester",
                    Submit = "Submit",
                    Close = "Close",
                    Blocks = blocks,
                    CallbackId = InteractionCallbackInfo.For<FormHandler>(),
                    PrivateMetadata = "test_form|" + formSettingName,
                };

                await platformMessage.Responder.OpenModalAsync(platformMessage.TriggerId.Require(), modal);
            }
            catch (InvalidFormFieldException iffex)
            {
                await platformMessage.Responder.OpenModalAsync(platformMessage.TriggerId.Require(),
                    AlertModal.Render(GenerateAlertMessage(iffex), "Form Invalid"));
            }
            catch (Exception ex)
            {
                await platformMessage.Responder.OpenModalAsync(platformMessage.TriggerId.Require(),
                    AlertModal.Render(ex.ToString(), "Error Opening Modal"));
            }
            finally
            {
                if (platformMessage.Payload.InteractionInfo.ResponseUrl is { } responseUrl)
                {
                    await platformMessage.Responder.DeleteActivityAsync(responseUrl);
                }
            }
        }
    }

    static string GenerateAlertMessage(InvalidFormFieldException iffex)
    {
        var messageContent = iffex.InnerException is null
            ? iffex.ToString()
            : iffex.InnerException.ToString();

        var message = $"```\n{messageContent}\n```";
        if (iffex.TemplateParameterName is { Length: > 0 })
        {
            message = $"*Template parameter*: {iffex.TemplateParameterName}\n{message}";
        }

        if (iffex.FieldId is { Length: > 0 })
        {
            message = $"*Field Id*: {iffex.FieldId}\n{message}";
        }

        return message;
    }

    public async Task OnSubmissionAsync(IViewContext<IViewSubmissionPayload> viewContext)
    {
        // We should only show this to staff, but just in case, let's double-check
        if (!viewContext.FromMember.IsStaff())
        {
            return;
        }

        if (viewContext.Payload.View.PrivateMetadata is null
            || !viewContext.Payload.View.PrivateMetadata.StartsWith("test_form|", StringComparison.Ordinal))
        {
            return;
        }

        var formSetting = viewContext.Payload.View.PrivateMetadata["test_form|".Length..];
        var setting = await _settingsManager.GetAsync(SettingsScope.Member(viewContext.FromMember), formSetting);
        if (setting is null)
        {
            viewContext.RespondByUpdatingView(AlertModal.Render("Form Expired",
                "The test form has expired, please try again."));

            return;
        }

        var definition = FormEngine.DeserializeFormDefinition(setting.Value);

        IReadOnlyDictionary<string, object?> results;
        try
        {
            results = _formEngine.ProcessFormSubmission(
                viewContext.Payload,
                definition,
                CreateTestTemplateContext());
        }
        catch (InvalidFormFieldException iffex)
        {
            viewContext.RespondByUpdatingView(AlertModal.Render(GenerateAlertMessage(iffex), "Form Invalid"));
            return;
        }

        var resultString = string.Join("\n", results.Select(x => $"*{x.Key}*: {FormatValue(x.Value)}"));

        // Update the modal with the results.
        var view = new ViewUpdatePayload()
        {
            CallbackId = InteractionCallbackInfo.For<FormHandler>(),
            Title = "Form Results",
            Close = "Close",
            Submit = null,
            Blocks = new List<ILayoutBlock>()
            {
                new Section("Form results"),
                new Section(new MrkdwnText(resultString)),
            }
        };

        viewContext.RespondByUpdatingView(view);

        static string FormatValue(object? value) =>
            value switch
            {
                null => "",
                IEnumerable<object> seq => "\n" + seq.Select(FormatValue).ToMarkdownList(),
                _ => $"`{value}`",
            };
    }

    static CreateTicketTemplateContext CreateTestTemplateContext()
    {
        return new CreateTicketTemplateContext(
            new(Id: 42,
                Url: "https://app.ab.bot/conversations/42",
                MessageUrl: "https://test.slack.com/message/1234",
                State: ConversationState.New,
                Title: "A Test Conversation Title",
                PlainTextTitle: "A Test Conversation Title",
                LastMessagePostedOn: DateTime.UtcNow.AddMinutes(-30),
                StartedBy: new(
                    Id: 12,
                    Active: true,
                    DisplayName: "Chatty McChatface",
                    Email: "chatty@example.com",
                    FormattedAddress: "123 Jump St.",
                    PlatformUserId: "U9999999999",
                    PlatformUrl: "https://test.slack.com/team/U9999999999",
                    TimeZoneId: "America/Vancouver",
                    Avatar: "https://example.com"
                )
            ),
            new(
                Id: 39,
                ManagedConversationsEnabled: true,
                Name: "cust-veridian-dynamics",
                PlatformRoomId: "C9999999999",
                Metadata: new Dictionary<string, string?> { { "customer-name", "Aloy" } },
                RoomType: RoomType.PublicChannel),
            new(
                Id: 190,
                Avatar: "https://example.com",
                Domain: "initech.slack.com",
                Name: "Initech",
                PlatformTeamId: "T9999999999",
                PlatformType: PlatformType.Slack),
            new(
                Id: 21,
                Active: true,
                DisplayName: "Clicky Buttonings",
                Email: "clicky@example.com",
                FormattedAddress: "12934 Main St.",
                PlatformUserId: "U8888888888",
                PlatformUrl: "https://test.slack.com/team/U8888888888",
                TimeZoneId: "America/New_York",
                Avatar: "https://example.com")
            );
    }
}
