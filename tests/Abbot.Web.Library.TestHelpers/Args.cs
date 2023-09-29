using Microsoft.Bot.Builder;
using NetTopologySuite.Geometries;
using NSubstitute;
using Serious.Abbot.Entities;
using Serious.Abbot.Events;
using Serious.Abbot.Messaging;

public static class Args
{
    public static ref CancellationToken CancellationToken => ref Arg.Any<CancellationToken>();
    public static ref DateTime DateTime => ref Arg.Any<DateTime>();
    public static ref string String => ref Arg.Any<string>();
    public static ref int Int32 => ref Arg.Any<int>();
    public static ref double Double => ref Arg.Any<double>();
    public static ref Point Point => ref Arg.Any<Point>();
    public static ref bool Boolean => ref Arg.Any<bool>();
    public static ref IPlatformMessage PlatformMessage => ref Arg.Any<IPlatformMessage>();
    public static ref IPlatformEvent PlatformEvent => ref Arg.Any<IPlatformEvent>();
    public static ref ITurnContext TurnContext => ref Arg.Any<ITurnContext>();
    public static ref MessageContext MessageContext => ref Arg.Any<MessageContext>();
    public static ref Organization Organization => ref Arg.Any<Organization>();
}
