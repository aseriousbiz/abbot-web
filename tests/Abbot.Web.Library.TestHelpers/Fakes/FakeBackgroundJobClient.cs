using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Xunit;

namespace Serious.TestHelpers
{
    public class FakeBackgroundJobClient : IBackgroundJobClient
    {
        readonly List<EnqueuedJob> _enqueued = new();

        public string Create(Job job, IState state)
        {
            var id = Guid.NewGuid().ToString();
            _enqueued.Add(new EnqueuedJob(job, state, id));
            return id;
        }

        public bool ChangeState(string jobId, IState state, string expectedState)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<EnqueuedJob> EnqueuedJobs => _enqueued;

        public IState DidEnqueue(Expression<Action> methodCall)
        {
            var job = Job.FromExpression(methodCall);
            return DidEnqueue(job);
        }

        public IState DidEnqueue<TService>(Expression<Action<TService>> methodCall)
        {
            var job = Job.FromExpression(methodCall);
            return DidEnqueue(job);
        }

        public IState DidEnqueue(Expression<Func<Task>> methodCall)
        {
            var job = Job.FromExpression(methodCall);
            return DidEnqueue(job);
        }

        public IState DidEnqueue<TService>(Expression<Func<TService, Task>> methodCall)
        {
            var job = Job.FromExpression(methodCall);
            return DidEnqueue(job);
        }

        public IState DidEnqueue(Job job)
        {
            var enqueuedByJob = _enqueued
                .GroupBy(js => new JobKey(js.Job))
                .ToDictionary(g => g.Key, g => g.ToArray());

            var jobs = Assert.Contains(
                new JobKey(job),
                (IReadOnlyDictionary<JobKey, EnqueuedJob[]>)enqueuedByJob);

            return Assert.Single(jobs).State;
        }
    }

    public record EnqueuedJob(Job Job, IState State, string Id)
    {
        public void Deconstruct(out Job job, out IState state)
        {
            job = Job;
            state = State;
        }
    }

    public class JobKey : IEquatable<JobKey>
    {
        public Job Job { get; }

        public JobKey(Job job)
        {
            Job = job;
        }

        public override string ToString() => $"{Format(Job.Type)}.{Job.Method.Name}({string.Join(", ", Job.Args)})";

        static string Format(Type type) =>
            type.IsGenericType
                ? $"{type.Name[..type.Name.IndexOf('`')]}<{string.Join(", ", type.GetGenericArguments().Select(Format))}>"
                : type.Name;

        public bool Equals(JobKey? other) => other is not null
            && Job.Type == other.Job.Type
            && Job.Method == other.Job.Method
            && Job.Args.Count == other.Job.Args.Count
            && Job.Args.SequenceEqual(other.Job.Args);

        public override int GetHashCode()
        {
            var hashCode = new HashCode();
            hashCode.Add(Job.Type);
            hashCode.Add(Job.Method);
            foreach (var arg in Job.Args)
            {
                hashCode.Add(arg);
            }
            return hashCode.ToHashCode();
        }
    }
}
