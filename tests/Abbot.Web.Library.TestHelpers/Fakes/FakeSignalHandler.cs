using Serious.Abbot.Entities;
using Serious.Abbot.Messages;
using Serious.Abbot.Models;
using Serious.Abbot.Signals;

namespace Serious.TestHelpers
{
    public class FakeSystemSignaler : ISystemSignaler
    {
        readonly ISignalHandler _signalHandler;

        public FakeSystemSignaler(ISignalHandler signalHandler)
        {
            _signalHandler = signalHandler;
        }

        public void EnqueueSystemSignal(
            SystemSignal signal,
            string arguments,
            Id<Organization> organizationId,
            PlatformRoom room,
            Member actor,
            MessageInfo? triggeringMessage)
        {
            if (_signalHandler is FakeSignalHandler fakeSignalHandler)
            {
                fakeSignalHandler.EnqueueSystemSignal(signal, arguments, organizationId, room, actor, triggeringMessage);
            }
        }
    }

    public class FakeSignalHandler : ISignalHandler
    {
        public bool CycleDetected { get; set; }
        public IList<RaisedSignal> RaisedSignals { get; } = new List<RaisedSignal>();

        public bool EnqueueSignalHandling(Id<Skill> id, SignalRequest signalRequest)
        {
            RaisedSignals.Add(new RaisedSignal(signalRequest.Name, signalRequest.Arguments, signalRequest.Room.Id, signalRequest.SenderId, null));
            return !CycleDetected;
        }

        public void EnqueueSystemSignal(
            SystemSignal signal,
            string arguments,
            Id<Organization> organizationId,
            PlatformRoom room,
            Member actor,
            MessageInfo? triggeringMessage)
        {
            RaisedSignals.Add(new RaisedSignal(signal.Name, arguments, room.Id, actor.Id, triggeringMessage));
        }

        public record RaisedSignal(
            string Name,
            string Arguments,
            string PlatformRoomId,
            int? SenderId,
            MessageInfo? TriggeringMessage);

        /// <summary>
        /// Asserts that a signal was raised with the given name, arguments and other parameters.
        /// </summary>
        /// <param name="signalName">The name of the signal that should have been raised.</param>
        /// <param name="arguments">The arguments that signal should have been raised with.</param>
        /// <param name="platformRoomId">The platform-specific ID of the room in which the signal should have been raised.</param>
        /// <param name="sender">The sender of the signal, or 'null' if Abbot should be the sender.</param>
        /// <param name="triggeringMessage">The message that triggered the signal, if any.</param>
        public void AssertRaised(
            string signalName,
            string arguments,
            string platformRoomId,
            Id<Member>? sender,
            MessageInfo? triggeringMessage)
        {
            // First, make sure signalName exists among raised signals
            Assert.Contains(signalName, RaisedSignals.Select(rs => rs.Name).Distinct());

            // Assume only one with the expected Name
            var i = Assert.Single(RaisedSignals.Where(rs => rs.Name == signalName));

            // Now we get a useful failure if Arguments don't match
            Assert.Equal(arguments, i.Arguments);
            Assert.Same(platformRoomId, i.PlatformRoomId);
            if (triggeringMessage is not null)
            {
                Assert.Equal(triggeringMessage, i.TriggeringMessage);
            }
            else
            {
                Assert.Null(i.TriggeringMessage);
            }
            Assert.Equal(sender, i.SenderId);
        }

        /// <summary>
        /// Asserts that no signals of the given name were raised.
        /// </summary>
        /// <param name="name">The name of the signal that should not have been raised.</param>
        public void AssertNotRaised(string name)
        {
            Assert.Empty(RaisedSignals.Where(r => r.Name == name));
        }
    }
}
