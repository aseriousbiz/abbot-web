using System.Diagnostics;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Serious.Abbot.Eventing.Infrastructure;

// A marker just for logger categories and ILogger<T> extension methods
// ReSharper disable once ClassNeverInstantiated.Global
public class DiagnosticSendPublishFilter
{
}

public class DiagnosticSendPublishFilter<TMessage> : IFilter<SendContext<TMessage>>, IFilter<PublishContext<TMessage>>
    where TMessage : class
{
    readonly ILogger<DiagnosticSendPublishFilter> _logger;

    public DiagnosticSendPublishFilter(ILogger<DiagnosticSendPublishFilter> logger)
    {
        _logger = logger;
    }

    public async Task Send(SendContext<TMessage> context, IPipe<SendContext<TMessage>> next)
    {
        _logger.SendingMessage(context.SourceAddress, context.DestinationAddress);

        try
        {
            await next.Send(context);
        }
        catch (Exception ex)
        {
            _logger.SendFault(ex);
            throw;
        }
        finally
        {
            _logger.SentMessage(context.SourceAddress, context.DestinationAddress);
        }
    }

    public async Task Send(PublishContext<TMessage> context, IPipe<PublishContext<TMessage>> next)
    {
        _logger.PublishingMessage(context.SourceAddress, context.DestinationAddress);

        try
        {
            await next.Send(context);
        }
        catch (Exception ex)
        {
            _logger.PublishFault(ex);
            throw;
        }
        finally
        {
            _logger.PublishedMessage(context.SourceAddress, context.DestinationAddress);
        }
    }

    public void Probe(ProbeContext context)
    {
    }
}

static partial class DiagnosticSendPublishFilterLoggerExtensions
{
    [LoggerMessage(
        EventId = 4,
        Level = LogLevel.Debug,
        Message = "Sending message from {SourceAddress} to {DestinationAddress}")]
    public static partial void SendingMessage(this ILogger<DiagnosticSendPublishFilter> logger, Uri? sourceAddress, Uri? destinationAddress);

    [LoggerMessage(
        EventId = 5,
        Level = LogLevel.Information,
        Message = "Sent message from {SourceAddress} to {DestinationAddress}")]
    public static partial void SentMessage(this ILogger<DiagnosticSendPublishFilter> logger, Uri? sourceAddress, Uri? destinationAddress);

    [LoggerMessage(
        EventId = 6,
        Level = LogLevel.Error,
        Message = "Send fault")]
    public static partial void SendFault(this ILogger<DiagnosticSendPublishFilter> logger, Exception ex);

    [LoggerMessage(
        EventId = 7,
        Level = LogLevel.Debug,
        Message = "Publishing message from {SourceAddress} to {DestinationAddress}")]
    public static partial void PublishingMessage(this ILogger<DiagnosticSendPublishFilter> logger, Uri? sourceAddress, Uri? destinationAddress);

    [LoggerMessage(
        EventId = 8,
        Level = LogLevel.Information,
        Message = "Published message from {SourceAddress} to {DestinationAddress}")]
    public static partial void PublishedMessage(this ILogger<DiagnosticSendPublishFilter> logger, Uri? sourceAddress, Uri? destinationAddress);

    [LoggerMessage(
        EventId = 9,
        Level = LogLevel.Error,
        Message = "Publish fault")]
    public static partial void PublishFault(this ILogger<DiagnosticSendPublishFilter> logger, Exception ex);
}
