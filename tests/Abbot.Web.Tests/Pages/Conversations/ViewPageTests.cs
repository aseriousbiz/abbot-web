using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Serious.Abbot.Entities;
using Serious.Abbot.Pages.Conversations;

public class ViewPageTests : ConversationPageTestBase<ViewPage>
{
    [Fact]
    public async Task OnGetAsync_ReturnsNotFoundIfSelectedConversationDoesNotBelongToUsersOrg()
    {
        var convo = await Db.Conversations.FirstAsync(c => c.OrganizationId == TestOrganization2.Id);

        var (_, result) = await InvokePageAsync(p =>
            p.OnGetAsync(convo.Id));

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task OnGetAsync_ReturnsNotFoundIfSelectedConversationIsHidden()
    {
        var convo = await Db.Conversations.FirstAsync(c => c.OrganizationId == TestOrganization.Id);
        convo.State = ConversationState.Hidden;
        await Db.SaveChangesAsync();

        var (_, result) = await InvokePageAsync(p =>
            p.OnGetAsync(convo.Id));

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task OnGetAsync_LoadsConversationIfItBelongsToTheUsersOrg()
    {
        var convo = await Db.Conversations.FirstAsync(c => c.OrganizationId == TestOrganization.Id);

        var (page, result) = await InvokePageAsync(p =>
            p.OnGetAsync(convo.Id));

        Assert.IsType<PageResult>(result);
        Assert.Same(page.Conversation, convo);
    }
}
