using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting.Server;
using Serious.Abbot.Signals;

public class SystemSignalTests
{
    public class TheAllProperty
    {
        [Fact]
        public void IncludesExpectedSignals()
        {
            Assert.Equal(
                new[]
                {
                    "system:abbot:added",
                    "system:conversation:category:sentiment",
                    "system:conversation:category:state",
                    "system:conversation:category:topic",
                    "system:conversation:linked:ticket",
                    "system:conversation:linked:ticket:state",
                    "system:conversation:overdue",
                    "system:conversation:started",
                    "system:reaction:added",
                    "system:staff:test",
                },
                SystemSignal.All
                    .Select(s => s.Name)
                    .Order()
                    .ToArray()
            );
        }

        [Fact]
        public void MarksExpectedSignalsAsFromAI()
        {
            Assert.Equal(
                new[]
                {
                    "system:conversation:category:sentiment",
                    "system:conversation:category:state",
                    "system:conversation:category:topic",
                },
                SystemSignal.All
                    .Where(s => s.FromAI)
                    .Select(s => s.Name)
                    .ToArray()
            );
        }

        [Fact]
        public void MarksExpectedSignalsAsStaffOnly()
        {
            Assert.Equal(
                new[]
                {
                    "system:staff:test",
                },
                SystemSignal.All
                    .Where(s => s.StaffOnly)
                    .Select(s => s.Name)
                    .ToArray()
            );
        }
    }
}
