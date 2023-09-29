using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using IHostingEnvironment = Microsoft.AspNetCore.Hosting.IHostingEnvironment;

namespace Serious.TestHelpers;

public class FakeHostEnvironment : IHostEnvironment
#pragma warning disable CS0618 // Type or member is obsolete in newer AspNetCore
    , IHostingEnvironment
#pragma warning restore CS0618
{
    public FakeHostEnvironment()
    {
        ApplicationName = "Unit Tests";

        EnvironmentName = "Development";
    }

    public string? EnvironmentName { get; set; }

    public string? ApplicationName { get; set; }

    public string? ContentRootPath { get; set; }

    public IFileProvider? ContentRootFileProvider { get; set; }

    string? IHostingEnvironment.WebRootPath { get; set; }

    IFileProvider? IHostingEnvironment.WebRootFileProvider { get; set; }
}
