using Serious.Abbot.Infrastructure;
using Xunit;

public class RoutingHelperTests
{
    [Theory]
    [InlineData("/Skills/tHaT/Pay/tHE/Bills", "/skills/that/pay/the/bills")]
    [InlineData("/Skills/{SKILLID}", "/skills/{SKILLID}")]
    [InlineData("/Skills/{SKILLID}/ACTIVITY", "/skills/{SKILLID}/activity")]
    [InlineData("{JustAnId}", "{JustAnId}")]
    [InlineData("{Id}/LITERAL/{id}/LITERAL/LITERAL{ID?}LITERAL", "{Id}/literal/{id}/literal/literal{ID?}literal")]
    public void CanLowercaseRouteTemplateProperly(string input, string expected)
    {
        Assert.Equal(expected, RoutingHelper.LowercaseRouteTemplate(input));
    }
}
