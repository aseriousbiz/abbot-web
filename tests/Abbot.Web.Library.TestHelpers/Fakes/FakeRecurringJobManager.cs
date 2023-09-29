using System.Collections.Generic;
using Hangfire;
using Hangfire.Common;

namespace Serious.TestHelpers
{
    public class FakeRecurringJobManager : IRecurringJobManager
    {
        readonly List<string> _triggeredJobs = new();

        readonly Dictionary<string, (Job Job, string Schedule)> _recurringJobs = new();

        public void AddOrUpdate(string recurringJobId, Job job, string cronExpression, RecurringJobOptions options)
        {
            _recurringJobs[recurringJobId] = (job, cronExpression);
        }

        public void Trigger(string recurringJobId)
        {
            _triggeredJobs.Add(recurringJobId);
        }

        public void RemoveIfExists(string recurringJobId)
        {
            if (_recurringJobs.ContainsKey(recurringJobId))
            {
                _recurringJobs.Remove(recurringJobId);
            }
        }

        public IReadOnlyList<string> TriggeredJobs => _triggeredJobs;

        public IReadOnlyDictionary<string, (Job Job, string Schedule)> RecurringJobs => _recurringJobs;
    }
}
