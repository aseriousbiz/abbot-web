using System;
using System.Security.Claims;
using Abbot.Common.TestHelpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Serious;
using Serious.Abbot.Entities;
using Serious.Abbot.Extensions;
using Serious.Abbot.Repositories;
using Serious.TestHelpers;

public abstract class ActionTestBase<TTestData> where TTestData : CommonTestData
{
    TestEnvironmentWithData<TTestData>? _env;
    TestEnvironmentBuilder<TestEnvironmentWithData<TTestData>> _builder;
    ActionContext? _actionContext;
    HttpContext? _httpContext;
    public bool HasAuthenticatedManually { get; set; }

    public TestEnvironmentWithData<TTestData> Env => _env ??= _builder.Build();

    public TestEnvironmentBuilder<TestEnvironmentWithData<TTestData>> Builder => _env is null
        ? _builder
        : throw new InvalidOperationException("Cannot use Builder after Env has been used.");

    public ActionContext ActionContext => _actionContext ??=
        new FakeActionContext(HttpContext, new RouteData(), new ControllerActionDescriptor(), Env.Router);

    public HttpContext HttpContext => _httpContext ??=
        new FakeHttpContext()
        {
            RequestServices = Env.Services
        };

    public ModelStateDictionary ModelState => ActionContext.ModelState;
    public RouteData RouteData => ActionContext.RouteData;

    public IRoomRepository Rooms => Env.Rooms;

    public IConversationRepository Conversations => Env.Conversations;

    public FakeAuditLog AuditLog => Env.AuditLog;

    public FakeAbbotContext Db => Env.Db;

    public IUserRepository Users => Env.Users;

    public Member TestMember => Env.TestData.Member;
    public Organization TestOrganization => Env.TestData.Organization;

    protected ActionTestBase()
    {
        // ReSharper disable once VirtualMemberCallInConstructor
        _builder = CreateTestEnvironmentBuilder();
    }

    /// <summary>
    /// Apply default authentication, if appropriate.
    /// </summary>
    protected virtual void ApplyDefaultAuthentication() { }

    protected ClaimsPrincipal AuthenticateAs(Member member, Id<Skill>? skill = null)
    {
        var principal = skill is null
            ? new FakeClaimsPrincipal(member)
            : FakeClaimsPrincipal.ForSkillToken(skill.Value, member, member.User);
        AuthenticateAs(principal);
        ActionContext.HttpContext.SetCurrentMember(member);
        HasAuthenticatedManually = true;
        return principal;
    }

    protected void AuthenticateAs(ClaimsPrincipal principal)
    {
        ActionContext.HttpContext.User = principal;
    }

    protected void TimeTravelTo(DateTime nowUtc)
    {
        if (nowUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Time must be UTC", nameof(nowUtc));
        }

        Env.Clock.TravelTo(nowUtc);
    }

    protected virtual TestEnvironmentBuilder<TestEnvironmentWithData<TTestData>> CreateTestEnvironmentBuilder() =>
        TestEnvironmentBuilder.Create<TTestData>();
}

public abstract class ActionTestBase : ActionTestBase<CommonTestData>
{
}
