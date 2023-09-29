using System;
using System.Collections.Generic;
using Azure.Core.Serialization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Options;

namespace Serious.TestHelpers
{
    public class FakeFunctionContext : FunctionContext
    {
        public FakeFunctionContext() : this(
            "fake-invocation-id",
            "fake-function-id",
            null,
            null,
            CreateFakeServiceProvider(),
            null,
            new Dictionary<object, object>(),
            null,
            null)
        {
        }

        public FakeFunctionContext(
            string invocationId,
            string functionId,
            Microsoft.Azure.Functions.Worker.TraceContext? traceContext,
            BindingContext? bindingContext,
            IServiceProvider instanceServices,
            FunctionDefinition? functionDefinition,
            IDictionary<object, object> items,
            IInvocationFeatures? features,
            RetryContext? retryContext)
        {
            InvocationId = invocationId;
            FunctionId = functionId;
            TraceContext = traceContext!;
            BindingContext = bindingContext!;
            InstanceServices = instanceServices;
            FunctionDefinition = functionDefinition!;
            Items = items;
            Features = features!;
            RetryContext = retryContext!;
        }

        public override string InvocationId { get; }
        public override string FunctionId { get; }
        public override Microsoft.Azure.Functions.Worker.TraceContext TraceContext { get; }
        public override BindingContext BindingContext { get; }
        public override RetryContext RetryContext { get; }
        public override IServiceProvider InstanceServices { get; set; }
        public override FunctionDefinition FunctionDefinition { get; }
        public override IDictionary<object, object> Items { get; set; }
        public override IInvocationFeatures Features { get; }

        static IServiceProvider CreateFakeServiceProvider()
        {
            var provider = new FakeServiceProvider();
            var workerOptions = Options.Create(new WorkerOptions()
            {
                Serializer = new JsonObjectSerializer()
            });
            provider.AddService(typeof(IOptions<WorkerOptions>), workerOptions);
            return provider;
        }
    }
}
