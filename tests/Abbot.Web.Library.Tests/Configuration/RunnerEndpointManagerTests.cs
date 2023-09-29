using Abbot.Common.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Serious.Abbot.Configuration;
using Serious.Abbot.Entities;

namespace Abbot.Web.Library.Tests.Configuration;

public class RunnerEndpointManagerTests
{
    public class TheGetEndpointAsyncMethod
    {
        [Theory]
        [InlineData(CodeLanguage.CSharp, "https://config.example|abcd", null, null, "https://config.example/", "abcd", true)]
        [InlineData(CodeLanguage.CSharp, "https://config.example|abcd", "https://global.example|efgh", null, "https://global.example/", "efgh", true)]
        [InlineData(CodeLanguage.CSharp, "https://config.example|abcd", "https://global.example|efgh", "https://org.example|ijkl", "https://org.example/", "ijkl", false)]
        [InlineData(CodeLanguage.Python, "https://config.example|abcd", null, null, "https://config.example/", "abcd", true)]
        [InlineData(CodeLanguage.Python, "https://config.example|abcd", "https://global.example|efgh", null, "https://global.example/", "efgh", true)]
        [InlineData(CodeLanguage.Python, "https://config.example|abcd", "https://global.example|efgh", "https://org.example|ijkl", "https://org.example/", "ijkl", false)]
        [InlineData(CodeLanguage.JavaScript, "https://config.example|abcd", null, null, "https://config.example/", "abcd", true)]
        [InlineData(CodeLanguage.JavaScript, "https://config.example|abcd", "https://global.example|efgh", null, "https://global.example/", "efgh", true)]
        [InlineData(CodeLanguage.JavaScript, "https://config.example|abcd", "https://global.example|efgh", "https://org.example|ijkl", "https://org.example/", "ijkl", false)]
        [InlineData(CodeLanguage.Ink, "https://config.example|abcd", null, null, "https://config.example/", "abcd", true)]
        [InlineData(CodeLanguage.Ink, "https://config.example|abcd", "https://global.example|efgh", null, "https://global.example/", "efgh", true)]
        [InlineData(CodeLanguage.Ink, "https://config.example|abcd", "https://global.example|efgh", "https://org.example|ijkl", "https://org.example/", "ijkl", false)]
        public async Task ReturnsEndpointsInCorrectPriorityOrder(
            CodeLanguage codeLanguage,
            string configEndpoint,
            string? globalOverrideEndpoint,
            string? orgOverrideEndpoint,
            string expectedEndpoint,
            string expectedApiToken,
            bool expectedIsHosted)
        {
            var env = TestEnvironmentBuilder
                .Create()
                .ConfigureServices(s => {
                    var splat = configEndpoint.Split('|');
                    s.Configure<SkillOptions>(o => {
                        o.DotNetEndpoint = codeLanguage == CodeLanguage.CSharp ? splat[0] : null;
                        o.DotNetEndpointCode = codeLanguage == CodeLanguage.CSharp ? splat[1] : null;
                        o.PythonEndpoint = codeLanguage == CodeLanguage.Python ? splat[0] : null;
                        o.PythonEndpointCode = codeLanguage == CodeLanguage.Python ? splat[1] : null;
                        o.JavaScriptEndpoint = codeLanguage == CodeLanguage.JavaScript ? splat[0] : null;
                        o.JavaScriptEndpointCode = codeLanguage == CodeLanguage.JavaScript ? splat[1] : null;
                        o.InkEndpoint = codeLanguage == CodeLanguage.Ink ? splat[0] : null;
                        o.InkEndpointCode = codeLanguage == CodeLanguage.Ink ? splat[1] : null;
                    });
                })
                .Build();
            var organization = env.TestData.Organization;

            if (orgOverrideEndpoint?.Split("|") is [{ } orgUrl, var orgToken])
            {
                organization.Settings.SkillEndpoints[codeLanguage] = new(new(orgUrl), orgToken);
            }

            await env.Db.SaveChangesAsync();

            var globalOverrides = new Dictionary<CodeLanguage, SkillRunnerEndpoint>();
            if (globalOverrideEndpoint?.Split("|") is [{ } globalUrl, var globalToken])
            {
                globalOverrides[codeLanguage] = new(new(globalUrl), globalToken);
                await env.Settings.SetAsync(
                    SettingsScope.Global,
                    "RunnerEndpoints",
                    JsonConvert.SerializeObject(globalOverrides),
                    env.TestData.User);
            }

            var endpointManager = env.Activate<RunnerEndpointManager>();
            var endpoint = await endpointManager.GetEndpointAsync(organization, codeLanguage);
            Assert.Equal(expectedEndpoint, endpoint.Url.ToString());
            Assert.Equal(expectedApiToken, endpoint.ApiToken);
            Assert.Equal(expectedIsHosted, endpoint.IsHosted);
        }
    }

    public class TheSetGlobalOverrideAsyncMethod
    {
        [Fact]
        public async Task InitializesSettingIfNoExistingValue()
        {
            var env = TestEnvironment.Create();
            var endpointManager = env.Activate<RunnerEndpointManager>();
            await endpointManager.SetGlobalOverrideAsync(
                CodeLanguage.Ink,
                new(new("https://inkywinky"), null),
                env.TestData.Member);

            var setting = await env.Settings.GetAsync(SettingsScope.Global, "RunnerEndpoints");
            Assert.NotNull(setting);
            Assert.Equal("""
{"Ink":{"Url":"https://inkywinky","ApiToken":null,"IsHosted":false}}
""",
                setting.Value);
        }

        [Fact]
        public async Task UpdatesSettingIfExistingValue()
        {
            var env = TestEnvironment.Create();
            await env.Settings.SetAsync(SettingsScope.Global,
                "RunnerEndpoints",
                """
{"CSharp":{"Url":"https://sharpy","ApiToken":null,"IsHosted":false},"Ink":{"Url":"https://blot","ApiToken":null,"IsHosted":false},"JavaScript":{"Url":"https://scripty","ApiToken":null,"IsHosted":false},"Python":{"Url":"https://aspish","ApiToken":null,"IsHosted":false}}
""",
                env.TestData.User);
            var endpointManager = env.Activate<RunnerEndpointManager>();
            await endpointManager.SetGlobalOverrideAsync(
                CodeLanguage.Ink,
                new(new("https://inkywinky"), "42"),
                env.TestData.Member);

            var setting = await env.Settings.GetAsync(SettingsScope.Global, "RunnerEndpoints");
            Assert.NotNull(setting);
            Assert.Equal("""
{"CSharp":{"Url":"https://sharpy","ApiToken":null,"IsHosted":false},"Ink":{"Url":"https://inkywinky","ApiToken":"42","IsHosted":false},"JavaScript":{"Url":"https://scripty","ApiToken":null,"IsHosted":false},"Python":{"Url":"https://aspish","ApiToken":null,"IsHosted":false}}
""",
                setting.Value);
        }
    }

    public class TheGetAppConfigEndpointsAsyncMethod
    {
        [Fact]
        public async Task GetsAllAppConfigEndpoints()
        {
            var env = TestEnvironmentBuilder
                .Create()
                .ConfigureServices(s => {
                    s.Configure<SkillOptions>(o => {
                        o.DotNetEndpoint = "https://dotnet";
                        o.DotNetEndpointCode = "dotnetrocks";
                        o.PythonEndpoint = "https://python";
                        o.PythonEndpointCode = "pythonrocks";
                        o.JavaScriptEndpoint = "https://javascript";
                        o.JavaScriptEndpointCode = "jsrocks";
                        o.InkEndpoint = "https://ink";
                        o.InkEndpointCode = "inkisfine";
                    });
                })
                .Build();

            var endpointManager = env.Activate<RunnerEndpointManager>();
            var endpoints = await endpointManager.GetAppConfigEndpointsAsync();
            Assert.Equal(new (CodeLanguage, SkillRunnerEndpoint)[]
            {
                (CodeLanguage.CSharp, new(new("https://dotnet"), "dotnetrocks", true)),
                (CodeLanguage.Python, new(new("https://python"), "pythonrocks", true)),
                (CodeLanguage.JavaScript, new(new("https://javascript"), "jsrocks", true)),
                (CodeLanguage.Ink, new(new("https://ink"), "inkisfine", true)),
            }, endpoints.OrderBy(p => p.Key).Select(p => (p.Key, p.Value)).ToArray());
        }
    }

    public class TheGetGlobalOverridesAsyncMethod
    {
        [Fact]
        public async Task GetsAllGlobalOverridesConfigured()
        {
            var env = TestEnvironment.Create();
            await env.Settings.SetAsync(SettingsScope.Global,
                "RunnerEndpoints",
                """
{"CSharp":{"Url":"http://sharpy","ApiToken":"1","IsHosted":true},"Ink":{"Url":"http://blot","ApiToken":"2","IsHosted":true},"JavaScript":{"Url":"http://scripty","ApiToken":"3","IsHosted":true},"Python":{"Url":"http://aspish","ApiToken":"4","IsHosted":true}}
""",
                env.TestData.User);

            var endpointManager = env.Activate<RunnerEndpointManager>();
            var endpoints = await endpointManager.GetGlobalOverridesAsync();
            Assert.Equal(new (CodeLanguage, SkillRunnerEndpoint)[]
            {
                (CodeLanguage.CSharp, new(new("http://sharpy"), "1", true)),
                (CodeLanguage.Python, new(new("http://aspish"), "4", true)),
                (CodeLanguage.JavaScript, new(new("http://scripty"), "3", true)),
                (CodeLanguage.Ink, new(new("http://blot"), "2", true)),
            }, endpoints.OrderBy(p => p.Key).Select(p => (p.Key, p.Value)).ToArray());
        }
    }

    public class TheClearGlobalOverridesAsyncMethod
    {
        [Fact]
        public async Task ClearsTheSpecifiedGlobalOverride()
        {
            var env = TestEnvironment.Create();
            await env.Settings.SetAsync(SettingsScope.Global,
                "RunnerEndpoints",
                """
{"CSharp":{"Url":"https://sharpy","ApiToken":"1","IsHosted":true},"Ink":{"Url":"https://blot","ApiToken":"2","IsHosted":true},"JavaScript":{"Url":"https://scripty","ApiToken":"3","IsHosted":true},"Python":{"Url":"https://aspish","ApiToken":"4","IsHosted":true}}
""",
                env.TestData.User);

            var endpointManager = env.Activate<RunnerEndpointManager>();
            await endpointManager.ClearGlobalOverrideAsync(CodeLanguage.Python, env.TestData.Member);
            var setting = await env.Settings.GetAsync(SettingsScope.Global, "RunnerEndpoints");
            Assert.NotNull(setting);
            Assert.Equal("""
{"CSharp":{"Url":"https://sharpy","ApiToken":"1","IsHosted":true},"Ink":{"Url":"https://blot","ApiToken":"2","IsHosted":true},"JavaScript":{"Url":"https://scripty","ApiToken":"3","IsHosted":true}}
""", setting.Value);
        }
    }
}
