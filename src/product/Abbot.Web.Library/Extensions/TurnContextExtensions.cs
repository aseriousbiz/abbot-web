using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using Microsoft.Bot.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Serious.Abbot.Exceptions;
using Serious.Abbot.Infrastructure;
using Serious.Cryptography;
using Serious.Slack.BlockKit;
using Serious.Slack.BotFramework;

namespace Serious.Abbot.Extensions
{
    /// <summary>
    /// Extension methods to <see cref="ITurnContext{T}" />. This solves a need to pass information to our
    /// BotFramework middleware such as the current organization.
    /// </summary>
    public static class TurnContextExtensions
    {
        const string ApiTokenKey = "__APITOKEN__";

        /// <summary>
        /// Stores the API token in the turn context.
        /// </summary>
        /// <param name="turnContext">The context object for this turn.</param>
        /// <param name="apiToken">The API Token to store.</param>
        public static void SetApiToken(this ITurnContext turnContext, SecretString apiToken)
        {
            turnContext.TurnState.Set(ApiTokenKey, apiToken);
        }

        /// <summary>
        /// Try to retrieve the Api token from the turn context.
        /// </summary>
        /// <param name="turnContext">The context object for this turn.</param>
        /// <param name="apiToken">Populated with the Api Token if it exists.</param>
        /// <returns></returns>
        public static bool TryGetApiToken(this ITurnContext turnContext, [NotNullWhen(true)] out SecretString? apiToken)
        {
            if (turnContext.TurnState.TryGetValue(ApiTokenKey, out var apiTokenObject)
                && apiTokenObject is SecretString secretApiToken)
            {
                apiToken = secretApiToken;
                return true;
            }

            apiToken = null;
            return false;
        }

        public static async Task HandleUnhandledException(
            this ITurnContext turnContext,
            Exception exception,
            IConfiguration configuration,
            ILogger logger)
        {
            var (skill, platformId, endpoint) = exception is SkillRunException skillCallException
                ? (skillCallException.Skill, skillCallException.PlatformId, skillCallException.Endpoint)
                : (null, null, null);

            if (turnContext.TryGetSlackEventId(out var eventInfo))
            {
                var (slackEventId, envelopeType, teamId, eventDetails) = eventInfo;
                var eventDetailsJson = JsonConvert.SerializeObject(eventDetails, Formatting.Indented);

                if (skill is null)
                {
                    logger.ExceptionUnhandledOnTurnForSlack(
                        exception,
                        slackEventId,
                        envelopeType,
                        eventDetails.Type,
                        teamId,
                        eventDetailsJson);
                }
                else
                {
                    logger.ExceptionCallingSkillFromSlack(
                        exception,
                        skill.Id,
                        skill.Name,
                        slackEventId,
                        envelopeType,
                        teamId,
                        endpoint,
                        eventDetailsJson);
                }
            }
            else
            {
                if (skill is not null)
                {
                    // Non-slack logging.
                    logger.ExceptionCallingSkill(exception, skill.Name, skill.Id, platformId!, endpoint);
                }
                else
                {
                    logger.ExceptionUnhandledOnTurn(exception);
                }
            }

#if DEBUG
            if (exception.InnerException is HttpRequestException)
            {
                await turnContext.SendActivityAsync(
                    "Chances are, you haven't started up the skill runner service yet.");
                return;
            }
#endif

            if (ShowUnhandledErrorMessageEnabled(configuration))
            {
                try
                {
                    await turnContext.SendActivityAsync(WebConstants.UnexpectedBotErrorMessage,
                        new Section
                        {
                            Text = new MrkdwnText(WebConstants.UnexpectedBotErrorMessage),
                            Accessory = new ImageElement
                            {
                                ImageUrl = $"https://{WebConstants.DefaultHost}/img/abbot-avatar-error.png",
                                AltText = "Abbot Error"
                            }
                        });
                }
                catch (InvalidOperationException e) when (e.Message.Contains("Error: invalid_blocks", StringComparison.Ordinal))
                {
                    // This might occur if we use invalid blocks in our error message.
                    // We should never do that, but while developing, we might and this extra check comes in handy.
                    logger.ExceptionUnhandledOnTurn(e);
                    // If we can't send the message, then we're in a bad state.
                    // We can't do anything more to recover.
                    await turnContext.SendActivityAsync(WebConstants.UnexpectedBotErrorMessage);
                }
            }
        }

        static bool ShowUnhandledErrorMessageEnabled(IConfiguration configuration)
        {
            return bool.TryParse(configuration["ShowUnexpectedBotErrorMessage"], out var showUnhandledErrorMessage)
                   && showUnhandledErrorMessage;
        }
    }
}
