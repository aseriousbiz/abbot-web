namespace Serious.TestHelpers
{
    public static class FakeServiceProviderExtensions
    {
        public static void AddFakeService<T>(this FakeHttpContext httpContext, T service) where T : notnull
        {
            ((FakeServiceProvider)httpContext.RequestServices).AddService(typeof(T), service);
        }
    }
}
